using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Orchestration du workflow de demande de création de compte (validation DM,
/// validation/création DSI, refus). Les contrôles de permission « globaux »
/// restent au controller ; le service gère l'appartenance, l'état, la
/// persistance et les notifications.
/// </summary>
public interface IDemandeCompteAdminService
{
    Task<List<DemandeCreationCompte>> GetListAsync(Guid? restrictToDmId);
    Task<WorkflowResult> ValiderDmAsync(Guid id, string? commentaire, Guid currentUserId, bool hasFullScope, string nomActeur);
    Task<WorkflowResult> RefuserDmAsync(Guid id, string? commentaire, Guid currentUserId, bool hasFullScope, string nomActeur);
    Task<WorkflowResult> ValiderDsiAsync(Guid id, string? commentaire, RoleUtilisateur role);
    Task<WorkflowResult> RefuserDsiAsync(Guid id, string? commentaire, string nomActeur);
}
