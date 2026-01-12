using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class Projet : EntiteAudit
    {
        public Guid Id { get; set; }

        public string CodeProjet { get; set; } 
        public string Titre { get; set; }

        // Objectif du projet (pour le portefeuille)
        public string? Objectif { get; set; }

        // Lien vers le portefeuille
        public Guid? PortefeuilleProjetId { get; set; }
        public PortefeuilleProjet? PortefeuilleProjet { get; set; }

        public Guid DemandeProjetId { get; set; }
        public DemandeProjet DemandeProjet { get; set; }

        public Guid? DirectionId { get; set; }     
        public Direction Direction { get; set; }

        // Sponsor (Directeur métier)
        public Guid SponsorId { get; set; }
        public Utilisateur Sponsor { get; set; }

        // Chef de projet (DSI)
        public Guid? ChefProjetId { get; set; }
        public Utilisateur ChefProjet { get; set; }

        // Etat du projet
        public StatutProjet StatutProjet { get; set; }
        public PhaseProjet PhaseActuelle { get; set; }
        public int PourcentageAvancement { get; set; }
        public EtatProjet EtatProjet { get; set; }
        
        // Indicateur RAG (calculé automatiquement pour le portefeuille)
        public IndicateurRAG IndicateurRAG { get; set; } = IndicateurRAG.Vert;
        public DateTime? DateDernierCalculRAG { get; set; }

        // Dates
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public DateTime? DateFinReelle { get; set; }

        // Clôture
        public string BilanCloture { get; set; }
        public string LeconsApprises { get; set; }

        // UAT / MEP
        public bool RecetteValidee { get; set; }
        public DateTime? DateRecetteValidee { get; set; }
        public Guid? RecetteValideeParId { get; set; }
        public Utilisateur RecetteValideePar { get; set; }

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
        public ICollection<ChargeProjet> Charges { get; set; } = new List<ChargeProjet>();
        public CharteProjet? CharteProjet { get; set; }
        public FicheProjet? FicheProjet { get; set; }
    }
}
