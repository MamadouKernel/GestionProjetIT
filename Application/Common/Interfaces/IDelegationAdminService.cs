using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Orchestration des délégations (validation DSI + chefferie de projet).
/// Le controller fournit l'identité courante et le périmètre (full scope) ;
/// le service applique l'appartenance, la validation et la persistance.
/// </summary>
public interface IDelegationAdminService
{
    Task<DelegationsPageViewModel> GetPageAsync(
        Guid currentUserId, bool hasFullScope, string? tab,
        string? rechercheDsi, string? rechercheChef,
        int pageDsi, int pageChef, int pageSize);

    Task<DelegationDetailsResult> GetDsiAsync(Guid id, Guid currentUserId, bool hasFullScope);
    Task<WorkflowResult> CreateDsiAsync(CreateDelegationDsiInput input);
    Task<WorkflowResult> UpdateDsiAsync(UpdateDelegationDsiInput input);
    Task<WorkflowResult> DeleteDsiAsync(Guid id, Guid currentUserId, bool hasFullScope);

    Task<DelegationsChefProjetPageViewModel> GetChefProjetPageAsync(Guid currentUserId, bool hasFullScope);
    Task<DelegationDetailsResult> GetChefAsync(Guid id, Guid currentUserId, bool hasFullScope);
    Task<WorkflowResult> CreateChefAsync(CreateDelegationChefInput input);
    Task<WorkflowResult> UpdateChefAsync(UpdateDelegationChefInput input);
    Task<WorkflowResult> DeleteChefAsync(Guid id, Guid currentUserId, bool hasFullScope);
}

/// <summary>Résultat d'une lecture de détail : introuvable / interdit / données JSON.</summary>
public sealed record DelegationDetailsResult(bool IsNotFound, bool IsForbidden, object? Data)
{
    public static DelegationDetailsResult NotFound() => new(true, false, null);
    public static DelegationDetailsResult Forbidden() => new(false, true, null);
    public static DelegationDetailsResult Ok(object data) => new(false, false, data);
}
