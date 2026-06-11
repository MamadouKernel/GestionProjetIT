namespace GestionProjects.Application.DTOs;

public enum DocumentPreviewMode
{
    Text,
    Spreadsheet,
    Image,
    Pdf,
    Archive,
    Unsupported
}

public class DocumentPreviewViewModel
{
    public string Title { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string FileSizeDisplay { get; set; } = string.Empty;
    public string RawUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DocumentPreviewMode Mode { get; set; }
    public string? InfoMessage { get; set; }
    public string? TextContent { get; set; }
    public List<DocumentPreviewSheetViewModel> Sheets { get; set; } = new();
    public List<string> ArchiveEntries { get; set; } = new();
}

public class DocumentPreviewSheetViewModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}
