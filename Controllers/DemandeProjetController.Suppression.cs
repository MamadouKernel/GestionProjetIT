using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // POST: Envoyer une demande à la corbeille (réservé AdminIT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerDemande(Guid id)
        {
            if (!User.IsInRole(nameof(RoleUtilisateur.AdminIT)))
                return Forbid();

            var result = await _demandeWorkflowService.SupprimerAsync(id, NomActeurCourant());
            return MapCorbeilleResult(result);
        }

        // POST: Restaurer une demande depuis la corbeille (réservé AdminIT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RestaurerDemande(Guid id)
        {
            if (!User.IsInRole(nameof(RoleUtilisateur.AdminIT)))
                return Forbid();

            var result = await _demandeWorkflowService.RestaurerAsync(id, NomActeurCourant());
            return MapCorbeilleResult(result);
        }

        private IActionResult MapCorbeilleResult(WorkflowResult result)
        {
            if (result.IsNotFound) return NotFound();
            if (result.IsForbidden) return Forbid();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Index), new { afficherSupprimees = true });
        }
    }
}
