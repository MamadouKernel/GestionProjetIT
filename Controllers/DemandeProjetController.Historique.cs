using GestionProjects.Application.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // GET: Historique des actions du Directeur Métier sur les demandes
        [Authorize]
        public async Task<IActionResult> HistoriqueActionsDM(int page = 1, int pageSize = 20)
        {
            var vm = await _demandeQueryService.GetHistoriqueActionsDMAsync(
                User.GetUserIdOrThrow(), await HasAdminScopeAsync(), page, pageSize);
            return View(vm);
        }
    }
}
