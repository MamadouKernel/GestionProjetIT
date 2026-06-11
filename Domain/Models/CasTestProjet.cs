using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class CasTestProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public Guid? CampagneTestProjetId { get; set; }
        public CampagneTestProjet? CampagneTestProjet { get; set; }

        public string Reference { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ResultatAttendu { get; set; } = string.Empty;
        public PrioriteAnomalie Priorite { get; set; } = PrioriteAnomalie.Moyenne;
        public bool EstObligatoire { get; set; } = true;

        public ICollection<ExecutionTestProjet> Executions { get; set; } = new List<ExecutionTestProjet>();
    }
}
