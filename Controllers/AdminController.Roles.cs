using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> ListeRoles(string? recherche = null, Guid? directionId = null, RoleUtilisateur? role = null, int page = 1, int pageSize = 20)
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

            var allRoles   = Enum.GetValues(typeof(RoleUtilisateur)).Cast<RoleUtilisateur>().ToList();
            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            var allUsers = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime)
                .ToListAsync();
            var roleCounts = Enum.GetValues(typeof(RoleUtilisateur))
                .Cast<RoleUtilisateur>()
                .ToDictionary(r => r, r => allUsers.Count(u => u.GetRolesActifs().Contains(r)));

            var vm = new RolesListViewModel
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

            return View(vm);
        }

        public async Task<IActionResult> GererRoles(Guid id)
        {
            var user = await _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (user == null)
                return NotFound();

            var vm = new GererRolesViewModel
            {
                User     = user,
                AllRoles = Enum.GetValues(typeof(RoleUtilisateur)).Cast<RoleUtilisateur>().ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(Guid id, string Roles)
        {
            var user = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (user == null)
                return NotFound();

            var rolesEnum = _utilisateurService.ParseSelectedRoles(Roles);

            if (!rolesEnum.Any())
            {
                TempData["Error"] = "Veuillez sélectionner au moins un rôle.";
                return RedirectToAction(nameof(GererRoles), new { id });
            }

            var rolesInput = (Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => int.TryParse(r.Trim(), out var rid) ? rid : 0)
                .Where(r => r > 0).ToList();

            if (rolesEnum.Contains(RoleUtilisateur.AdminIT) && rolesInput.Count > 1)
                TempData["Info"] = "Le rôle Admin IT est exclusif : les autres rôles ont été retirés automatiquement.";

            var rolesActuels = user.GetRolesActifs().ToList();

            await _utilisateurService.SynchronizeUserRolesAsync(user, rolesEnum);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MODIFICATION_ROLES_UTILISATEUR", "Utilisateur", user.Id,
                new { AnciensRoles = string.Join(", ", rolesActuels) },
                new { NouveauxRoles = string.Join(", ", rolesEnum) });

            TempData["Success"] = $"Rôles de {user.Nom} {user.Prenoms} mis à jour avec succès.";
            return RedirectToAction(nameof(ListeRoles));
        }
    }
}
