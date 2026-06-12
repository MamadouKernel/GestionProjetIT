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
        public async Task<IActionResult> UploadLivrable(Guid projetId, PhaseProjet phase, TypeLivrable typeLivrable, IFormFile fichier, string? commentaire, string? version)
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

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Phase = phase,
                TypeLivrable = typeLivrable,
                NomDocument = fichier.FileName,
                CheminRelatif = path,
                DateDepot = DateTime.Now,
                DeposeParId = userId,
                Commentaire = commentaire ?? string.Empty,
                Version = version ?? string.Empty,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };

            _db.LivrablesProjets.Add(livrable);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPLOAD_LIVRABLE", "LivrableProjet", livrable.Id);

            TempData["Success"] = "Livrable déposé avec succès.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
        }

        // POST: Mettre à jour un livrable (statut, commentaire, etc.)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateLivrable(Guid id, Guid livrableId, string? commentaire, string? version)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var livrable = await _db.LivrablesProjets.FindAsync(livrableId);
            if (livrable == null || livrable.ProjetId != id)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, livrable.Phase))
                return Forbid();

            // Mettre à jour les champs fournis
            if (!string.IsNullOrWhiteSpace(commentaire))
                livrable.Commentaire = commentaire;

            if (!string.IsNullOrWhiteSpace(version))
                livrable.Version = version;

            livrable.DateModification = DateTime.Now;
            livrable.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MISE_A_JOUR_LIVRABLE", "LivrableProjet", livrable.Id,
                new { ProjetId = id, NomDocument = livrable.NomDocument },
                new { Version = livrable.Version, Commentaire = livrable.Commentaire });

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

        private async Task ReplaceGeneratedPlanningLivrableAsync(
            Projet projet,
            Guid userId,
            TypeLivrable typeLivrable,
            string fileName,
            byte[] content,
            string comment)
        {
            var existingLivrables = await _db.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id
                    && l.Phase == PhaseProjet.PlanificationValidation
                    && l.TypeLivrable == typeLivrable
                    && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLivrables)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var relativePath = await _fileStorage.SaveGeneratedFileAsync(
                content,
                fileName,
                Path.Combine("projets", projet.CodeProjet, "planification", "generated"));

            _db.LivrablesProjets.Add(new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.PlanificationValidation,
                TypeLivrable = typeLivrable,
                NomDocument = fileName,
                CheminRelatif = relativePath,
                DateDepot = DateTime.Now,
                DeposeParId = userId,
                Commentaire = comment,
                Version = $"auto-{DateTime.Now:yyyyMMddHHmmss}",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM"
            });
        }
    }
}
