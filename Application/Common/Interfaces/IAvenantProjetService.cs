using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Workflow de gestion du changement (avenant projet) : un changement de périmètre,
    /// budget ou délai après baseline est tracé, validé par le Métier (DM) puis par la DSI
    /// (dont la validation applique le changement au projet). L'autorisation reste au
    /// contrôleur ; ce service possède la logique métier, l'application et l'audit.
    /// </summary>
    public interface IAvenantProjetService
    {
        Task<List<AvenantProjet>> ListerAsync(Guid projetId);

        Task<WorkflowResult> CreerAsync(
            Guid projetId,
            Guid userId,
            TypeAvenant type,
            string titre,
            string justification,
            string? descriptionPerimetre,
            decimal? nouveauBudget,
            DateTime? nouvelleDateFinPrevue);

        Task<WorkflowResult> ValiderDmAsync(Guid avenantId, Guid userId);
        Task<WorkflowResult> ValiderDsiAsync(Guid avenantId, Guid userId);
        Task<WorkflowResult> RejeterAsync(Guid avenantId, Guid userId, string commentaire);
    }
}
