using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // POST: Valider par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderDM(Guid id, string? commentaire,
            string? titre, string? description, string? objectifs, string? avantagesAttendus)
        {
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ValiderDM"))
                return Forbid();

            var result = await _demandeWorkflowService.ValiderDmAsync(
                id, commentaire, titre, description, objectifs, avantagesAttendus,
                User.GetUserIdOrThrow(), await HasAdminScopeAsync(), NomActeurCourant());
            return MapWorkflowToDetails(result, id);
        }

        // POST: Rejeter par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterDM(Guid id, string commentaire)
        {
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "RejeterDM"))
                return Forbid();

            var result = await _demandeWorkflowService.RejeterDmAsync(
                id, commentaire, User.GetUserIdOrThrow(), await HasAdminScopeAsync(), NomActeurCourant());
            return MapWorkflowToDetails(result, id);
        }

        // POST: Demander correction par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DemanderCorrectionDM(Guid id, string commentaire)
        {
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "DemanderCorrectionDM"))
                return Forbid();

            var result = await _demandeWorkflowService.DemanderCorrectionDmAsync(
                id, commentaire, User.GetUserIdOrThrow(), await HasAdminScopeAsync(), NomActeurCourant());
            return MapWorkflowToDetails(result, id);
        }

        // ── Helpers HTTP (présentation uniquement) ────────────────────────────
        private string NomActeurCourant() =>
            $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();

        private IActionResult MapWorkflowToDetails(WorkflowResult result, Guid id)
        {
            if (result.IsNotFound)  return NotFound();
            if (result.IsForbidden) return Forbid();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
