using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class RisqueProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        public ProbabiliteRisque Probabilite { get; set; }
        public ImpactRisque Impact { get; set; }

        public string? PlanMitigation { get; set; }
        public string? Responsable { get; set; }

        public StatutRisque Statut { get; set; }
        public DateTime DateCreationRisque { get; set; }
        public DateTime? DateCloture { get; set; }
    }
}
