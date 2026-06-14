using GestionProjects.Application.ViewModels;
using GestionProjects.Application.ViewModels.Account;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces;

public interface IAccountService
{
    Task<AccountLoginResult> ValidateLocalLoginAsync(LoginViewModel model);
    Task RecordLoginAsync(Guid userId);
    Task<ProfilViewModel?> GetProfilAsync(Guid userId);
    Task<AccountProfileUpdateResult> UpdateProfilAsync(Guid userId, ProfilViewModel model, bool hasModelStateErrors);
    Task<InscriptionViewModel> BuildInscriptionViewModelAsync();
    Task<IReadOnlyList<AccountLookupItem>> GetServicesByDirectionAsync(Guid directionId);
    Task<IReadOnlyList<AccountLookupItem>> GetDirecteursMetierByDirectionAsync(Guid directionId);
}

public sealed record AccountLoginResult(Utilisateur? User, string? ErrorMessage)
{
    public bool Succeeded => User != null && string.IsNullOrWhiteSpace(ErrorMessage);
}

public sealed record AccountValidationError(string Field, string Message);

public sealed record AccountProfileUpdateResult(
    bool NotFound,
    bool Succeeded,
    ProfilViewModel ViewModel,
    Utilisateur? User,
    IReadOnlyList<AccountValidationError> Errors);

public sealed record AccountLookupItem(Guid Id, string Libelle);
