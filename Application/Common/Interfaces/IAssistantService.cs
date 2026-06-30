using System.Text.Json.Serialization;
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

        /// <summary>
        /// Construit un brouillon de notes de clarification (phase Analyse) à partir
        /// des données déjà saisies sur la demande de projet et la charte.
        /// </summary>
        Task<BrouillonAnalyseResult?> GenererBrouillonAnalyseAsync(Guid projetId, Guid userId);

        /// <summary>
        /// Construit un brouillon de synthèse d'exécution à partir de l'avancement des
        /// tâches de planning, des anomalies et des charges du projet.
        /// </summary>
        Task<BrouillonExecutionResult?> GenererBrouillonExecutionAsync(Guid projetId, Guid userId);
    }

    public sealed class ProchainesEtapesResult
    {
        public required string CodeProjet { get; init; }
        public required string Titre { get; init; }
        public required PhaseProjet PhaseActuelle { get; init; }
        public required string PhaseLabel { get; init; }
        public required string OngletCible { get; init; }
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

    /// <summary>
    /// Noms de propriétés forcés en PascalCase (JsonPropertyName) car le formulaire
    /// d'exécution lie directement les noms de champs sans préfixe camelCase.
    /// </summary>
    public sealed class BrouillonAnalyseResult
    {
        [JsonPropertyName("notesClarification")]
        public required string NotesClarification { get; init; }

        [JsonPropertyName("decisionsPrises")]
        public required string DecisionsPrises { get; init; }

        [JsonPropertyName("hypothesesProjet")]
        public required string HypothesesProjet { get; init; }
    }

    public sealed class BrouillonExecutionResult
    {
        [JsonPropertyName("CommentaireAvancementExecution")]
        public required string CommentaireAvancementExecution { get; init; }

        [JsonPropertyName("ActionsRealiseesExecution")]
        public required string ActionsRealiseesExecution { get; init; }

        [JsonPropertyName("ActionsAVenirExecution")]
        public required string ActionsAVenirExecution { get; init; }

        [JsonPropertyName("ProblemesBlocagesExecution")]
        public required string ProblemesBlocagesExecution { get; init; }
    }
}
