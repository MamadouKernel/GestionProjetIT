using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class ExecutionTestProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public Guid CasTestProjetId { get; set; }
        public CasTestProjet CasTestProjet { get; set; } = null!;

        public Guid? CampagneTestProjetId { get; set; }
        public CampagneTestProjet? CampagneTestProjet { get; set; }

        public StatutExecutionTest Statut { get; set; } = StatutExecutionTest.AExecuter;
        public string Commentaire { get; set; } = string.Empty;
        public DateTime DateExecution { get; set; }

        public Guid? ExecuteParId { get; set; }
        public Utilisateur? ExecutePar { get; set; }

        public Guid? AnomalieProjetId { get; set; }
        public AnomalieProjet? AnomalieProjet { get; set; }
    }
}
