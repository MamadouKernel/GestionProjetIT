using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Security;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.ViewComponents
{
    public class SidebarMenuViewComponent : ViewComponent
    {
        private readonly IPermissionService _permissionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SidebarMenuViewComponent> _logger;

        public SidebarMenuViewComponent(
            IPermissionService permissionService,
            ICurrentUserService currentUserService,
            ApplicationDbContext db,
            ILogger<SidebarMenuViewComponent> logger)
        {
            _permissionService = permissionService;
            _currentUserService = currentUserService;
            _db = db;
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

                // Compteurs de badges (calcules une fois pour toutes les entrees concernees).
                var badges = await CalculerBadgesAsync(permissionSet);

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
                            Icon = d.Icone ?? "bi-file-earmark",
                            Badge = badges.TryGetValue($"{d.Controleur}::{d.Action}", out var n) && n > 0 ? n : (int?)null
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

        /// <summary>
        /// Compteurs d'actions en attente affiches en badge sur les entrees de menu.
        /// Une seule passe par requete (le menu est rendu a chaque page) -> garder LEGER.
        /// </summary>
        private async Task<Dictionary<string, int>> CalculerBadgesAsync(HashSet<string> permissionSet)
        {
            var badges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Badge : demandes d'acces en attente de validation DM (pour le DM connecte).
            if (permissionSet.Contains("DemandesAcces::ValidationsDm"))
            {
                var userIdClaim = ((ViewComponentContext)ViewComponentContext)
                    .ViewContext.HttpContext.User
                    .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    var directionsCouvertes = await _db.Utilisateurs
                        .Where(u => u.Id == userId && !u.EstSupprime &&
                                    u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                        .Select(u => u.DirectionId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToListAsync();

                    if (directionsCouvertes.Count > 0)
                    {
                        var nb = await _db.DemandesAccesAzureAd
                            .CountAsync(d => !d.EstSupprime &&
                                             d.Statut == StatutDemandeAcces.EnAttente &&
                                             d.DirectionDetecteeId.HasValue &&
                                             directionsCouvertes.Contains(d.DirectionDetecteeId.Value));
                        if (nb > 0) badges["DemandesAcces::ValidationsDm"] = nb;
                    }
                }
            }

            return badges;
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
        /// <summary>
        /// Compteur d'actions en attente sur cette entrée (badge). Null = pas de badge.
        /// </summary>
        public int? Badge { get; set; }
    }
}
