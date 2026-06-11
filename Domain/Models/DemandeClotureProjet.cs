using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class DemandeClotureProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public DateTime DateDemande { get; set; }
        public DateTime? DateSouhaiteeCloture { get; set; }

        public Guid DemandeParId { get; set; }        
        public Utilisateur DemandePar { get; set; } = null!;

        public string CommentaireInitiateur { get; set; } = string.Empty;

        // Validation Demandeur / métier
        public StatutValidationCloture StatutValidationDemandeur { get; set; }
        public DateTime? DateValidationDemandeur { get; set; }
        public string CommentaireDemandeur { get; set; } = string.Empty;

        // Validation Directeur Métier
        public StatutValidationCloture StatutValidationDirecteurMetier { get; set; }
        public DateTime? DateValidationDirecteurMetier { get; set; }
        public string CommentaireDirecteurMetier { get; set; } = string.Empty;

        // Validation DSI
        public StatutValidationCloture StatutValidationDSI { get; set; }
        public DateTime? DateValidationDSI { get; set; }
        public string CommentaireDSI { get; set; } = string.Empty;

        public bool EstTerminee { get; set; }
        public DateTime? DateClotureFinale { get; set; }
    }
}
