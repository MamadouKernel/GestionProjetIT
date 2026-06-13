using GestionProjects.Application.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public Task<IActionResult> Soumettre(Guid id)
        {
            return SoumettreDepuisWorkflowAsync(id, ignorerDoublons: false, avertirSiDoublons: false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public Task<IActionResult> ConfirmerSoumission(Guid id, bool confirmerMalgreDoublons = false)
        {
            return SoumettreDepuisWorkflowAsync(
                id,
                ignorerDoublons: confirmerMalgreDoublons,
                avertirSiDoublons: true);
        }

        private async Task<IActionResult> SoumettreDepuisWorkflowAsync(
            Guid id,
            bool ignorerDoublons,
            bool avertirSiDoublons)
        {
            var userId = User.GetUserIdOrThrow();
            var result = await _demandeWorkflowService.SoumettreAsync(
                id,
                userId,
                await HasAdminScopeAsync(),
                ignorerDoublons);

            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (result.Doublons != null)
            {
                if (avertirSiDoublons)
                {
                    TempData["Warning"] = "Veuillez confirmer que vous souhaitez soumettre cette demande malgre l'existence de demandes similaires.";
                }

                return View("VerificationDoublons", result.Doublons);
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;

            if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
