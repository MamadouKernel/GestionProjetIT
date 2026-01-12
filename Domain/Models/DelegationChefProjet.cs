using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class DelegationChefProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        public Guid DelegantId { get; set; }
        public Utilisateur? Delegant { get; set; }

        public Guid DelegueId { get; set; }
        public Utilisateur? Delegue { get; set; }

        public DateTime DateDebut { get; set; }
        // DateFin sera automatiquement mise à jour lors de la clôture du projet
        public DateTime? DateFin { get; set; }

        public bool EstActive { get; set; }
    }
}

