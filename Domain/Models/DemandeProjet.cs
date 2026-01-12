using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;
using Microsoft.Identity.Client;

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
        public string? Perimetre { get; set; } // Processus impactés, population concernée, budget estimé

        public UrgenceProjet Urgence { get; set; }
        public CriticiteProjet Criticite { get; set; }

        public DateTime? DateMiseEnOeuvreSouhaitee { get; set; }

        public Guid DemandeurId { get; set; }
        public Utilisateur Demandeur { get; set; }

        public Guid? DirectionId { get; set; }

        public Direction Direction { get; set; }

        public Guid DirecteurMetierId { get; set; }
        public Utilisateur DirecteurMetier { get; set; }

        public StatutDemande StatutDemande { get; set; }
        public DateTime DateSoumission { get; set; }

        public DateTime? DateValidationDM { get; set; }
        public DateTime? DateValidationDSI { get; set; }

        public string? CommentaireDirecteurMetier { get; set; }
        public string? CommentaireDSI { get; set; }

        public string? CahierChargesPath { get; set; }

        public ICollection<DocumentJointDemande> Annexes { get; set; } = new List<DocumentJointDemande>();

        public Projet Projet { get; set; }
    }
}
