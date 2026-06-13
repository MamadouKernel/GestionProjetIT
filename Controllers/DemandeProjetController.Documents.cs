using GestionProjects.Application.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // POST: Ajouter des documents complémentaires
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> AjouterDocumentsComplementaires(Guid id, List<IFormFile>? documents)
        {
            var result = await _demandeWorkflowService.AjouterDocumentsComplementairesAsync(
                id, documents, User.GetUserIdOrThrow(), await CanManageDemandesBackofficeAsync());
            return MapWorkflowToDetails(result, id);
        }

        // POST: Dupliquer/Relancer une demande refusée
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DupliquerDemande(Guid id)
        {
            var result = await _demandeWorkflowService.DupliquerDemandeAsync(
                id, User.GetUserIdOrThrow(), await CanManageDemandesBackofficeAsync());

            if (result.IsNotFound)  return NotFound();
            if (result.IsForbidden) return Forbid();

            if (result.ErrorMessage is not null)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction(nameof(Details), new { id = result.NouvelleDemandeId });
        }
    }
}
