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
        // POST: Créer (soumettre) un avenant
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CreerAvenant(
            Guid id,
            TypeAvenant type,
            string titre,
            string justification,
            string? descriptionPerimetre,
            decimal? nouveauBudget,
            DateTime? nouvelleDateFinPrevue,
            [FromServices] IAvenantProjetService avenantService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.CanActAsChefProjet || ui.HasDsiGovernanceAccess))
                return Forbid();

            var result = await avenantService.CreerAsync(
                id, User.GetUserIdOrThrow(), type, titre, justification,
                descriptionPerimetre, nouveauBudget, nouvelleDateFinPrevue);

            return MapAvenantResult(result, id);
        }

        // POST: Valider un avenant par le Métier (DM / sponsor)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderAvenantDM(Guid id, Guid avenantId, [FromServices] IAvenantProjetService avenantService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!(ui.HasDmGovernanceAccess && ui.IsProjectSponsor) || ui.IsReadOnly)
                return Forbid();

            var result = await avenantService.ValiderDmAsync(avenantId, User.GetUserIdOrThrow());
            return MapAvenantResult(result, id);
        }

        // POST: Valider un avenant par la DSI (applique le changement)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderAvenantDSI(Guid id, Guid avenantId, [FromServices] IAvenantProjetService avenantService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.HasDsiGovernanceAccess)
                return Forbid();

            var result = await avenantService.ValiderDsiAsync(avenantId, User.GetUserIdOrThrow());
            return MapAvenantResult(result, id);
        }

        // POST: Rejeter un avenant (à l'étape Métier ou DSI)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterAvenant(Guid id, Guid avenantId, string commentaire, [FromServices] IAvenantProjetService avenantService)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            var peutRejeter = ui.HasDsiGovernanceAccess ||
                              (ui.HasDmGovernanceAccess && ui.IsProjectSponsor && !ui.IsReadOnly);
            if (!peutRejeter)
                return Forbid();

            var result = await avenantService.RejeterAsync(avenantId, User.GetUserIdOrThrow(), commentaire);
            return MapAvenantResult(result, id);
        }

        private IActionResult MapAvenantResult(WorkflowResult result, Guid id)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;
            else if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id, tab = "avenants" });
        }
    }
}
