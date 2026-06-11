using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Drawing = DocumentFormat.OpenXml.Drawing;
using GestionProjects.Application.DTOs;
using Microsoft.AspNetCore.StaticFiles;
using OfficeOpenXml;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace GestionProjects.Infrastructure.Services;

public interface IDocumentPreviewService
{
    Task<DocumentPreviewViewModel> BuildPreviewAsync(
        string relativePath,
        string displayName,
        string title,
        string rawUrl,
        string downloadUrl);

    string GetMimeType(string fileName);
}

public class DocumentPreviewService : IDocumentPreviewService
{
    private const int MaxTextPreviewLength = 120_000;
    private const int MaxSpreadsheetSheets = 5;
    private const int MaxSpreadsheetRows = 50;
    private const int MaxSpreadsheetColumns = 12;
    private static readonly Regex MultipleLineBreaksRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private readonly IFileStorageService _fileStorage;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public DocumentPreviewService(IFileStorageService fileStorage)
    {
        _fileStorage = fileStorage;

        _contentTypeProvider.Mappings[".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        _contentTypeProvider.Mappings[".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        _contentTypeProvider.Mappings[".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        _contentTypeProvider.Mappings[".doc"] = "application/msword";
        _contentTypeProvider.Mappings[".xls"] = "application/vnd.ms-excel";
        _contentTypeProvider.Mappings[".csv"] = "text/csv";
        _contentTypeProvider.Mappings[".md"] = "text/markdown";
    }

    public async Task<DocumentPreviewViewModel> BuildPreviewAsync(
        string relativePath,
        string displayName,
        string title,
        string rawUrl,
        string downloadUrl)
    {
        var absolutePath = _fileStorage.GetAbsolutePath(relativePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Le document demandé est introuvable.", absolutePath);

        var fileInfo = new FileInfo(absolutePath);
        var extension = Path.GetExtension(displayName).ToLowerInvariant();

        var model = new DocumentPreviewViewModel
        {
            Title = title,
            DisplayName = displayName,
            FileExtension = extension,
            MimeType = GetMimeType(displayName),
            FileSizeDisplay = FormatFileSize(fileInfo.Length),
            RawUrl = rawUrl,
            DownloadUrl = downloadUrl
        };

        switch (extension)
        {
            case ".pdf":
                model.Mode = DocumentPreviewMode.Pdf;
                model.InfoMessage = "Le document est affiché directement dans l'application.";
                break;

            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".gif":
            case ".bmp":
            case ".webp":
                model.Mode = DocumentPreviewMode.Image;
                model.InfoMessage = "L'image est affichée directement dans l'application.";
                break;

            case ".txt":
            case ".json":
            case ".xml":
            case ".csv":
            case ".md":
            case ".log":
                model.Mode = DocumentPreviewMode.Text;
                model.TextContent = TruncateText(await ReadTextFileAsync(absolutePath));
                break;

            case ".docx":
                model.Mode = DocumentPreviewMode.Text;
                model.TextContent = TruncateText(ExtractWordPreview(absolutePath));
                model.InfoMessage = "Aperçu texte extrait du document Word.";
                break;

            case ".xlsx":
                model.Mode = DocumentPreviewMode.Spreadsheet;
                model.Sheets = ExtractSpreadsheetPreview(absolutePath);
                model.InfoMessage = $"Aperçu limité aux {MaxSpreadsheetSheets} premières feuilles, {MaxSpreadsheetRows} lignes et {MaxSpreadsheetColumns} colonnes par feuille.";
                break;

            case ".pptx":
                model.Mode = DocumentPreviewMode.Text;
                model.TextContent = TruncateText(ExtractPresentationPreview(absolutePath));
                model.InfoMessage = "Aperçu texte extrait des diapositives.";
                break;

            case ".zip":
                model.Mode = DocumentPreviewMode.Archive;
                model.ArchiveEntries = ExtractArchiveEntries(absolutePath);
                model.InfoMessage = "Le contenu de l'archive est listé ci-dessous.";
                break;

            case ".doc":
            case ".xls":
                model.Mode = DocumentPreviewMode.Text;
                model.TextContent = TruncateText(ExtractBinaryTextPreview(absolutePath));
                model.InfoMessage = "Aperçu texte reconstruit depuis un format binaire ancien. Pour un rendu complet, privilégiez PDF, DOCX ou XLSX.";
                break;

            default:
                model.Mode = DocumentPreviewMode.Unsupported;
                model.InfoMessage = $"Le format {extension} ne dispose pas d'un aperçu riche dans l'application. Vous pouvez télécharger le fichier brut.";
                break;
        }

        if (model.Mode == DocumentPreviewMode.Text && string.IsNullOrWhiteSpace(model.TextContent))
        {
            model.Mode = DocumentPreviewMode.Unsupported;
            model.InfoMessage = $"Le contenu du fichier {extension} n'a pas pu être extrait automatiquement. Vous pouvez télécharger le fichier brut.";
        }

        if (model.Mode == DocumentPreviewMode.Spreadsheet && !model.Sheets.Any())
        {
            model.Mode = DocumentPreviewMode.Unsupported;
            model.InfoMessage = "Le classeur est vide ou son contenu n'a pas pu être prévisualisé.";
        }

        return model;
    }

    public string GetMimeType(string fileName)
    {
        if (_contentTypeProvider.TryGetContentType(fileName, out var contentType))
            return contentType;

        return "application/octet-stream";
    }

    private static async Task<string> ReadTextFileAsync(string absolutePath)
    {
        using var stream = File.OpenRead(absolutePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    private static string ExtractWordPreview(string absolutePath)
    {
        using var document = WordprocessingDocument.Open(absolutePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
            return string.Empty;

        var lines = new List<string>();

        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                var text = NormalizeExtractedText(string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)));
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text);
            }
            else if (element is Table table)
            {
                foreach (var row in table.Elements<TableRow>())
                {
                    var cells = row.Elements<TableCell>()
                        .Select(cell => NormalizeExtractedText(string.Concat(cell.Descendants<Text>().Select(t => t.Text))))
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .ToList();

                    if (cells.Any())
                        lines.Add(string.Join(" | ", cells));
                }
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static List<DocumentPreviewSheetViewModel> ExtractSpreadsheetPreview(string absolutePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage(new FileInfo(absolutePath));
        var result = new List<DocumentPreviewSheetViewModel>();

        foreach (var worksheet in package.Workbook.Worksheets.Take(MaxSpreadsheetSheets))
        {
            if (worksheet.Dimension == null)
                continue;

            var endRow = Math.Min(worksheet.Dimension.End.Row, MaxSpreadsheetRows);
            var endColumn = Math.Min(worksheet.Dimension.End.Column, MaxSpreadsheetColumns);

            var headers = new List<string>();
            for (var column = 1; column <= endColumn; column++)
            {
                var header = worksheet.Cells[1, column].Text?.Trim();
                headers.Add(string.IsNullOrWhiteSpace(header) ? $"Colonne {column}" : header);
            }

            var rows = new List<List<string>>();
            for (var row = 2; row <= endRow; row++)
            {
                var values = new List<string>();
                var hasContent = false;

                for (var column = 1; column <= endColumn; column++)
                {
                    var value = worksheet.Cells[row, column].Text?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        hasContent = true;

                    values.Add(value);
                }

                if (hasContent)
                    rows.Add(values);
            }

            if (!rows.Any())
            {
                for (var row = 1; row <= endRow; row++)
                {
                    var values = new List<string>();
                    var hasContent = false;

                    for (var column = 1; column <= endColumn; column++)
                    {
                        var value = worksheet.Cells[row, column].Text?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(value))
                            hasContent = true;

                        values.Add(value);
                    }

                    if (hasContent)
                        rows.Add(values);
                }
            }

            result.Add(new DocumentPreviewSheetViewModel
            {
                Name = worksheet.Name,
                Headers = headers,
                Rows = rows
            });
        }

        return result;
    }

    private static string ExtractPresentationPreview(string absolutePath)
    {
        using var presentation = PresentationDocument.Open(absolutePath, false);
        var presentationPart = presentation.PresentationPart;
        var slideIdList = presentationPart?.Presentation?.SlideIdList;
        if (presentationPart == null || slideIdList == null)
            return string.Empty;

        var lines = new List<string>();
        var slideNumber = 1;

        foreach (var slideId in slideIdList.Elements<DocumentFormat.OpenXml.Presentation.SlideId>())
        {
            var relationshipId = slideId.RelationshipId?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId))
                continue;

            var slidePart = presentationPart.GetPartById(relationshipId) as SlidePart;
            if (slidePart?.Slide == null)
            {
                slideNumber++;
                continue;
            }

            var texts = slidePart.Slide.Descendants<Drawing.Text>()
                .Select(text => NormalizeExtractedText(text.Text))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (texts.Any())
            {
                lines.Add($"Diapositive {slideNumber}");
                lines.AddRange(texts);
                lines.Add(string.Empty);
            }

            slideNumber++;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<string> ExtractArchiveEntries(string absolutePath)
    {
        using var archive = ZipFile.OpenRead(absolutePath);
        return archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FullName))
            .Select(entry => $"{entry.FullName} ({FormatFileSize(entry.Length)})")
            .ToList();
    }

    private static string ExtractBinaryTextPreview(string absolutePath)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var extracted = new List<string>();
        extracted.AddRange(ExtractUtf16Strings(bytes));
        extracted.AddRange(ExtractAsciiStrings(bytes));

        return string.Join(
            Environment.NewLine,
            extracted
                .Select(NormalizeExtractedText)
                .Where(IsMeaningfulExtract)
                .Distinct()
                .Take(400));
    }

