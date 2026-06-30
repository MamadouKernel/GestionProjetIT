using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Assistant scripté (sans LLM externe) : aide contextuelle sur l'état d'un projet
    /// et génération de brouillons de bilan à partir des données réelles du projet.
    /// </summary>
    public interface IAssistantService
    {
        /// <summary>
        /// Calcule la phase actuelle, les étapes déjà validées et ce qui bloque encore
        /// le passage à la phase suivante (livrables manquants, validations en attente).
        /// </summary>
        Task<ProchainesEtapesResult?> ObtenirProchainesEtapesAsync(Guid projetId, Guid userId);

        /// <summary>
        /// Construit un brouillon de bilan de clôture à partir des données réelles du
        /// projet (dates, anomalies, charges). Le texte est généré par des templates
        /// déterministes, pas par génération libre.
        /// </summary>
        Task<BrouillonBilanResult?> GenererBrouillonBilanAsync(Guid projetId, Guid userId);
    }

    public sealed class ProchainesEtapesResult
    {
        public required string CodeProjet { get; init; }
        public required string Titre { get; init; }
        public required PhaseProjet PhaseActuelle { get; init; }
        public required string PhaseLabel { get; init; }
        public required bool EstCloture { get; init; }
        public required IReadOnlyList<string> ElementsManquants { get; init; }
        public required string ProchaineAction { get; init; }
    }

    public sealed class BrouillonBilanResult
    {
        public required string BilanPerimetre { get; init; }
        public required string BilanPlanning { get; init; }
        public required string BilanBudget { get; init; }
        public required string BilanDifficultes { get; init; }
        public required string BilanReussites { get; init; }
        public required string LeconsReussites { get; init; }
        public required string LeconsEchecs { get; init; }
    }
}
