using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

public interface IAutorisationMatrixService
{
    Task<AutorisationsViewModel> BuildIndexAsync();
    Task<OperationResult> UpdatePermissionAsync(UpdateRolePermissionInput input);
    Task<OperationResult> InitialiserPermissionsAsync();
}

public sealed record UpdateRolePermissionInput(
    RoleUtilisateur Role,
    string Controleur,
    string Action,
    bool EstActif);
