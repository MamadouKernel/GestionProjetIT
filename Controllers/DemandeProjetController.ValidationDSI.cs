using GestionProjects.Application.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // POST: Valider par DSI (crée le projet)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderDSI(Guid id, string? commentaire, Guid? chefProjetId)
        {
            var isDelegue = await IsActiveDsiDelegateAsync(User.GetUserIdOrThrow());
            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de valider cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _demandeWorkflowService.ValiderDsiAsync(id, commentaire, chefProjetId, isDelegue, NomActeurCourant());

            if (result.IsNotFound)
                return NotFound();

            if (result.ErrorMessage is not null)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction("Details", "Projet", new { id = result.ProjetId });
        }

        // POST: Rejeter par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterDSI(Guid id, string? commentaire)
        {
            var isDelegue = await IsActiveDsiDelegateAsync(User.GetUserIdOrThrow());
            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de rejeter cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _demandeWorkflowService.RejeterDsiAsync(id, commentaire, isDelegue, NomActeurCourant());
            return MapWorkflowToDetails(result, id);
        }

        // POST: Renvoyer au demandeur par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RenvoyerAuDemandeurDSI(Guid id, string? commentaire)
        {
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "HistoriqueActionsDM"))
                return Forbid();

            var isDelegue = await IsActiveDsiDelegateAsync(User.GetUserIdOrThrow());
            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de renvoyer cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _demandeWorkflowService.RenvoyerAuDemandeurDsiAsync(id, commentaire, isDelegue, NomActeurCourant());
            return MapWorkflowToDetails(result, id);
        }

        // POST: Renvoyer au Directeur Métier par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RenvoyerAuDMDSI(Guid id, string commentaire)
        {
            var isDelegue = await IsActiveDsiDelegateAsync(User.GetUserIdOrThrow());
            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de renvoyer cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _demandeWorkflowService.RenvoyerAuDmDsiAsync(id, commentaire, isDelegue, NomActeurCourant());
            return MapWorkflowToDetails(result, id);
        }
    }
}
