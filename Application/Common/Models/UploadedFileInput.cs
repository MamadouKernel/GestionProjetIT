namespace GestionProjects.Application.Common.Models;

/// <summary>
/// Fichier transmis aux cas d'utilisation sans exposer ASP.NET a la couche Application.
/// </summary>
public sealed class UploadedFileInput
{
    private readonly Func<Stream> _openReadStream;

    public UploadedFileInput(string fileName, string contentType, long length, Func<Stream> openReadStream)
    {
        FileName = fileName;
        ContentType = contentType;
        Length = length;
        _openReadStream = openReadStream;
    }

    public string FileName { get; }
    public string ContentType { get; }
    public long Length { get; }

    public Stream OpenReadStream() => _openReadStream();
}
