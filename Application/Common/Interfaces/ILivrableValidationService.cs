using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Service de validation des livrables obligatoires pour les changements de phase
    /// </summary>
    public interface ILivrableValidationService
    {
        /// <summary>
        /// Vérifie si tous les livrables obligatoires sont présents pour passer à la phase suivante
        /// </summary>
        /// <param name="projet">Le projet à valider</param>
        /// <param name="phaseCible">La phase cible vers laquelle on souhaite passer</param>
        /// <returns>Résultat de validation avec liste des livrables manquants</returns>
        Task<LivrableValidationResult> ValiderLivrablesObligatoiresAsync(Projet projet, PhaseProjet phaseCible);

        /// <summary>
        /// Obtient la liste des livrables obligatoires pour une transition de phase donnée
        /// </summary>
        /// <param name="phaseActuelle">Phase actuelle du projet</param>
        /// <param name="phaseCible">Phase cible</param>
        /// <returns>Liste des types de livrables obligatoires</returns>
        List<TypeLivrable> GetLivrablesObligatoires(PhaseProjet phaseActuelle, PhaseProjet phaseCible);
    }

    /// <summary>
    /// Résultat de la validation des livrables
    /// </summary>
    public class LivrableValidationResult
    {
        public bool EstValide { get; set; }
        public List<TypeLivrable> LivrablesManquants { get; set; } = new();
        public string MessageErreur { get; set; } = string.Empty;
    }
}

