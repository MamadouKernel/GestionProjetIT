using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Parametres()
        {
            return View(await _parametreService.GetViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnregistrerParametresWorkflow(
            string? dsiPrincipalId,
            string? dsiDelegueId,
            int? delaiInactiviteSessionMinutes,
            string? repertoireStockageRacine,
            string? typesLivrables)
        {
            await _parametreService.SaveWorkflowAsync(new ParametresWorkflowInput(
                dsiPrincipalId, dsiDelegueId, delaiInactiviteSessionMinutes, repertoireStockageRacine, typesLivrables));

            TempData["Success"] = "Paramètres workflow enregistrés avec succès.";
            return RedirectToAction(nameof(Parametres));
        }

        [HttpGet]
        public async Task<IActionResult> GetParametre(Guid id)
        {
            var details = await _parametreService.GetDetailsAsync(id);
            return details is null ? NotFound() : Json(details);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateParametre(string Cle, string Valeur, string Description)
        {
            var result = await _parametreService.CreateAsync(new CreateParametreInput(Cle, Valeur, Description));
            return await HandleParametreResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateParametre(Guid id, string Cle, string Valeur, string Description)
        {
            var result = await _parametreService.UpdateAsync(new UpdateParametreInput(id, Cle, Valeur, Description));
            return await HandleParametreResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteParametre(Guid id)
        {
            var result = await _parametreService.DeleteAsync(id);
            return await HandleParametreResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTeamsWebhook(Guid? parametreId, string? webhookUrl)
        {
            TempData["Success"] = await _parametreService.SaveTeamsWebhookAsync(parametreId, webhookUrl);
            return RedirectToAction(nameof(Parametres));
        }

        // ── Helper HTTP (présentation uniquement) ─────────────────────────────
        private async Task<IActionResult> HandleParametreResultAsync(OperationResult result)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.Succeeded)
            {
                TempData["Success"] = result.SuccessMessage;
                return RedirectToAction(nameof(Parametres));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            return View("Parametres", await _parametreService.GetViewModelAsync());
        }
    }
}
