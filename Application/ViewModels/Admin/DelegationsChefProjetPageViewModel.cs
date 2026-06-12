using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin
{
    public class DelegationsChefProjetPageViewModel
    {
        public List<DelegationChefProjet> Delegations { get; set; } = new();
        public List<GestionProjects.Domain.Models.Projet> Projets { get; set; } = new();
        public List<Utilisateur> Delegants { get; set; } = new();
        public List<Utilisateur> Delegues { get; set; } = new();
        public Guid CurrentUserId { get; set; }
        public bool CanAdminDelegations { get; set; }
    }
}
