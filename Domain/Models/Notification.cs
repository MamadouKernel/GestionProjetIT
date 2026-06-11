using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class Notification : EntiteAudit
    {
        public Guid Id { get; set; }

        // Destinataire
        public Guid UtilisateurId { get; set; }
        public Utilisateur Utilisateur { get; set; } = null!;

        // Type de notification
        public TypeNotification TypeNotification { get; set; }

        // Titre et message
        public string Titre { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        // Lien vers l'entité concernée
        public string? EntiteType { get; set; } // "Projet", "DemandeProjet", etc.
        public Guid? EntiteId { get; set; }

        // Statut
        public bool EstLue { get; set; }
        public DateTime? DateLecture { get; set; }

        // Données supplémentaires (JSON)
        public string? DonneesSupplementaires { get; set; }
    }
}

