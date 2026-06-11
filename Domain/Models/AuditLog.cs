using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class AuditLog : EntiteAudit
    {
        public Guid Id { get; set; }

        public DateTime DateAction { get; set; }

        public Guid? UtilisateurId { get; set; }
        public Utilisateur? Utilisateur { get; set; }

        public string TypeAction { get; set; } = string.Empty;      // VALIDATION_DM, CREATION_PROJET, UPLOAD_LIVRABLE...
        public string Entite { get; set; } = string.Empty;          // "DemandeProjet", "Projet", "LivrableProjet", etc.
        public string EntiteId { get; set; } = string.Empty;        // Id de l'entité (en string pour flexibilité)

        public string AnciennesValeurs { get; set; } = string.Empty;   // JSON
        public string NouvellesValeurs { get; set; } = string.Empty;   // JSON
        public string Commentaire { get; set; } = string.Empty;

        public string AdresseIP { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}
