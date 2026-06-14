using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class AideController : Controller
    {
        private readonly IAideQueryService _aideQuery;

        public AideController(IAideQueryService aideQuery)
        {
            _aideQuery = aideQuery;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.GetUserIdOrThrow();

            var context = await _aideQuery.GetUserContextAsync(userId);
            if (context == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.UserRoles = context.Roles.ToList();
            ViewBag.UserName = context.UserName;
            ViewBag.UserDirection = context.UserDirection;

            return View();
        }
    }
}
