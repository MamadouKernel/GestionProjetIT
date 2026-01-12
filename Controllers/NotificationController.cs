using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notificationService;

        public NotificationController(
            ApplicationDbContext db,
            INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        // GET: Liste des notifications
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();

            var query = _db.Notifications
                .Where(n => n.UtilisateurId == userId && !n.EstSupprime)
                .OrderByDescending(n => n.DateCreation);

            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var pagedResult = await query.ToPagedResultAsync(page, pageSize);

            ViewBag.PageNumber = pagedResult.PageNumber;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.PageSize = pagedResult.PageSize;

            ViewBag.NonLues = await _db.Notifications
                .CountAsync(n => n.UtilisateurId == userId && !n.EstLue && !n.EstSupprime);

            return View(pagedResult.Items);
        }

        // GET: API pour récupérer le nombre de notifications non lues
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.GetUserIdOrThrow();
            var count = await _db.Notifications
                .CountAsync(n => n.UtilisateurId == userId && !n.EstLue && !n.EstSupprime);

            return Json(new { count });
        }

        // GET: API pour récupérer les dernières notifications non lues
        [HttpGet]
        public async Task<IActionResult> GetUnreadNotifications(int count = 5)
        {
            var userId = User.GetUserIdOrThrow();

            var notifications = await _db.Notifications
                .Where(n => n.UtilisateurId == userId && !n.EstLue && !n.EstSupprime)
                .OrderByDescending(n => n.DateCreation)
                .Take(count)
                .Select(n => new
                {
                    n.Id,
                    n.Titre,
                    n.Message,
                    n.TypeNotification,
                    n.EntiteType,
                    n.EntiteId,
                    n.DateCreation,
                    DateCreationFormatted = n.DateCreation.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(notifications);
        }

        // POST: Marquer comme lue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarquerLue(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            await _notificationService.MarquerCommeLueAsync(id, userId);
            return Json(new { success = true });
        }

        // POST: Marquer toutes comme lues
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarquerToutesLues()
        {
            var userId = User.GetUserIdOrThrow();
            await _notificationService.MarquerToutesCommeLuesAsync(userId);
            return Json(new { success = true });
        }
    }
}

