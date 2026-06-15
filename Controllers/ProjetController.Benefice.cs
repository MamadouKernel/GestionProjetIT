using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // POST: Ajouter un bénéfice attendu
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterBenefice(
            Guid id, string libelle, string indicateur, string valeurCible,
            DateTime? dateCibleRealisation, [FromServices] IBeneficeProjetService beneficeService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess))
                return Forbid();

            var result = await beneficeService.AjouterAsync(
                id, User.GetUserIdOrThrow(), libelle, indicateur, valeurCible, dateCibleRealisation);

            return MapBeneficeResult(result, id);
        }

        // POST: Évaluer un bénéfice (revue post-implémentation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EvaluerBenefice(
            Guid id, Guid beneficeId, StatutBenefice statut, string? valeurRealisee,
            string? commentaire, [FromServices] IBeneficeProjetService beneficeService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess))
                return Forbid();

            var result = await beneficeService.EvaluerAsync(
                beneficeId, User.GetUserIdOrThrow(), statut, valeurRealisee, commentaire);

            return MapBeneficeResult(result, id);
        }

        // POST: Supprimer un bénéfice
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerBenefice(Guid id, Guid beneficeId, [FromServices] IBeneficeProjetService beneficeService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess))
                return Forbid();

            var result = await beneficeService.SupprimerAsync(beneficeId, User.GetUserIdOrThrow());
            return MapBeneficeResult(result, id);
        }

        private IActionResult MapBeneficeResult(WorkflowResult result, Guid id)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;
            else if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id, tab = "benefices" });
        }
    }
}
