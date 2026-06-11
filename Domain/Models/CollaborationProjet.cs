using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class CollaborationProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet? Projet { get; set; }

        public ModeCollaborationProjet Mode { get; set; }
        public StatutCollaborationProjet Statut { get; set; }

        public string NomEquipeTeams { get; set; } = string.Empty;
        public string? TeamId { get; set; }
        public string? TeamUrl { get; set; }

        public string NomCanalTeams { get; set; } = string.Empty;
        public string? ChannelId { get; set; }
        public string? ChannelUrl { get; set; }

        public string NomPlanPlanner { get; set; } = string.Empty;
        public string? PlanId { get; set; }
        public string? PlanUrl { get; set; }

        public string? NomBucketPlanner { get; set; }
        public string? BucketId { get; set; }

        public DateTime? DateProvisioning { get; set; }
        public DateTime? DerniereSynchronisationEquipe { get; set; }
        public int NombreMembresSynchronises { get; set; }
        public string MessageStatut { get; set; } = string.Empty;

        public ICollection<TacheCollaborationProjet> Taches { get; set; } = new List<TacheCollaborationProjet>();
    }
}
