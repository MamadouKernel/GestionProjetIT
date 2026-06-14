using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardAnalyticsService _dashboardAnalytics;

        public DashboardController(IDashboardAnalyticsService dashboardAnalytics)
        {
            _dashboardAnalytics = dashboardAnalytics;
        }

        public async Task<IActionResult> Index()
        {
            if (!await _dashboardAnalytics.CanAccessAsync())
            {
                return Forbid();
            }

            ViewData["Title"] = "Tableau de bord analytique";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> StatsProjetsParPhase()
        {
            var result = await _dashboardAnalytics.GetProjetsParPhaseAsync(User.GetUserIdOrThrow());
            return ToJsonOrForbid(result);
        }

        [HttpGet]
        public async Task<IActionResult> StatsRAG()
        {
            var result = await _dashboardAnalytics.GetRagAsync(User.GetUserIdOrThrow());
            return ToJsonOrForbid(result);
        }

        [HttpGet]
        public async Task<IActionResult> StatsProjetsParDirection()
        {
            var result = await _dashboardAnalytics.GetProjetsParDirectionAsync();
            return ToJsonOrForbid(result);
        }

        [HttpGet]
        public async Task<IActionResult> StatsProjetsParStatut()
        {
            var result = await _dashboardAnalytics.GetProjetsParStatutAsync(User.GetUserIdOrThrow());
            return ToJsonOrForbid(result);
        }

        [HttpGet]
        public async Task<IActionResult> StatsTendanceMensuelle()
        {
            var result = await _dashboardAnalytics.GetTendanceMensuelleAsync(User.GetUserIdOrThrow());
            return ToJsonOrForbid(result);
        }

        [HttpGet]
        public async Task<IActionResult> StatsDureeParPhase()
        {
            var result = await _dashboardAnalytics.GetDureeParPhaseAsync(User.GetUserIdOrThrow());
            return ToJsonOrForbid(result);
        }

        [HttpGet]
        public async Task<IActionResult> StatsKPIs()
        {
            var result = await _dashboardAnalytics.GetKpisAsync(User.GetUserIdOrThrow());
            return ToJsonOrForbid(result);
        }

        private IActionResult ToJsonOrForbid<T>(DashboardAnalyticsResult<T> result)
        {
            return result.IsForbidden ? Forbid() : Json(result.Data);
        }
    }
}
