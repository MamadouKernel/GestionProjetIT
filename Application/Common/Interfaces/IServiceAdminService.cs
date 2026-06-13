using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Orchestration métier de la gestion des services (administration).
/// </summary>
public interface IServiceAdminService
{
    Task<ServicesListViewModel> GetListAsync(string? recherche, Guid? directionId, string? statut, int page, int pageSize);
    Task<OperationResult> CreateAsync(CreateServiceInput input);
    Task<OperationResult> UpdateAsync(UpdateServiceInput input);
    Task<OperationResult> DeleteAsync(Guid id);
}
