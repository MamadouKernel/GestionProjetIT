using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationQueryService _notificationQuery;
        private readonly INotificationService _notificationService;
        private readonly INotificationTargetResolver _targetResolver;

        public NotificationController(
            INotificationQueryService notificationQuery,
            INotificationService notificationService,
            INotificationTargetResolver targetResolver)
        {
            _notificationQuery = notificationQuery;
            _notificationService = notificationService;
            _targetResolver = targetResolver;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var vm = await _notificationQuery.GetIndexAsync(userId, page, pageSize);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.GetUserIdOrThrow();
            var count = await _notificationQuery.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadNotifications(int count = 5)
        {
            var userId = User.GetUserIdOrThrow();
            var notifications = await _notificationQuery.GetUnreadNotificationsAsync(userId, count);

            return Json(notifications.Select(n => new
            {
                n.Id,
                n.Titre,
                n.Message,
                n.TypeNotification,
                n.EntiteType,
                n.EntiteId,
                n.DateCreation,
                n.DateCreationFormatted,
                OuvrirUrl = Url.Action(nameof(Ouvrir), "Notification", new { id = n.Id })
            }));
        }

        [HttpGet]
        public async Task<IActionResult> Ouvrir(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            var notification = await _notificationQuery.GetOpenInfoAsync(id, userId);
            if (notification == null)
            {
                TempData["Error"] = "Notification introuvable ou deja supprimee.";
                return RedirectToAction(nameof(Index));
            }

            await _notificationService.MarquerCommeLueAsync(id, userId);

            var target = await _targetResolver.ResolveAsync(notification.EntiteType, notification.EntiteId);
            return RedirectToAction(target.Action, target.Controller, target.RouteValues);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarquerLue(Guid id, string? returnUrl = null)
        {
            var userId = User.GetUserIdOrThrow();
            await _notificationService.MarquerCommeLueAsync(id, userId);
            if (IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            return RedirectToSafeNotificationUrl(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarquerToutesLues(string? returnUrl = null)
        {
            var userId = User.GetUserIdOrThrow();
            await _notificationService.MarquerToutesCommeLuesAsync(userId);
            if (IsAjaxRequest())
            {
                return Json(new { success = true });
            }

            return RedirectToSafeNotificationUrl(returnUrl);
        }

        private bool IsAjaxRequest()
        {
            if (Request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
                string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var acceptHeader = Request.Headers.Accept.ToString();
            return acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult RedirectToSafeNotificationUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
