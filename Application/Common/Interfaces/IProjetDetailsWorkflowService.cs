using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Models;

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

        /// <summary>
        /// Démarre opérationnellement un projet créé après validation DSI.
        /// La création administrative reste NonDemarre/0%; cette action marque la prise
        /// en charge réelle sans gonfler artificiellement l'avancement.
        /// </summary>
        Task<WorkflowResult> DemarrerProjetAsync(Guid projetId, Guid userId);

        /// <summary>
        /// Suspend un projet en cours (mise en pause maîtrisée) avec un motif obligatoire,
        /// et le repasse en cours via la reprise. Tient l'historique de phase.
        /// </summary>
        Task<WorkflowResult> SuspendreProjetAsync(Guid projetId, Guid userId, string motif);
        Task<WorkflowResult> ReprendreProjetAsync(Guid projetId, Guid userId);

        // ── Corbeille (réservé AdminIT — gating fait par le contrôleur) ────────
        Task<WorkflowResult> SupprimerAsync(Guid projetId, string nomActeur);
        Task<WorkflowResult> RestaurerAsync(Guid projetId, string nomActeur);

        /// <summary>
        /// Assemble le ProjetDetailsViewModel : chargements conditionnels par onglet,
        /// liste des chefs de projet réassignables et audit de prise en charge.
        /// Le contrôleur garde l'autorisation (BuildProjectUi) et le recalcul d'avancement ;
        /// les drapeaux d'habilitation sont passés en booléens pour ne pas coupler la couche
        /// Application à l'Infrastructure.
        /// </summary>
        Task<ProjetDetailsViewModel> BuildDetailsViewModelAsync(
            Projet projet,
            Guid userId,
            string? tab,
            bool isReadOnly,
            bool canReassignChefProjet,
            bool isAssignedChefProjet,
            bool canOpenChargesTab);
    }
}
