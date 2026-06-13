using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class RoleAdminService : IRoleAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IUtilisateurService _utilisateurService;

    public RoleAdminService(
        ApplicationDbContext db,
        IAuditService audit,
        IUtilisateurService utilisateurService)
    {
        _db = db;
        _audit = audit;
        _utilisateurService = utilisateurService;
    }

    public async Task<RolesListViewModel> GetListAsync(string? recherche, Guid? directionId, RoleUtilisateur? role, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = _db.Utilisateurs
            .Include(u => u.Direction)
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(u => u.Nom.Contains(recherche) || u.Prenoms.Contains(recherche) || u.Matricule.Contains(recherche));

        if (directionId.HasValue)
            query = query.Where(u => u.DirectionId == directionId.Value);

        if (role.HasValue)
            query = query.Where(u => u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == role.Value));

        query = query.OrderBy(u => u.Nom).ThenBy(u => u.Prenoms);

        var paged = await query.ToPagedResultAsync(page, pageSize);

        var allRoles   = Enum.GetValues<RoleUtilisateur>().ToList();
        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .OrderBy(d => d.Libelle)
            .ToListAsync();

        var allUsers = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime)
            .ToListAsync();
        var roleCounts = allRoles.ToDictionary(r => r, r => allUsers.Count(u => u.GetRolesActifs().Contains(r)));

        return new RolesListViewModel
        {
            Users               = paged.Items,
            Directions          = directions,
            AllRoles            = allRoles,
            RoleCounts          = roleCounts,
            TotalCount          = paged.TotalCount,
            PageNumber          = paged.PageNumber,
            TotalPages          = paged.TotalPages,
            PageSize            = paged.PageSize,
            Recherche           = recherche,
            SelectedDirectionId = directionId,
            SelectedRole        = role
        };
    }

    public async Task<GererRolesViewModel?> GetUserForRolesAsync(Guid id)
    {
        var user = await _db.Utilisateurs
            .Include(u => u.Direction)
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

        if (user == null)
            return null;

        return new GererRolesViewModel
        {
            User     = user,
            AllRoles = Enum.GetValues<RoleUtilisateur>().ToList()
        };
    }

    public async Task<UpdateRolesResult> UpdateRolesAsync(Guid id, string? roles)
    {
        var user = await _db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

        if (user == null)
            return new UpdateRolesResult(NotFound: true, NoRolesSelected: false, InfoMessage: null, SuccessMessage: null);

        var rolesEnum = _utilisateurService.ParseSelectedRoles(roles);

        if (!rolesEnum.Any())
            return new UpdateRolesResult(NotFound: false, NoRolesSelected: true, InfoMessage: null, SuccessMessage: null);

        var rolesInput = (roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => int.TryParse(r.Trim(), out var rid) ? rid : 0)
            .Where(r => r > 0).ToList();

        string? info = null;
        if (rolesEnum.Contains(RoleUtilisateur.AdminIT) && rolesInput.Count > 1)
            info = "Le rôle Admin IT est exclusif : les autres rôles ont été retirés automatiquement.";

        var rolesActuels = user.GetRolesActifs().ToList();

        await _utilisateurService.SynchronizeUserRolesAsync(user, rolesEnum);
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("MODIFICATION_ROLES_UTILISATEUR", "Utilisateur", user.Id,
            new { AnciensRoles = string.Join(", ", rolesActuels) },
            new { NouveauxRoles = string.Join(", ", rolesEnum) });

        return new UpdateRolesResult(
            NotFound: false,
            NoRolesSelected: false,
            InfoMessage: info,
            SuccessMessage: $"Rôles de {user.Nom} {user.Prenoms} mis à jour avec succès.");
    }
}
