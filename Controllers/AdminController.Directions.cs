using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Directions(string? recherche = null, int page = 1, int pageSize = 20)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Directions
                .Include(d => d.DSI)
                .Where(d => !d.EstSupprime)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => d.Libelle.Contains(recherche) || d.Code.Contains(recherche));

            query = query.OrderBy(d => d.Libelle);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View(paged.Items);
        }

        private string GenerateCodeFromLibelle(string libelle)
        {
            if (string.IsNullOrWhiteSpace(libelle))
                return string.Empty;

            var libelleNormalise = libelle.Trim().ToLowerInvariant()
                .Replace("'", " ")
                .Replace("-", " ")
                .Replace("  ", " ")
                .Trim();

            if (libelleNormalise.Contains("direction") && libelleNormalise.Contains("exploitation"))
            {
                return "DEX";
            }

            var motsIgnorer = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "de", "des", "du", "d'", "le", "la", "les", "un", "une",
                "et", "ou", "à", "au", "aux", "en", "pour", "par", "avec",
                "sans", "sous", "sur", "dans", "entre", "vers"
            };

            var texteNettoye = libelle
                .Replace("'", " ")
                .Replace("-", " ")
                .Replace("  ", " ")
                .Trim();

            var mots = texteNettoye.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var code = new System.Text.StringBuilder();
            foreach (var mot in mots)
            {
                var motNettoye = mot.Trim();
                if (motNettoye.Length >= 2 && !motsIgnorer.Contains(motNettoye))
                {
                    code.Append(char.ToUpperInvariant(motNettoye[0]));
                }
            }

            return code.ToString();
        }

        [HttpGet]
        public async Task<IActionResult> GetDirectionCode(Guid id)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            return Json(new { code = direction.Code ?? "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDirection(string Code, string Libelle, string? DSIId)
        {
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            string codeFinal = Code?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(codeFinal) && !string.IsNullOrWhiteSpace(Libelle))
            {
                codeFinal = GenerateCodeFromLibelle(Libelle);
            }

            if (string.IsNullOrWhiteSpace(codeFinal))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            if (!string.IsNullOrWhiteSpace(codeFinal))
            {
                var codeExists = await _db.Directions
                    .AnyAsync(d => d.Code == codeFinal && !d.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de direction existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                var direction = new Direction
                {
                    Id = Guid.NewGuid(),
                    Code = codeFinal,
                    Libelle = Libelle.Trim(),
                    EstActive = estActive,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null,
                    DateModification = null
                };

                if (!string.IsNullOrWhiteSpace(DSIId) && Guid.TryParse(DSIId, out var dsiGuid))
                {
                    direction.DSIId = dsiGuid;
                }

                _db.Directions.Add(direction);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CreationDirection", "Direction", direction.Id,
                    null,
                    new { Code = direction.Code, Libelle = direction.Libelle });

                TempData["Success"] = "Direction créée avec succès.";
                return RedirectToAction(nameof(Directions));
            }

            var directions = await _db.Directions
                .Include(d => d.DSI)
                .Where(d => !d.EstSupprime)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View("Directions", directions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDirection(Guid id, string Code, string Libelle, string? DSIId)
        {
            var existingDirection = await _db.Directions.FindAsync(id);
            if (existingDirection == null)
                return NotFound();

            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (string.IsNullOrWhiteSpace(Code))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            if (!string.IsNullOrWhiteSpace(Code) && Code != existingDirection.Code)
            {
                var codeExists = await _db.Directions
                    .AnyAsync(d => d.Code == Code && d.Id != id && !d.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de direction existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                existingDirection.Code = Code.Trim();
                existingDirection.Libelle = Libelle.Trim();
                existingDirection.EstActive = estActive;

                if (!string.IsNullOrWhiteSpace(DSIId) && Guid.TryParse(DSIId, out var dsiGuid))
                {
                    existingDirection.DSIId = dsiGuid;
                }
                else
                {
                    existingDirection.DSIId = null;
                }

                existingDirection.DateModification = DateTime.Now;
                existingDirection.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("ModificationDirection", "Direction", existingDirection.Id,
                    new { AncienCode = existingDirection.Code, AncienLibelle = existingDirection.Libelle },
                    new { NouveauCode = Code, NouveauLibelle = Libelle });

                TempData["Success"] = "Direction modifiée avec succès.";
                return RedirectToAction(nameof(Directions));
            }

            var directions = await _db.Directions
                .Include(d => d.DSI)
                .Where(d => !d.EstSupprime)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View("Directions", directions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDirection(Guid id)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            direction.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_DIRECTION", "Direction", direction.Id);

            TempData["Success"] = "Direction supprimée.";
            return RedirectToAction(nameof(Directions));
        }
    }
}
