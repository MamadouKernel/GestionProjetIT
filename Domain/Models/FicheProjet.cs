using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Fiche projet structurée utilisée comme socle de pilotage transverse.
    /// </summary>
    public class FicheProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        // Identification
        public string? TitreCourt { get; set; }
        public string? TitreLong { get; set; }

        // Objectifs & description
        public string? ObjectifPrincipal { get; set; }
        public string? ContexteProblemeAdresse { get; set; }
        public string? DescriptionSynthetique { get; set; }
        public string? ResultatsAttendus { get; set; }

        // Analyse
        public string? NotesClarification { get; set; }
        public string? DecisionsPrises { get; set; }
        public string? HypothesesProjet { get; set; }

        // Périmètre
        public string? PerimetreInclus { get; set; }
        public string? PerimetreExclu { get; set; }

        // Indicateurs clés
        public string? BeneficesAttendus { get; set; }
        public string? CriticiteUrgence { get; set; }
        public string? TypeProjet { get; set; }

        // Planification
        public string? ProchainJalon { get; set; }
        public string? JalonsPrincipaux { get; set; }
        public string? DecoupageLotsTravail { get; set; }
        public string? PlanificationRessources { get; set; }
        public string? RaciParActivite { get; set; }
        public string? FrequenceReunions { get; set; }
        public string? ParticipantsReunions { get; set; }
        public string? CanalCommunication { get; set; }
        public bool CopilPrevu { get; set; }
        public string? CommentaireBudgetPlanification { get; set; }
        public string? CommentaireValidationPlanification { get; set; }

        // Risques & gouvernance
        public string? SyntheseRisques { get; set; }
        public string? EquipeProjet { get; set; }
        public string? PartiesPrenantesCles { get; set; }

        // Livrables synthétiques
        public bool CharteProjetPresente { get; set; }
        public bool WBSPlanningRACIBudgetPresent { get; set; }
        public bool CRReunionsPresent { get; set; }
        public bool CahierTestsPVRecettePVMEPPresent { get; set; }
        public bool RapportLeconsApprisesPVCloturePresent { get; set; }

        // Budget
        public decimal? BudgetPrevisionnel { get; set; }
        public decimal? BudgetConsomme { get; set; }
        public decimal? EcartsBudget { get; set; }
        public string? JustificationEcartBudget { get; set; }
        public DateTime? DateJustificationEcart { get; set; }
        public Guid? JustificationParId { get; set; }
        public Utilisateur? JustificationPar { get; set; }

        // Exécution & suivi
        public DateTime? DateDebutReelleExecution { get; set; }
        public DateTime? DateFinEstimeeExecution { get; set; }
        public string? JustificationRetardExecution { get; set; }
        public string? CommentaireAvancementExecution { get; set; }
        public string? ActionsRealiseesExecution { get; set; }
        public string? ActionsAVenirExecution { get; set; }
        public string? ProblemesBlocagesExecution { get; set; }
        public string? JustificationBudgetExecution { get; set; }
        public string? SyntheseChargesExecution { get; set; }
        public string? DecisionsExecution { get; set; }

        // UAT & MEP
        public DateTime? DateDebutRecette { get; set; }
        public DateTime? DateFinRecette { get; set; }
        public string? UtilisateursTesteurs { get; set; }
        public string? PerimetreTeste { get; set; }
        public DateTime? DateMepPrevue { get; set; }
        public string? PrerequisMep { get; set; }
        public string? PlanMep { get; set; }
        public string? PlanRollback { get; set; }
        public bool ChangeRequis { get; set; }
        public string? ReferenceChange { get; set; }
        public string? StatutValidationChange { get; set; }
        public string? ResultatMep { get; set; }
        public string? IncidentsMep { get; set; }
        public string? PeriodeHypercare { get; set; }
        public string? IncidentsPostMep { get; set; }
        public string? StatutHypercare { get; set; }
        public bool HypercareTermine { get; set; }

        // Clôture
        public bool TransfertRunDocumentation { get; set; }
        public bool TransfertRunAcces { get; set; }
        public bool TransfertRunSupportInforme { get; set; }
        public bool TransfertRunExploitationPrete { get; set; }
        public string? StatutFinalCloture { get; set; }
        public string? CommentaireStatutFinal { get; set; }

        // Synthèse DSI
        public string? PointsForts { get; set; }
        public string? PointsVigilance { get; set; }
        public string? DecisionsAttendues { get; set; }
        public string? DemandesArbitrage { get; set; }

        // Métadonnées
        public DateTime? DateDerniereMiseAJour { get; set; }
        public Guid? DerniereMiseAJourParId { get; set; }
        public Utilisateur? DerniereMiseAJourPar { get; set; }
    }
}
