using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Security;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.ViewComponents
{
    public class SidebarMenuViewComponent : ViewComponent
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<SidebarMenuViewComponent> _logger;

        public SidebarMenuViewComponent(
            IPermissionService permissionService,
            ILogger<SidebarMenuViewComponent> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var permissions = await _permissionService.GetCurrentUserActivePermissionsAsync();
                var permissionSet = new HashSet<string>(
                    permissions.Select(p => $"{p.Controleur}::{p.Action}"),
                    StringComparer.OrdinalIgnoreCase);

                var definitions = PermissionCatalog.GetDefinitions()
                    .Where(d => d.AfficherDansMenu)
                    .Where(d => permissionSet.Contains($"{d.Controleur}::{d.Action}"))
                    .OrderBy(d => PermissionCatalog.GetCategoryOrder(d.Categorie))
                    .ThenBy(d => d.Ordre)
                    .ToList();

                var menuItems = definitions
                    .GroupBy(d => d.Categorie)
                    .OrderBy(g => PermissionCatalog.GetCategoryOrder(g.Key))
                    .Select(g => new MenuCategory
                    {
                        Title = PermissionCatalog.GetMenuSectionTitle(g.Key),
                        Items = g.Select(d => new MenuItem
                        {
                            Title = GetMenuTitle(d, permissionSet),
                            Controller = d.Controleur,
                            Action = d.Action,
                            Icon = d.Icone ?? "bi-file-earmark"
                        }).ToList()
                    })
                    .ToList();

                _logger.LogInformation("SidebarMenuViewComponent: {Count} categories de menu creees", menuItems.Count);
                return View(menuItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans SidebarMenuViewComponent");
                return View(new List<MenuCategory>());
            }
        }

        private static string GetMenuTitle(
            PermissionDefinition definition,
            HashSet<string> permissionSet)
        {
            var hasPortfolioAccess = permissionSet.Contains("Projet::Portefeuille");
            var hasDemandeurClotureAccess = permissionSet.Contains("Projet::ListeValidationClotureDemandeur");
            var hasChefProjetAccess = permissionSet.Contains("Projet::UpdateProgress") || permissionSet.Contains("Projet::ValiderAnalyse");
            var hasDirectionValidationAccess =
                permissionSet.Contains("Projet::ValiderCharteDM") ||
                permissionSet.Contains("Projet::ValiderPlanificationDM") ||
                permissionSet.Contains("Projet::ValiderRecette") ||
                permissionSet.Contains("Projet::ListeValidationClotureDM");

            return (definition.Controleur, definition.Action) switch
            {
                ("Projet", "Index") when !hasPortfolioAccess && hasDirectionValidationAccess && !hasChefProjetAccess && !hasDemandeurClotureAccess => "Projets de ma direction",
                ("Projet", "Index") when hasChefProjetAccess || hasDemandeurClotureAccess => "Mes Projets",
                ("Dashboard", "Index") when !hasPortfolioAccess && hasDirectionValidationAccess => "Analytics direction",
                ("Dashboard", "Index") when hasPortfolioAccess => "Analytics DSI",
                _ => definition.NomAffichage
            };
        }
    }

    public class MenuCategory
    {
        public string Title { get; set; } = string.Empty;
        public List<MenuItem> Items { get; set; } = new();
    }

    public class MenuItem
    {
        public string Title { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
