using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class HistoriqueChefProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }
        
        public Guid ChefProjetId { get; set; }
        public Utilisateur ChefProjet { get; set; }
        
        public DateTime DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        
        public string Commentaire { get; set; } = string.Empty;
    }
}

