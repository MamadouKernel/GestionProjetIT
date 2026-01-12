using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class AnomalieProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet? Projet { get; set; }

        public string Reference { get; set; } = string.Empty;   
        public string Description { get; set; } = string.Empty;

        public PrioriteAnomalie Priorite { get; set; }
        public StatutAnomalie Statut { get; set; }

        public Environnement Environnement { get; set; }
        public string ModuleConcerne { get; set; } = string.Empty;

        public string RapporteePar { get; set; } = string.Empty;   
        public DateTime DateCreationAnomalie { get; set; }

        public string AssigneeA { get; set; } = string.Empty;    
        public DateTime? DateResolution { get; set; }
        public string CommentaireResolution { get; set; } = string.Empty;
    }
}
