using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>Orchestration métier de la gestion des rôles utilisateurs (administration).</summary>
public interface IRoleAdminService
{
    Task<RolesListViewModel> GetListAsync(string? recherche, Guid? directionId, RoleUtilisateur? role, int page, int pageSize);
    Task<GererRolesViewModel?> GetUserForRolesAsync(Guid id);
    Task<UpdateRolesResult> UpdateRolesAsync(Guid id, string? roles);
}

/// <summary>Résultat de la synchronisation des rôles, avec les nuances de redirection/messages.</summary>
public record UpdateRolesResult(
    bool NotFound,
    bool NoRolesSelected,
    string? InfoMessage,
    string? SuccessMessage);
