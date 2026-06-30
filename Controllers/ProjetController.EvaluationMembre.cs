using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // POST: Enregistrer (ajouter ou mettre à jour) l'évaluation d'un membre
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnregistrerEvaluationMembre(
            Guid id, Guid membreProjetId, int noteQualite, int noteRespectDelais, int noteCollaboration,
            string? commentaire, [FromServices] IEvaluationMembreProjetService evaluationService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess))
                return Forbid();

            var result = await evaluationService.EnregistrerAsync(
                id, membreProjetId, User.GetUserIdOrThrow(), noteQualite, noteRespectDelais, noteCollaboration, commentaire);

            return MapEvaluationMembreResult(result, id);
        }

        // POST: Supprimer l'évaluation d'un membre
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerEvaluationMembre(
            Guid id, Guid evaluationId, [FromServices] IEvaluationMembreProjetService evaluationService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess))
                return Forbid();

            var result = await evaluationService.SupprimerAsync(evaluationId, User.GetUserIdOrThrow());
            return MapEvaluationMembreResult(result, id);
        }

        private IActionResult MapEvaluationMembreResult(WorkflowResult result, Guid id)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;
            else if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }
    }
}
