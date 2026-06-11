using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Helpers;
using System.ComponentModel.DataAnnotations.Schema;
namespace GestionProjects.Domain.Models
{
    public class DemandeProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public string? Titre { get; set; }
        public string? Description { get; set; }
        public string? Contexte { get; set; }
        public string? Objectifs { get; set; }
        public string? AvantagesAttendus { get; set; }
        public string? Perimetre { get; set; } // Processus impactes, population concernee

        public UrgenceProjet Urgence { get; set; }
        public CriticiteProjet Criticite { get; set; }

        public DateTime? DateMiseEnOeuvreSouhaitee { get; set; }

        public Guid DemandeurId { get; set; }
        public Utilisateur Demandeur { get; set; } = null!;

        public Guid? DirectionId { get; set; }

        public Direction? Direction { get; set; }

        public Guid DirecteurMetierId { get; set; }
        public Utilisateur DirecteurMetier { get; set; } = null!;

        public Guid? AutreSponsorId { get; set; }
        public Utilisateur? AutreSponsor { get; set; }

        public StatutDemande StatutDemande { get; set; }
        public DateTime DateSoumission { get; set; }

        public DateTime? DateValidationDM { get; set; }
        public DateTime? DateValidationDSI { get; set; }

        public string? CommentaireDirecteurMetier { get; set; }
        public string? CommentaireDSI { get; set; }

        public string? CahierChargesPath { get; set; }

        [NotMapped]
        public DocumentJointDemande? CahierChargesDocument => Annexes
            .FirstOrDefault(a => !a.EstSupprime &&
                                 !string.IsNullOrWhiteSpace(CahierChargesPath) &&
                                 string.Equals(a.CheminRelatif, CahierChargesPath, StringComparison.OrdinalIgnoreCase));

        [NotMapped]
        public IEnumerable<DocumentJointDemande> DocumentsAnnexes => Annexes
            .Where(a => !a.EstSupprime &&
                        (string.IsNullOrWhiteSpace(CahierChargesPath) ||
                         !string.Equals(a.CheminRelatif, CahierChargesPath, StringComparison.OrdinalIgnoreCase)));

        [NotMapped]
        public int PrioriteScore => PrioriteDemandeHelper.CalculateScore(Urgence, Criticite);

        [NotMapped]
        public string PrioriteCode => PrioriteDemandeHelper.GetPrioriteCode(Urgence, Criticite);

        [NotMapped]
        public string PrioriteLibelle => PrioriteDemandeHelper.GetPrioriteLibelle(Urgence, Criticite);

        [NotMapped]
        public string PrioriteBadgeClass => PrioriteDemandeHelper.GetPrioriteBadgeClass(Urgence, Criticite);

        [NotMapped]
        public string StatutWorkflowLabel => StatutDemande switch
        {
            StatutDemande.Brouillon => "Brouillon",
            StatutDemande.EnAttenteValidationDirecteurMetier => "En attente validation Directeur Métier",
            StatutDemande.CorrectionDemandeeParDirecteurMetier => "Correction demandée par le Directeur Métier",
            StatutDemande.RejeteeParDirecteurMetier => "Rejetée par le Directeur Métier",
            StatutDemande.EnAttenteValidationDSI => "En attente validation DSI",
            StatutDemande.RetourneeAuDemandeurParDSI => "Correction demandée par la DSI (Demandeur)",
            StatutDemande.RetourneeAuDirecteurMetierParDSI => "Correction demandée par la DSI (Directeur Métier)",
            StatutDemande.RejeteeParDSI => "Rejetée par la DSI",
            StatutDemande.ValideeParDSI => "Validée par la DSI",
            _ => StatutDemande.ToString()
        };

        public ICollection<DocumentJointDemande> Annexes { get; set; } = new List<DocumentJointDemande>();

        public Projet? Projet { get; set; }
    }
}
