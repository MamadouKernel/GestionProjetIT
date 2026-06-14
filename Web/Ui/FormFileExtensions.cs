using GestionProjects.Application.Common.Models;
using Microsoft.AspNetCore.Http;

namespace GestionProjects.Web.Ui;

public static class FormFileExtensions
{
    public static UploadedFileInput ToUploadedFileInput(this IFormFile file)
    {
        return new UploadedFileInput(file.FileName, file.ContentType, file.Length, file.OpenReadStream);
    }

    public static List<UploadedFileInput>? ToUploadedFileInputs(this IEnumerable<IFormFile>? files)
    {
        return files?
            .Where(file => file.Length > 0)
            .Select(file => file.ToUploadedFileInput())
            .ToList();
    }
}
