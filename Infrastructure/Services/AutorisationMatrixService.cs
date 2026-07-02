using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Common.Security;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionProjects.Infrastructure.Services;

public sealed class AutorisationMatrixService : IAutorisationMatrixService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AutorisationMatrixService> _logger;
    private readonly IAuditService _auditService;

    public AutorisationMatrixService(
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<AutorisationMatrixService> logger,
        IAuditService auditService)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<AutorisationsViewModel> BuildIndexAsync()
    {
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

        var managedKeys = ObtenirClesGerees(vuesDisponibles);
        var entreesObsoletesCount = permissionsPersisted
            .Count(p => !managedKeys.Contains($"{p.Controleur}::{p.Action}"));

        return new AutorisationsViewModel
        {
            Roles = roles,
            PermissionsParRole = permissionsParRole,
            VuesDisponibles = vuesDisponibles,
            EntreesObsoletesCount = entreesObsoletesCount
        };
    }

    public async Task<OperationResult> UpdatePermissionAsync(UpdateRolePermissionInput input)
    {
        if (input.Role == RoleUtilisateur.AdminIT)
        {
            return OperationResult.Invalid(
                "role",
                "Le role Admin IT conserve un acces global et n'est pas restreignable depuis cette matrice.");
        }

        var permission = await _context.RolePermissions
            .FirstOrDefaultAsync(rp =>
                rp.Role == input.Role &&
                rp.Controleur == input.Controleur &&
                rp.Action == input.Action);

        var ancienEstActif = permission?.EstActif;

        var vueInfo = ObtenirInfoVue(input.Controleur, input.Action);
        if (permission == null)
        {
            permission = new RolePermission
            {
                Id = Guid.NewGuid(),
                Role = input.Role,
                Controleur = input.Controleur,
                Action = input.Action,
                NomAffichage = vueInfo.NomAffichage,
                Description = vueInfo.Description,
                Categorie = vueInfo.Categorie,
                Icone = vueInfo.Icone,
                Ordre = vueInfo.Ordre,
                EstActif = input.EstActif
            };

            _context.RolePermissions.Add(permission);
        }
        else
        {
            permission.EstActif = input.EstActif;
            permission.NomAffichage = vueInfo.NomAffichage;
            permission.Description = vueInfo.Description;
            permission.Categorie = vueInfo.Categorie;
            permission.Icone = vueInfo.Icone;
            permission.Ordre = vueInfo.Ordre;
            _context.Update(permission);
        }

        await _context.SaveChangesAsync();
        ViderCachePermissions(input.Role);

        await _auditService.LogActionAsync(
            "MODIFICATION_PERMISSION_ROLE",
            "RolePermission",
            permission.Id,
            new { input.Role, input.Controleur, input.Action, EstActif = ancienEstActif },
            new { input.Role, input.Controleur, input.Action, input.EstActif });

        return OperationResult.Success(
            "Permission mise a jour avec succes. Le changement est effectif des la prochaine verification de droits de l'utilisateur concerne.");
    }

    public async Task<OperationResult> InitialiserPermissionsAsync()
    {
        var vuesDisponibles = ObtenirVuesDisponibles();
        var roles = Enum.GetValues<RoleUtilisateur>();
        var permissionsPersisted = await _context.RolePermissions.ToListAsync();

        var nombreCrees = 0;
        var nombreModifies = 0;

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
                    nombreCrees++;
                }
                else
                {
                    if (permission.EstActif != estActifParDefaut)
                    {
                        nombreModifies++;
                    }

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

        var managedKeys = ObtenirClesGerees(vuesDisponibles);

        var obsoletePermissions = permissionsPersisted
            .Where(p => managedKeys.Contains($"{p.Controleur}::{p.Action}") == false)
            .ToList();

        if (obsoletePermissions.Any())
        {
            _context.RolePermissions.RemoveRange(obsoletePermissions);
        }

        await _context.SaveChangesAsync();
        ViderCachePermissions();

        await _auditService.LogActionAsync(
            "REINITIALISATION_PERMISSIONS",
            "RolePermission",
            null,
            null,
            new { NombreCrees = nombreCrees, NombreModifies = nombreModifies, NombreSupprimes = obsoletePermissions.Count });

        return OperationResult.Success(
            $"Permissions reinitialisees avec succes ({nombreCrees} creee(s), {nombreModifies} modifiee(s), {obsoletePermissions.Count} obsolete(s) supprimee(s)). " +
            "Le changement est effectif des la prochaine verification de droits des utilisateurs concernes.");
    }

    private static HashSet<string> ObtenirClesGerees(List<VueInfo> vuesDisponibles)
    {
        return new HashSet<string>(
            vuesDisponibles.Select(v => $"{v.Controleur}::{v.Action}"),
            StringComparer.OrdinalIgnoreCase);
    }

    private static List<VueInfo> ObtenirVuesDisponibles()
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

    private static VueInfo ObtenirInfoVue(string controleur, string action)
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
}
