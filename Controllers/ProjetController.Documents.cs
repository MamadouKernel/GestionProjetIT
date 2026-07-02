using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // POST: Générer Word Charte (version complète)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererCharteCompletWord(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.Demandeur)
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.ChefProjet)
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.PartiesPrenantes.Where(p => !p.EstSupprime))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (projet.CharteProjet == null)
            {
                TempData["Error"] = "La charte de projet n'a pas encore été créée.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            try
            {
                var wordBytes = await _wordService.GenerateCharteProjetWordAsync(projet.CharteProjet);
                var fileName = $"CharteProjet_Complet_{projet.CodeProjet}_{DateTime.UtcNow:yyyyMMdd}.docx";
                return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du document Word: {ex.Message}";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }
        }

        // POST: Générer Word Fiche Projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererFicheProjetWord(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (projet.FicheProjet == null)
            {
                TempData["Error"] = "La fiche projet n'a pas encore été créée.";
                return RedirectToAction(nameof(FicheProjet), new { id });
            }

            try
            {
                var wordBytes = await _wordService.GenerateFicheProjetWordAsync(projet.FicheProjet);
                var fileName = $"FicheProjet_{projet.CodeProjet}_{DateTime.UtcNow:yyyyMMdd}.docx";
                return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du document Word: {ex.Message}";
                return RedirectToAction(nameof(FicheProjet), new { id });
            }
        }

        // POST: Générer Excel Portefeuille
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererPortefeuilleExcel()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var portefeuille = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuille == null)
            {
                TempData["Error"] = "Aucun portefeuille actif trouvé.";
                return RedirectToAction(nameof(Portefeuille));
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Where(p => !p.EstSupprime && (p.PortefeuilleProjetId == portefeuille.Id || portefeuille == null))
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var excelBytes = await _excelService.GeneratePortefeuilleProjetsExcelAsync(portefeuille, projets);
                var fileName = $"PortefeuilleProjets_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du fichier Excel: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer PDF Portefeuille
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererPortefeuillePdf()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var portefeuille = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuille == null)
            {
                TempData["Error"] = "Aucun portefeuille actif trouvé.";
                return RedirectToAction(nameof(Portefeuille));
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Where(p => !p.EstSupprime && p.PortefeuilleProjetId == portefeuille.Id)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var pdfBytes = await _pdfService.GeneratePortefeuilleProjetsPdfAsync(portefeuille, projets);
                var fileName = $"PortefeuilleProjets_{DateTime.UtcNow:yyyyMMdd}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du fichier PDF: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer Rapport DSI/DG PDF
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererRapportDSIDGPdf()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.DemandeProjet)
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var pdfBytes = await _pdfService.GenerateRapportDSIDGPdfAsync(projets);
                var fileName = $"Rapport_DSI_DG_{DateTime.UtcNow:yyyyMMdd}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du rapport PDF: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer Rapport DSI/DG Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererRapportDSIDGExcel()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.DemandeProjet)
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var excelBytes = await _excelService.GenerateRapportDSIDGExcelAsync(projets);
                var fileName = $"Rapport_DSI_DG_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du rapport Excel: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }
    }
}
