using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Persistance et audit des livrables projet.
    /// Le contrôleur conserve l'autorisation et la sauvegarde de fichier (concerns HTTP) ;
    /// ce service possède la logique de données.
    /// </summary>
    public interface ILivrableProjetService
    {
        /// <summary>Crée un livrable déposé et journalise l'action. Retourne l'Id créé.</summary>
        Task<Guid> DeposerAsync(
            Guid projetId,
            PhaseProjet phase,
            TypeLivrable typeLivrable,
            string nomDocument,
            string cheminRelatif,
            Guid deposeParId,
            string? commentaire,
            string? version);

        /// <summary>
        /// Met à jour commentaire/version d'un livrable. Retourne false si le livrable
        /// est introuvable ou n'appartient pas au projet.
        /// </summary>
        Task<bool> MettreAJourAsync(
            Guid projetId,
            Guid livrableId,
            string? commentaire,
            string? version);
    }
}
