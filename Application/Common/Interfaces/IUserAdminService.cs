using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Orchestration métier de la gestion des utilisateurs (administration) :
/// liste, détails, CRUD, réinitialisation de mot de passe. L'import Excel
/// reste isolé dans son propre flux.
/// </summary>
public interface IUserAdminService
{
    Task<UsersListViewModel> GetListAsync(string? recherche, Guid? directionId, RoleUtilisateur? role, int page, int pageSize);
    Task<UserDetailsDto?> GetDetailsAsync(Guid id);
    Task<OperationResult> CreateAsync(CreateUserInput input);
    Task<OperationResult> UpdateAsync(UpdateUserInput input);
    Task<OperationResult> DeleteAsync(Guid id);
    Task<OperationResult> ResetPasswordAsync(Guid id, string? nouveauMotDePasse);

    /// <summary>
    /// Régénère un jeton d'activation et renvoie l'email d'activation à l'utilisateur.
    /// Invalide d'abord les jetons précédents non utilisés (soft-delete).
    /// </summary>
    Task<OperationResult> RenvoyerLienActivationAsync(Guid id);
}
