using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class DemandeClotureProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        public DateTime DateDemande { get; set; }
        public DateTime? DateSouhaiteeCloture { get; set; }

        public Guid DemandeParId { get; set; }        
        public Utilisateur DemandePar { get; set; }

        // Validation Demandeur / métier
        public StatutValidationCloture StatutValidationDemandeur { get; set; }
        public DateTime? DateValidationDemandeur { get; set; }
        public string CommentaireDemandeur { get; set; }

        // Validation Directeur Métier
        public StatutValidationCloture StatutValidationDirecteurMetier { get; set; }
        public DateTime? DateValidationDirecteurMetier { get; set; }
        public string CommentaireDirecteurMetier { get; set; }

        // Validation DSI
        public StatutValidationCloture StatutValidationDSI { get; set; }
        public DateTime? DateValidationDSI { get; set; }
        public string CommentaireDSI { get; set; }

        public bool EstTerminee { get; set; }
        public DateTime? DateClotureFinale { get; set; }
    }
}
