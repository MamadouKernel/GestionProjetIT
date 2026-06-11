using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    public interface ICollaborationProjetService
    {
        Task<CollaborationProjet> ConfigurerAsync(Guid projetId, CollaborationProjetConfigurationRequest request, string? currentUserMatricule);
        Task<CollaborationProjetSyncResult> SynchroniserAsync(Guid projetId, string? currentUserMatricule);
    }

    public class CollaborationProjetConfigurationRequest
    {
        public ModeCollaborationProjet Mode { get; set; }
        public string? NomEquipeTeams { get; set; }
        public string? TeamId { get; set; }
        public string? TeamUrl { get; set; }
        public string? NomCanalTeams { get; set; }
        public string? ChannelId { get; set; }
        public string? ChannelUrl { get; set; }
        public string? NomPlanPlanner { get; set; }
        public string? PlanId { get; set; }
        public string? PlanUrl { get; set; }
        public string? NomBucketPlanner { get; set; }
        public string? BucketId { get; set; }
    }

    public class CollaborationProjetSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int NombreMembresSynchronises { get; set; }
        public int NombreTachesSynchronisees { get; set; }
        public int NombreMembres => NombreMembresSynchronises;
        public int NombreTaches => NombreTachesSynchronisees;
        public CollaborationProjet? Collaboration { get; set; }
    }
}