    private static IEnumerable<string> ExtractUtf16Strings(byte[] bytes)
    {
        var buffer = new StringBuilder();

        for (var index = 0; index + 1 < bytes.Length; index += 2)
        {
            var character = BitConverter.ToUInt16(bytes, index);
            if (IsPrintableChar((char)character))
            {
                buffer.Append((char)character);
            }
            else
            {
                if (buffer.Length >= 5)
                    yield return buffer.ToString();

                buffer.Clear();
            }
        }

        if (buffer.Length >= 5)
            yield return buffer.ToString();
    }

    private static IEnumerable<string> ExtractAsciiStrings(byte[] bytes)
    {
        var buffer = new StringBuilder();

        foreach (var value in bytes)
        {
            if (value is >= 32 and <= 126 || value is 9 or 10 or 13)
            {
                buffer.Append((char)value);
            }
            else
            {
                if (buffer.Length >= 5)
                    yield return buffer.ToString();

                buffer.Clear();
            }
        }

        if (buffer.Length >= 5)
            yield return buffer.ToString();
    }

    private static bool IsPrintableChar(char value)
    {
        return !char.IsControl(value) || value is '\n' or '\r' or '\t';
    }

    private static bool IsMeaningfulExtract(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Length < 4)
            return false;

        return value.Any(char.IsLetter);
    }

    private static string NormalizeExtractedText(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = MultipleLineBreaksRegex.Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    private static string TruncateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Length <= MaxTextPreviewLength)
            return text;

        return text[..MaxTextPreviewLength] + $"{Environment.NewLine}{Environment.NewLine}[Aperçu tronqué]";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["octets", "Ko", "Mo", "Go"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }
}
