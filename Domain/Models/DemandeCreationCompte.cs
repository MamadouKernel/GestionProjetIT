using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class DemandeCreationCompte : EntiteAudit
    {
        public Guid Id { get; set; }

        public string Nom { get; set; } = string.Empty;
        public string Prenoms { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;

        public Guid? DirectionId { get; set; }
        public Direction? Direction { get; set; }

        public Guid? DirecteurMetierId { get; set; }
        public Utilisateur? DirecteurMetier { get; set; }

        public StatutDemandeCompte Statut { get; set; } = StatutDemandeCompte.EnAttenteValidationDM;

        public string? CommentaireDM { get; set; }
        public string? CommentaireDSI { get; set; }

        public DateTime DateSoumission { get; set; } = DateTime.Now;

        /// <summary>Id de l'utilisateur créé après approbation DSI.</summary>
        public Guid? UtilisateurCreePar { get; set; }
    }
}
