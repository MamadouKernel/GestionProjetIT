using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class SignataireDossierSignatureProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid DossierSignatureProjetId { get; set; }
        public DossierSignatureProjet? DossierSignatureProjet { get; set; }

        public Guid? UtilisateurId { get; set; }
        public Utilisateur? Utilisateur { get; set; }

        public string NomComplet { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public RoleSignataireProjet Role { get; set; }
        public int OrdreSignature { get; set; }
        public StatutSignataireDossierSignature Statut { get; set; }
        public DateTime? DateSignature { get; set; }
    }
}
