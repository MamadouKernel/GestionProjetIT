using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class MembreProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Fonction { get; set; } = string.Empty;

        // Texte libre car peut être externe à CIT
        public string DirectionLibelle { get; set; } = string.Empty;

        public string RoleDansProjet { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public bool EstActif { get; set; } = true;
    }
}
