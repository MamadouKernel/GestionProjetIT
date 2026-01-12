using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class MembreProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Fonction { get; set; }

        // Texte libre car peut être externe à CIT
        public string DirectionLibelle { get; set; }

        public string RoleDansProjet { get; set; }  
        public string Email { get; set; }

        public bool EstActif { get; set; } = true;
    }
}
