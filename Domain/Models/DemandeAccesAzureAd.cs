using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class DemandeAccesAzureAd : EntiteAudit
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenoms { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public string AzureDepartment { get; set; } = string.Empty;

        public Guid? DirectionDetecteeId { get; set; }
        public Direction? DirectionDetectee { get; set; }

        public StatutDemandeAcces Statut { get; set; } = StatutDemandeAcces.EnAttente;
        public string CommentaireTraitement { get; set; } = string.Empty;
        public DateTime? DateTraitement { get; set; }

        public Guid? TraiteParId { get; set; }
        public Utilisateur? TraitePar { get; set; }

        public Guid? UtilisateurCreeId { get; set; }
        public Utilisateur? UtilisateurCree { get; set; }
    }
}
