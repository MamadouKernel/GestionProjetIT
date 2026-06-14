using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class HomeController
    {
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var stats = await _homeDashboard.BuildAsync(
                User.GetUserIdOrThrow(),
                (action, controller, values) => Url.Action(action, controller, values));

            return View(stats);
        }
    }
}
