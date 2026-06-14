using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class HomeController : Controller
    {
        private readonly IHomeDashboardService _homeDashboard;

        public HomeController(IHomeDashboardService homeDashboard)
        {
            _homeDashboard = homeDashboard;
        }
    }
}
