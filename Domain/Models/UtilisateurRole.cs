using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Table de liaison many-to-many entre Utilisateur et Role
    /// Permet à un utilisateur d'avoir plusieurs rôles (bonne pratique RBAC)
    /// </summary>
    public class UtilisateurRole : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid UtilisateurId { get; set; }
        public Utilisateur Utilisateur { get; set; }

        public RoleUtilisateur Role { get; set; }

        // Optionnel : date de début et fin de validité du rôle
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }

        // Commentaire pour traçabilité
        public string? Commentaire { get; set; }
    }
}

