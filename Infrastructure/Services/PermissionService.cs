using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Security;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionProjects.Infrastructure.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PermissionService> _logger;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public PermissionService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IMemoryCache cache,
            ILogger<PermissionService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> HasPermissionAsync(RoleUtilisateur role, string controleur, string action)
        {
            try
            {
                if (role == RoleUtilisateur.AdminIT)
                {
                    return true;
                }

                var version = _cache.Get<long>(PermissionCacheKeys.GetRoleVersionKey(role));
                var cacheKey = PermissionCacheKeys.GetPermissionKey(role, controleur, action, version);
                if (_cache.TryGetValue(cacheKey, out bool cachedResult))
                {
                    return cachedResult;
                }

                var permission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp =>
                        rp.Role == role &&
                        rp.Controleur == controleur &&
                        rp.Action == action);

                var result = permission != null
                    ? permission.EstActif
                    : PermissionCatalog.IsEnabledByDefault(role, controleur, action);

                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erreur lors de la verification de permission pour {Role} - {Controleur}.{Action}",
                    role, controleur, action);
                return role == RoleUtilisateur.AdminIT;
            }
        }

        public async Task<bool> CurrentUserHasPermissionAsync(string controleur, string action)
        {
            try
            {
                var userRoles = _currentUserService.Roles;
                if (userRoles == null || !userRoles.Any())
                {
                    return false;
                }

                foreach (var roleStr in userRoles)
                {
                    if (Enum.TryParse<RoleUtilisateur>(roleStr, out var role) &&
                        await HasPermissionAsync(role, controleur, action))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erreur lors de la verification de permission pour l'utilisateur actuel - {Controleur}.{Action}",
                    controleur, action);
                return false;
            }
        }

        public async Task<List<(string Controleur, string Action)>> GetActivePermissionsAsync(RoleUtilisateur role)
        {
            try
            {
                if (role == RoleUtilisateur.AdminIT)
                {
                    return PermissionCatalog.GetDefinitions()
                        .Select(d => (d.Controleur, d.Action))
                        .Distinct()
                        .ToList();
                }

                var version = _cache.Get<long>(PermissionCacheKeys.GetRoleVersionKey(role));
                var cacheKey = PermissionCacheKeys.GetPermissionListKey(role, version);
                if (_cache.TryGetValue(cacheKey, out List<(string, string)>? cachedResult) && cachedResult != null)
                {
                    return cachedResult;
                }

                var permissions = await _context.RolePermissions
                    .Where(rp => rp.Role == role)
                    .ToListAsync();

                var explicitMap = permissions.ToDictionary(
                    p => $"{p.Controleur}::{p.Action}",
                    p => p.EstActif,
                    StringComparer.OrdinalIgnoreCase);

                var result = new List<(string Controleur, string Action)>();

                foreach (var definition in PermissionCatalog.GetDefinitions())
                {
                    var key = $"{definition.Controleur}::{definition.Action}";
                    var isActive = explicitMap.TryGetValue(key, out var estActif)
                        ? estActif
                        : PermissionCatalog.IsEnabledByDefault(role, definition.Controleur, definition.Action);

                    if (isActive)
                    {
                        result.Add((definition.Controleur, definition.Action));
                    }
                }

                foreach (var permission in permissions.Where(p =>
                             p.EstActif &&
                             !PermissionCatalog.IsManagedAction(p.Controleur, p.Action)))
                {
                    result.Add((permission.Controleur, permission.Action));
                }

                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recuperation des permissions pour {Role}", role);
                return new List<(string, string)>();
            }
        }

        public async Task<List<(string Controleur, string Action)>> GetCurrentUserActivePermissionsAsync()
        {
            try
            {
                var userRoles = _currentUserService.Roles;
                if (userRoles == null || !userRoles.Any())
                {
                    return new List<(string, string)>();
                }

                var allPermissions = new HashSet<(string, string)>();

                foreach (var roleStr in userRoles)
                {
                    if (Enum.TryParse<RoleUtilisateur>(roleStr, out var role))
                    {
                        var rolePermissions = await GetActivePermissionsAsync(role);
                        foreach (var permission in rolePermissions)
                        {
                            allPermissions.Add(permission);
                        }
                    }
                }

                return allPermissions.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recuperation des permissions pour l'utilisateur actuel");
                return new List<(string, string)>();
            }
        }

        public async Task<bool> IsActiveDmDelegateAsync(Guid directeurMetierId, Guid delegueId)
        {
            return await _context.DelegationsValidationDM.AnyAsync(d =>
                d.DirecteurMetierId == directeurMetierId &&
                d.DelegueId == delegueId &&
                d.EstActive &&
                d.DateDebut <= DateTime.UtcNow &&
                d.DateFin >= DateTime.UtcNow &&
                !d.EstSupprime);
        }

        public async Task<bool> IsActiveChefProjetDelegateAsync(Guid projetId, Guid delegueId)
        {
            return await _context.DelegationsChefProjet.AnyAsync(d =>
                d.ProjetId == projetId &&
                d.DelegueId == delegueId &&
                d.EstActive &&
                d.DateDebut <= DateTime.UtcNow &&
                (d.DateFin == null || d.DateFin >= DateTime.UtcNow) &&
                !d.EstSupprime);
        }
    }
}
