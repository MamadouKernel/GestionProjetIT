using GestionProjects.Application.Common.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Service de requêtes pour les projets : filtrage, scope par rôle, listes de référence.
    /// Extrait du ProjetController pour centraliser la logique de requêtage.
    /// </summary>
    public interface IProjetQueryService
    {
        /// <summary>
        /// Détermine la direction de l'utilisateur courant.
        /// </summary>
        Task<Guid?> GetUserDirectionIdAsync(Guid userId);

        /// <summary>
        /// Applique le filtre de scope (périmètre visible) sur une requête de projets
        /// selon les droits de l'utilisateur courant.
        /// </summary>
        Task<IQueryable<Projet>> AppliquerScopeAsync(
            IQueryable<Projet> query,
            Guid userId,
            bool canPortfolioAccess,
            bool hasChefProjetScope,
            bool hasDmScope,
            bool hasDemandeurScope,
            Guid? currentUserDirectionId);

        /// <summary>
        /// Charge les listes de référence pour les filtres de la vue Index (directions, chefs, phases...).
        /// </summary>
        Task<ProjetFiltresViewModel> ChargerFiltresAsync(
            Guid? directionId,
            Guid? chefProjetId,
            PhaseProjet? phase,
            StatutProjet? statut,
            EtatProjet? etat);
    }

    /// <summary>DTO léger pour les filtres de la liste projets.</summary>
    public class ProjetFiltresViewModel
    {
        public List<SelectOption> Directions { get; init; } = new();
        public List<SelectOption> ChefsProjet { get; init; } = new();
        public List<SelectOption> Phases { get; init; } = new();
        public List<SelectOption> Statuts { get; init; } = new();
        public List<SelectOption> Etats { get; init; } = new();
    }
}
