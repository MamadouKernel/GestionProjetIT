using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // POST: Envoyer un projet à la corbeille (réservé AdminIT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerProjet(Guid id, [FromServices] IProjetDetailsWorkflowService detailsWorkflow)
        {
            if (!User.IsInRole(nameof(RoleUtilisateur.AdminIT)))
                return Forbid();

            var nomActeur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            var result = await detailsWorkflow.SupprimerAsync(id, nomActeur);
            return MapCorbeilleProjetResult(result);
        }

        // POST: Restaurer un projet depuis la corbeille (réservé AdminIT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RestaurerProjet(Guid id, [FromServices] IProjetDetailsWorkflowService detailsWorkflow)
        {
            if (!User.IsInRole(nameof(RoleUtilisateur.AdminIT)))
                return Forbid();

            var nomActeur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            var result = await detailsWorkflow.RestaurerAsync(id, nomActeur);
            return MapCorbeilleProjetResult(result);
        }

        private IActionResult MapCorbeilleProjetResult(Application.Common.Results.WorkflowResult result)
        {
            if (result.IsNotFound) return NotFound();
            if (result.IsForbidden) return Forbid();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Index), new { afficherSupprimes = true });
        }
    }
}
