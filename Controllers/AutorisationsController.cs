using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class AutorisationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AutorisationsController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IPermissionService _permissionService;

        public AutorisationsController(
            ApplicationDbContext context,
            ILogger<AutorisationsController> logger,
            IMemoryCache cache,
            IPermissionService permissionService)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _permissionService = permissionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                if (!await CanManagePermissionsAsync())
                {
                    return Forbid();
                }

                var vuesDisponibles = ObtenirVuesDisponibles();
                var roles = Enum.GetValues<RoleUtilisateur>()
                    .OrderBy(r => r.ToString())
                    .ToList();

                var permissionsPersisted = await _context.RolePermissions
                    .OrderBy(rp => rp.Role)
                    .ThenBy(rp => rp.Categorie)
                    .ThenBy(rp => rp.Ordre)
                    .ToListAsync();

                var permissionsParRole = new Dictionary<RoleUtilisateur, List<RolePermission>>();

                foreach (var role in roles)
                {
                    var permissionsDuRole = new List<RolePermission>();

                    foreach (var vue in vuesDisponibles)
                    {
                        var permissionPersisted = permissionsPersisted.FirstOrDefault(p =>
                            p.Role == role &&
                            p.Controleur == vue.Controleur &&
                            p.Action == vue.Action);

                        permissionsDuRole.Add(new RolePermission
                        {
                            Id = permissionPersisted?.Id ?? Guid.Empty,
                            Role = role,
                            Controleur = vue.Controleur,
                            Action = vue.Action,
                            NomAffichage = vue.NomAffichage,
                            Description = vue.Description,
                            Categorie = vue.Categorie,
                            Icone = vue.Icone,
                            Ordre = vue.Ordre,
                            EstActif = permissionPersisted?.EstActif ?? (role == RoleUtilisateur.AdminIT
                                || PermissionCatalog.IsEnabledByDefault(role, vue.Controleur, vue.Action))
                        });
                    }

                    permissionsParRole[role] = permissionsDuRole
                        .OrderBy(p => PermissionCatalog.GetCategoryOrder(p.Categorie))
                        .ThenBy(p => p.Ordre)
                        .ThenBy(p => p.NomAffichage)
                        .ToList();
                }

                ViewBag.Roles = roles;
                ViewBag.PermissionsParRole = permissionsParRole;
                ViewBag.VuesDisponibles = vuesDisponibles;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des autorisations");
                TempData["Error"] = "Erreur lors du chargement des autorisations.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePermission(
            RoleUtilisateur role,
            string controleur,
            string action,
            bool estActif)
        {
            try
            {
                if (!await CanManagePermissionsAsync())
                {
                    return Json(new { success = false, message = "Accès refusé." });
                }

                if (role == RoleUtilisateur.AdminIT)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Le role Admin IT conserve un acces global et n'est pas restreignable depuis cette matrice."
                    });
                }

                var permission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp =>
                        rp.Role == role &&
                        rp.Controleur == controleur &&
                        rp.Action == action);

                if (permission == null)
                {
                    var vueInfo = ObtenirInfoVue(controleur, action);
                    permission = new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        Role = role,
                        Controleur = controleur,
                        Action = action,
                        NomAffichage = vueInfo.NomAffichage,
                        Description = vueInfo.Description,
                        Categorie = vueInfo.Categorie,
                        Icone = vueInfo.Icone,
                        Ordre = vueInfo.Ordre,
                        EstActif = estActif
                    };

                    _context.RolePermissions.Add(permission);
                }
                else
                {
                    var vueInfo = ObtenirInfoVue(controleur, action);
                    permission.EstActif = estActif;
                    permission.NomAffichage = vueInfo.NomAffichage;
                    permission.Description = vueInfo.Description;
                    permission.Categorie = vueInfo.Categorie;
                    permission.Icone = vueInfo.Icone;
                    permission.Ordre = vueInfo.Ordre;
                    _context.Update(permission);
                }

                await _context.SaveChangesAsync();
                ViderCachePermissions(role);

                return Json(new
                {
                    success = true,
                    message = "Permission mise a jour avec succes. Les changements sont pris en compte au prochain chargement de page."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise a jour de la permission");
                return Json(new { success = false, message = "Erreur lors de la mise a jour" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitialiserPermissions()
        {
            try
            {
                if (!await CanManagePermissionsAsync())
                {
                    return Forbid();
                }

                var vuesDisponibles = ObtenirVuesDisponibles();
                var roles = Enum.GetValues<RoleUtilisateur>();
                var permissionsPersisted = await _context.RolePermissions.ToListAsync();

                foreach (var role in roles)
                {
                    foreach (var vue in vuesDisponibles)
                    {
                        var permission = permissionsPersisted.FirstOrDefault(rp =>
                            rp.Role == role &&
                            rp.Controleur == vue.Controleur &&
                            rp.Action == vue.Action);

                        var estActifParDefaut = role == RoleUtilisateur.AdminIT
                            || PermissionCatalog.IsEnabledByDefault(role, vue.Controleur, vue.Action);

                        if (permission == null)
                        {
                            permission = new RolePermission
                            {
                                Id = Guid.NewGuid(),
                                Role = role,
                                Controleur = vue.Controleur,
                                Action = vue.Action,
                                NomAffichage = vue.NomAffichage,
                                Description = vue.Description,
                                Categorie = vue.Categorie,
                                Icone = vue.Icone,
                                Ordre = vue.Ordre,
                                EstActif = estActifParDefaut
                            };
                            _context.RolePermissions.Add(permission);
                        }
                        else
                        {
                            permission.NomAffichage = vue.NomAffichage;
                            permission.Description = vue.Description;
                            permission.Categorie = vue.Categorie;
                            permission.Icone = vue.Icone;
                            permission.Ordre = vue.Ordre;
                            permission.EstActif = estActifParDefaut;
                            _context.Update(permission);
                        }
                    }
                }

                var managedKeys = new HashSet<string>(
                    vuesDisponibles.Select(v => $"{v.Controleur}::{v.Action}"),
                    StringComparer.OrdinalIgnoreCase);

                var obsoletePermissions = permissionsPersisted
                    .Where(p => managedKeys.Contains($"{p.Controleur}::{p.Action}") == false)
                    .ToList();

                if (obsoletePermissions.Any())
                {
                    _context.RolePermissions.RemoveRange(obsoletePermissions);
                }

                await _context.SaveChangesAsync();
                ViderCachePermissions();

                TempData["Success"] = "Permissions reinitialisees avec succes. Les changements sont pris en compte au prochain chargement de page.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'initialisation des permissions");
                TempData["Error"] = "Erreur lors de l'initialisation des permissions.";
                return RedirectToAction(nameof(Index));
            }
        }

        private List<VueInfo> ObtenirVuesDisponibles()
        {
            return PermissionCatalog.GetDefinitions()
                .OrderBy(v => PermissionCatalog.GetCategoryOrder(v.Categorie))
                .ThenBy(v => v.Ordre)
                .Select(v => new VueInfo
                {
                    Controleur = v.Controleur,
                    Action = v.Action,
                    NomAffichage = v.NomAffichage,
                    Description = v.Description,
                    Categorie = v.Categorie,
                    Icone = v.Icone,
                    Ordre = v.Ordre
                })
                .ToList();
        }

        private VueInfo ObtenirInfoVue(string controleur, string action)
        {
            var definition = PermissionCatalog.GetDefinition(controleur, action);
            return definition != null
                ? new VueInfo
                {
                    Controleur = definition.Controleur,
                    Action = definition.Action,
                    NomAffichage = definition.NomAffichage,
                    Description = definition.Description,
                    Categorie = definition.Categorie,
                    Icone = definition.Icone,
                    Ordre = definition.Ordre
                }
                : new VueInfo
                {
                    Controleur = controleur,
                    Action = action,
                    NomAffichage = $"{controleur}.{action}",
                    Categorie = "Autre",
                    Icone = "bi-file",
                    Ordre = 999
                };
        }

        private void ViderCachePermissions(RoleUtilisateur? role = null)
        {
            try
            {
                if (role.HasValue)
                {
                    _cache.Set(PermissionCacheKeys.GetRoleVersionKey(role.Value), DateTime.UtcNow.Ticks);
                    _logger.LogInformation("Cache des permissions vide pour le role {Role}", role.Value);
                }
                else
                {
                    foreach (var currentRole in Enum.GetValues<RoleUtilisateur>())
                    {
                        _cache.Set(PermissionCacheKeys.GetRoleVersionKey(currentRole), DateTime.UtcNow.Ticks);
                    }

                    _logger.LogInformation("Cache des permissions vide pour tous les roles");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du vidage du cache des permissions");
            }
        }

        private async Task<bool> CanManagePermissionsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Autorisations", "Index");
        }
    }

    public class VueInfo
    {
        public string Controleur { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string NomAffichage { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Categorie { get; set; } = string.Empty;
        public string? Icone { get; set; }
        public int Ordre { get; set; }
    }
}
