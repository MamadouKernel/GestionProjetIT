using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // GET: Upload Livrable (formulaire)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> UploadLivrableForm(Guid projetId, PhaseProjet phase)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, phase))
                return Forbid();

            return PartialView("_UploadLivrableModal");
        }

        // POST: Upload Livrable
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> UploadLivrable(Guid projetId, PhaseProjet phase, TypeLivrable typeLivrable, IFormFile fichier, string? commentaire, string? version, [FromServices] ILivrableProjetService livrableService)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, phase))
                return Forbid();

            var userId = User.GetUserIdOrThrow();

            if (fichier == null || fichier.Length == 0)
            {
                TempData["Error"] = "Veuillez sélectionner un fichier.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".pptx", ".zip" };
            if (!_fileStorage.IsValidFileExtension(fichier.FileName, allowedExtensions))
            {
                TempData["Error"] = "Extension de fichier non autorisée.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
            }

            var subfolder = GetSubfolderForPhase(phase);
            var maxSize = 10 * 1024 * 1024; // 10 MB
            var path = await _fileStorage.SaveFileAsync(
                fichier,
                $"projets/{projet.CodeProjet}/{subfolder}",
                null,
                allowedExtensions,
                maxSize);

            await livrableService.DeposerAsync(
                projetId, phase, typeLivrable, fichier.FileName, path, userId, commentaire, version);

            TempData["Success"] = "Livrable déposé avec succès.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
        }

        // POST: Mettre à jour un livrable (statut, commentaire, etc.)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateLivrable(Guid id, Guid livrableId, string? commentaire, string? version, [FromServices] ILivrableProjetService livrableService)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var livrable = await _db.LivrablesProjets.FindAsync(livrableId);
            if (livrable == null || livrable.ProjetId != id)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, livrable.Phase))
                return Forbid();

            await livrableService.MettreAJourAsync(id, livrableId, commentaire, version);

            TempData["Success"] = "Livrable mis à jour avec succès.";
            return RedirectToAction(nameof(Details), new { id, tab = GetTabForPhase(livrable.Phase) });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers livrables
        // ══════════════════════════════════════════════════════════════════════

        private string GetSubfolderForPhase(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => "analyse",
                PhaseProjet.PlanificationValidation => "planification",
                PhaseProjet.ExecutionSuivi => "execution",
                PhaseProjet.UatMep => "uat",
                PhaseProjet.ClotureLeconsApprises => "cloture",
                _ => "autres"
            };
        }

        private string GetTabForPhase(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => "analyse",
                PhaseProjet.PlanificationValidation => "planification",
                PhaseProjet.ExecutionSuivi => "execution",
                PhaseProjet.UatMep => "uat",
                PhaseProjet.ClotureLeconsApprises => "cloture",
                _ => "synthese"
            };
        }

        private async Task<bool> CanManageLivrableAsync(Projet projet, PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => await CanManageAnalyseAsync(projet),
                PhaseProjet.PlanificationValidation => await CanManagePlanificationAsync(projet),
                PhaseProjet.ExecutionSuivi => await CanManageExecutionAsync(projet),
                PhaseProjet.UatMep => await CanManageUatAsync(projet),
                PhaseProjet.ClotureLeconsApprises => await CanManageClotureAsync(projet),
                _ => false
            };
        }
    }
}
