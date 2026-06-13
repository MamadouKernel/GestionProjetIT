using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Orchestration métier de la gestion des directions (administration).
/// Sépare la logique de données/validation du controller, qui ne garde
/// que la traduction HTTP.
/// </summary>
public interface IDirectionAdminService
{
    Task<DirectionsListViewModel> GetListAsync(string? recherche, string? statut, int page, int pageSize);
    Task<string?> GetCodeAsync(Guid id);
    Task<OperationResult> CreateAsync(CreateDirectionInput input);
    Task<OperationResult> UpdateAsync(UpdateDirectionInput input);
    Task<OperationResult> DeleteAsync(Guid id);
}
