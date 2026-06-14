using GestionProjects.Application.ViewModels.Admin;

namespace GestionProjects.Application.Common.Interfaces;

public interface IUserImportService
{
    Task<UserImportResult> ImportUsersAsync(
        Stream? fichierExcel,
        string? fileName,
        long fileLength,
        string motDePasseParDefaut,
        bool ignorerDoublons);
}

public sealed record UserImportResult(ImportUsersViewModel ViewModel, string? SuccessMessage = null);
