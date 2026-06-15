using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class Projet : EntiteAudit
    {
        public Guid Id { get; set; }

        public string CodeProjet { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;

        // Objectif du projet (pour le portefeuille)
        public string? Objectif { get; set; }

        // Lien vers le portefeuille
        public Guid? PortefeuilleProjetId { get; set; }
        public PortefeuilleProjet? PortefeuilleProjet { get; set; }

        public Guid DemandeProjetId { get; set; }
        public DemandeProjet DemandeProjet { get; set; } = null!;

        public Guid? DirectionId { get; set; }     
        public Direction? Direction { get; set; }

        // Sponsor (Directeur métier)
        public Guid SponsorId { get; set; }
        public Utilisateur Sponsor { get; set; } = null!;

        // Chef de projet (DSI)
        public Guid? ChefProjetId { get; set; }
        public Utilisateur? ChefProjet { get; set; }

        // Etat du projet
        public StatutProjet StatutProjet { get; set; }
        public PhaseProjet PhaseActuelle { get; set; }
        public int PourcentageAvancement { get; set; }
        public EtatProjet EtatProjet { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int PourcentageAvancementAffiche => StatutProjet switch
        {
            StatutProjet.NonDemarre => 0,
            StatutProjet.Cloture => 100,
            StatutProjet.Annule => Math.Clamp(PourcentageAvancement, 0, 99),
            _ => Math.Clamp(PourcentageAvancement, 0, 100)
        };
        
        // Indicateur RAG (calculé automatiquement pour le portefeuille)
        public IndicateurRAG IndicateurRAG { get; set; } = IndicateurRAG.Vert;
        public DateTime? DateDernierCalculRAG { get; set; }

        // Dates
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }

        // Clôture — champs structurés (remplacent les anciens champs texte libres)
        public string? BilanCloture { get; set; }       // conservé pour rétro-compatibilité
        public string? LeconsApprises { get; set; }     // conservé pour rétro-compatibilité

        public string? BilanPerimetre { get; set; }
        public string? BilanPlanning { get; set; }
        public string? BilanBudget { get; set; }
        public string? BilanDifficultes { get; set; }
        public string? BilanReussites { get; set; }

        public string? LeconsReussites { get; set; }
        public string? LeconsEchecs { get; set; }
        public string? LeconsRecommandations { get; set; }

        // UAT / MEP
        public bool RecetteValidee { get; set; }
        public DateTime? DateRecetteValidee { get; set; }
        public Guid? RecetteValideeParId { get; set; }
        public Utilisateur? RecetteValideePar { get; set; }

        public bool MepEffectuee { get; set; }
        public DateTime? DateMep { get; set; }

        // Charte / Planification
        public bool CharteValidee { get; set; }
        public DateTime? DateCharteValidee { get; set; }
        public bool CharteValideeParDM { get; set; }
        public DateTime? DateCharteValideeParDM { get; set; }
        public Guid? CharteValideeParDMId { get; set; }
        public Utilisateur? CharteValideeParDMUtilisateur { get; set; }
        public bool CharteValideeParDSI { get; set; }
        public DateTime? DateCharteValideeParDSI { get; set; }
        public Guid? CharteValideeParDSIId { get; set; }
        public Utilisateur? CharteValideeParDSIUtilisateur { get; set; }
        public string? CommentaireRefusCharteDM { get; set; }
        public string? CommentaireRefusCharteDSI { get; set; }

        public bool PlanningValideParDSI { get; set; }
        public DateTime? DatePlanningValideParDSI { get; set; }

        public bool PlanningValideParDM { get; set; }
        public DateTime? DatePlanningValideParDM { get; set; }

        // Commentaires techniques (Responsable Solutions IT)
        public string CommentaireTechnique { get; set; } = string.Empty;
        public DateTime? DateDernierCommentaireTechnique { get; set; }
        public Guid? DernierCommentaireTechniqueParId { get; set; }
        public Utilisateur? DernierCommentaireTechniquePar { get; set; }

        // Collections liées
        public ICollection<MembreProjet> Membres { get; set; } = new List<MembreProjet>();
        public ICollection<RisqueProjet> Risques { get; set; } = new List<RisqueProjet>();
        public ICollection<LivrableProjet> Livrables { get; set; } = new List<LivrableProjet>();
        public ICollection<AnomalieProjet> Anomalies { get; set; } = new List<AnomalieProjet>();
        public ICollection<HistoriquePhaseProjet> HistoriquePhases { get; set; } = new List<HistoriquePhaseProjet>();
        public ICollection<DemandeClotureProjet> DemandesCloture { get; set; } = new List<DemandeClotureProjet>();
        public ICollection<AvenantProjet> Avenants { get; set; } = new List<AvenantProjet>();
        public ICollection<ChargeProjet> Charges { get; set; } = new List<ChargeProjet>();
        public ICollection<TachePlanningProjet> TachesPlanning { get; set; } = new List<TachePlanningProjet>();
        public ICollection<LigneRaciProjet> LignesRaci { get; set; } = new List<LigneRaciProjet>();
        public ICollection<LigneCommunicationProjet> LignesCommunication { get; set; } = new List<LigneCommunicationProjet>();
        public ICollection<LigneBudgetPlanificationProjet> LignesBudgetPlanification { get; set; } = new List<LigneBudgetPlanificationProjet>();
        public PvKickOffProjet? PvKickOff { get; set; }
        public CharteProjet? CharteProjet { get; set; }
        public FicheProjet? FicheProjet { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string PhaseWorkflowLabel => PhaseActuelle switch
        {
            PhaseProjet.Demande => "Demande",
            PhaseProjet.AnalyseClarification => "Analyse & Clarification",
            PhaseProjet.PlanificationValidation => "Planification & Validation",
            PhaseProjet.ExecutionSuivi => "Exécution & Suivi",
            PhaseProjet.UatMep => "UAT & MEP",
            PhaseProjet.ClotureLeconsApprises => "Clôture & Leçons",
            _ => PhaseActuelle.ToString()
        };

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string StatutWorkflowLabel => StatutProjet switch
        {
            StatutProjet.NonDemarre => "Non démarré",
            StatutProjet.EnCours => "En cours",
            StatutProjet.Suspendu => "Suspendu",
            StatutProjet.ClotureEnCours => "Clôture en cours",
            StatutProjet.Cloture => "Clôturé",
            StatutProjet.Annule => "Annulé",
            _ => StatutProjet.ToString()
        };
    }
}
