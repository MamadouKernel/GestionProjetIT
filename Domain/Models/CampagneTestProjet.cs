using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class CampagneTestProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public string Nom { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Environnement Environnement { get; set; } = Environnement.Recette;
        public StatutCampagneTest Statut { get; set; } = StatutCampagneTest.Brouillon;
        public DateTime DateLancement { get; set; }
        public DateTime? DateCloture { get; set; }

        public ICollection<CasTestProjet> CasTests { get; set; } = new List<CasTestProjet>();
        public ICollection<ExecutionTestProjet> Executions { get; set; } = new List<ExecutionTestProjet>();
    }
}
