using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class DemandesAccesController : Controller
    {
        private readonly IPermissionService _permissionService;
        private readonly IDemandeAccesQueryService _demandeAccesQuery;
        private readonly IDemandeAccesWorkflowService _demandeAccesWorkflow;

        public DemandesAccesController(
            IPermissionService permissionService,
            IDemandeAccesQueryService demandeAccesQuery,
            IDemandeAccesWorkflowService demandeAccesWorkflow)
        {
            _permissionService = permissionService;
            _demandeAccesQuery = demandeAccesQuery;
            _demandeAccesWorkflow = demandeAccesWorkflow;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? recherche = null,
            StatutDemandeAcces? statut = null,
            Guid? focusId = null,
            int page = 1, int pageSize = 15)
        {
            if (!await CanManageAccessRequestsAsync())
                return Forbid();

            var vm = await _demandeAccesQuery.GetIndexAsync(recherche, statut, focusId, page, pageSize);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approuver(Guid id, Guid? directionId, string? commentaire, RoleUtilisateur role = RoleUtilisateur.Demandeur)
        {
            if (!await CanManageAccessRequestsAsync())
            {
                return Forbid();
            }

            var result = await _demandeAccesWorkflow.ApprouverAsync(
                new ApprouverDemandeAccesInput(id, directionId, commentaire, role, User.GetUserIdOrThrow()));
            return RedirectWithWorkflowResult(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rejeter(Guid id, string commentaire)
        {
            if (!await CanManageAccessRequestsAsync())
            {
                return Forbid();
            }

            var result = await _demandeAccesWorkflow.RejeterAsync(
                new RejeterDemandeAccesInput(id, commentaire, User.GetUserIdOrThrow()));
            return RedirectWithWorkflowResult(result);
        }

        private async Task<bool> CanManageAccessRequestsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("DemandesAcces", "Index");
        }

        private IActionResult RedirectWithWorkflowResult(DemandeAccesWorkflowResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                TempData["Error"] = result.ErrorMessage;
            }
            else if (!string.IsNullOrWhiteSpace(result.InfoMessage))
            {
                TempData["Info"] = result.InfoMessage;
            }
            else
            {
                TempData["Success"] = result.SuccessMessage;
            }

            return result.FocusId.HasValue
                ? RedirectToAction(nameof(Index), new { focusId = result.FocusId.Value })
                : RedirectToAction(nameof(Index));
        }
    }
}
