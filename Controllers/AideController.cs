using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class AideController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AideController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.GetUserIdOrThrow();

            var user = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Include(u => u.Direction)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Récupérer les rôles de l'utilisateur
            var roles = user.UtilisateurRoles
                .Where(ur => !ur.EstSupprime && 
                            (!ur.DateDebut.HasValue || ur.DateDebut.Value <= DateTime.Now) &&
                            (!ur.DateFin.HasValue || ur.DateFin.Value >= DateTime.Now))
                .Select(ur => ur.Role)
                .ToList();

            ViewBag.UserRoles = roles;
            ViewBag.UserName = $"{user.Nom} {user.Prenoms}";
            ViewBag.UserDirection = user.Direction?.Libelle ?? "Non assigné";

            return View();
        }
    }
}

