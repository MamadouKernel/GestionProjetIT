using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Persistance de la fiche projet CIT. Le contrôleur garde l'autorisation ;
    /// ce service possède la création/mise à jour + la règle d'écart budget + l'audit.
    /// </summary>
    public interface IFicheProjetService
    {
        /// <summary>
        /// Crée ou met à jour la fiche d'un projet. Erreur métier (justification d'écart
        /// budget manquante) renvoyée en WorkflowResult.Error ; NotFound si projet absent.
        /// </summary>
        /// <summary>Récupère (ou crée) la fiche d'un projet pour affichage et synchronise les indicateurs de livrables.</summary>
        Task<FicheProjet> ObtenirPourAffichageAsync(Projet projet);

        Task<WorkflowResult> SauvegarderAsync(Guid projetId, FicheProjet fiche, Guid userId);
    }
}
