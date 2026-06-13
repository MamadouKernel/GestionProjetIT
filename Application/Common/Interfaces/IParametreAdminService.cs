using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>Orchestration métier de la gestion des paramètres système (administration).</summary>
public interface IParametreAdminService
{
    Task<ParametresViewModel> GetViewModelAsync();
    Task SaveWorkflowAsync(ParametresWorkflowInput input);
    Task<ParametreDetailsDto?> GetDetailsAsync(Guid id);
    Task<OperationResult> CreateAsync(CreateParametreInput input);
    Task<OperationResult> UpdateAsync(UpdateParametreInput input);
    Task<OperationResult> DeleteAsync(Guid id);
    /// <summary>Crée/met à jour/efface le webhook Teams. Retourne le message de succès adapté.</summary>
    Task<string> SaveTeamsWebhookAsync(Guid? parametreId, string? webhookUrl);
}
