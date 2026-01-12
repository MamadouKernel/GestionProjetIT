using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class HistoriquePhaseProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        public PhaseProjet Phase { get; set; }
        public StatutProjet StatutProjet { get; set; }

        public DateTime DateDebut { get; set; }
        public DateTime? DateFin { get; set; }

        public Guid? ModifieParId { get; set; }
        public Utilisateur ModifieParUtilisateur { get; set; }

        public string Commentaire { get; set; }
    }
}
