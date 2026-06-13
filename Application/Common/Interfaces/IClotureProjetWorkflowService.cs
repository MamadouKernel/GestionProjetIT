using GestionProjects.Application.Common.Results;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Workflow de clôture projet (demande → validations Demandeur/DM/DSI → clôture finale,
    /// rejets, commentaire technique, forçage de statut). L'autorisation et les listes de
    /// validation restent au contrôleur ; ce service possède la logique métier + audit.
    /// </summary>
    public interface IClotureProjetWorkflowService
    {
        Task<WorkflowResult> DemanderClotureAsync(Guid projetId, Guid userId, string? commentaire, DateTime? dateSouhaiteeCloture);
        Task<WorkflowResult> ValiderClotureDemandeurAsync(Guid demandeClotureId);
        Task<WorkflowResult> ValiderClotureDmAsync(Guid demandeClotureId);
        Task<WorkflowResult> RejeterClotureDmAsync(Guid demandeClotureId, string commentaire);
        Task<WorkflowResult> ValiderClotureDsiAsync(Guid demandeClotureId);
        Task<WorkflowResult> RejeterClotureDsiAsync(Guid demandeClotureId, string commentaire);
        Task<WorkflowResult> AjouterCommentaireTechniqueAsync(Guid projetId, Guid userId, string? commentaireTechnique);
        Task<WorkflowResult> ForcerStatutProjetAsync(Guid projetId, string actionType, string commentaire);
    }
}
