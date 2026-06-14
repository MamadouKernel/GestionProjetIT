using GestionProjects.Application.Common.Results;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Workflows d'écriture de la page Détails projet (réassignation chef de projet, …).
    /// Le contrôleur garde l'autorisation ; ce service possède la logique métier + audit.
    /// Enrichi progressivement au fil de l'extraction de ProjetController.Details.
    /// </summary>
    public interface IProjetDetailsWorkflowService
    {
        /// <summary>
        /// Réassigne (ou retire) le Responsable Solutions IT (chef de projet) et tient
        /// l'historique. Erreurs métier renvoyées en WorkflowResult.Error.
        /// </summary>
        Task<WorkflowResult> UpdateChefProjetAsync(Guid projetId, Guid? chefProjetId);
    }
}
