using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin
{
    public class DelegationsPageViewModel
    {
        public List<DelegationValidationDSI> DelegationsDSI { get; set; } = new();
        public List<DelegationChefProjet> DelegationsChefProjet { get; set; } = new();
        public List<DelegationValidationDM> DelegationsDM { get; set; } = new();
        public List<Utilisateur> DSIs { get; set; } = new();
        public List<Utilisateur> DeleguesDSI { get; set; } = new();
        public List<Utilisateur> Delegants { get; set; } = new();
        public List<Utilisateur> DeleguesChefProjet { get; set; } = new();
        public List<Utilisateur> DirecteursMetier { get; set; } = new();
        public List<Utilisateur> DeleguesDM { get; set; } = new();
        public List<GestionProjects.Domain.Models.Projet> Projets { get; set; } = new();
        public string ActiveTab { get; set; } = "dsi";
        public bool CanAdminDelegations { get; set; }
        public Guid CurrentUserId { get; set; }

        // Pagination DSI
        public int PageNumberDsi { get; set; }
        public int TotalPagesDsi { get; set; }
        public int TotalCountDsi { get; set; }
        public int PageSizeDsi { get; set; }
        public string? RechercheDsi { get; set; }

        // Pagination Chef
        public int PageNumberChef { get; set; }
        public int TotalPagesChef { get; set; }
        public int TotalCountChef { get; set; }
        public int PageSizeChef { get; set; }
        public string? RechercheChef { get; set; }

        // Pagination DM
        public int PageNumberDm { get; set; }
        public int TotalPagesDm { get; set; }
        public int TotalCountDm { get; set; }
        public int PageSizeDm { get; set; }
        public string? RechercheDm { get; set; }
    }
}
