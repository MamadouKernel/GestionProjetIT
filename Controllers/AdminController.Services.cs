using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Services(string? recherche = null, Guid? directionId = null, int page = 1, int pageSize = 20)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Services
                .Include(s => s.Direction)
                .Where(s => !s.EstSupprime)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(s => s.Libelle.Contains(recherche) || s.Code.Contains(recherche));

            if (directionId.HasValue)
                query = query.Where(s => s.DirectionId == directionId.Value);

            query = query.OrderBy(s => s.Libelle);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber  = paged.PageNumber;
            ViewBag.TotalPages  = paged.TotalPages;
            ViewBag.TotalCount  = paged.TotalCount;
            ViewBag.PageSize    = paged.PageSize;
            ViewBag.Recherche   = recherche;
            ViewBag.SelectedDirectionId = directionId;

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();
            ViewBag.Directions = directions;

            var vm = new ServicesListViewModel
            {
                Services            = paged.Items,
                Directions          = directions,
                TotalCount          = paged.TotalCount,
                PageNumber          = paged.PageNumber,
                TotalPages          = paged.TotalPages,
                PageSize            = paged.PageSize,
                Recherche           = recherche,
                SelectedDirectionId = directionId
            };

            return View(vm);
        }

        private async Task<string> GenerateServiceCodeAsync(string libelle, Guid? directionId)
        {
            if (string.IsNullOrWhiteSpace(libelle) || !directionId.HasValue)
                return string.Empty;

            var direction = await _db.Directions.FindAsync(directionId.Value);
            if (direction == null || string.IsNullOrWhiteSpace(direction.Code))
                return string.Empty;

            var initialesService = GenerateCodeFromLibelle(libelle);

            return $"{direction.Code}-{initialesService}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(string Code, string Libelle, string DirectionId)
        {
            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            if (string.IsNullOrWhiteSpace(DirectionId) || !Guid.TryParse(DirectionId, out var directionGuid))
            {
                ModelState.AddModelError("DirectionId", "La direction est requise.");
            }

            string codeFinal = Code?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(codeFinal) && !string.IsNullOrWhiteSpace(Libelle) && Guid.TryParse(DirectionId, out directionGuid))
            {
                codeFinal = await GenerateServiceCodeAsync(Libelle, directionGuid);
            }

            if (string.IsNullOrWhiteSpace(codeFinal))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            if (!string.IsNullOrWhiteSpace(codeFinal))
            {
                var codeExists = await _db.Services
                    .AnyAsync(s => s.Code == codeFinal && !s.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de service existe déjà.");
                }
            }

            if (ModelState.IsValid && Guid.TryParse(DirectionId, out directionGuid))
            {
                var service = new Service
                {
                    Id = Guid.NewGuid(),
                    Code = codeFinal,
                    Libelle = Libelle.Trim(),
                    DirectionId = directionGuid,
                    EstActive = true,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };

                _db.Services.Add(service);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_SERVICE", "Service", service.Id,
                    null,
                    new { Code = service.Code, Libelle = service.Libelle, DirectionId = directionGuid });

                TempData["Success"] = "Service créé avec succès.";
                return RedirectToAction(nameof(Services));
            }

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View("Services", await _db.Services.Include(s => s.Direction).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateService(Guid id, string Code, string Libelle, string DirectionId)
        {
            var existingService = await _db.Services.FindAsync(id);
            if (existingService == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(Code))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            if (string.IsNullOrWhiteSpace(DirectionId) || !Guid.TryParse(DirectionId, out var directionGuid))
            {
                ModelState.AddModelError("DirectionId", "La direction est requise.");
            }

            if (!string.IsNullOrWhiteSpace(Code) && Code != existingService.Code)
            {
                var codeExists = await _db.Services
                    .AnyAsync(s => s.Code == Code && s.Id != id && !s.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de service existe déjà.");
                }
            }

            if (ModelState.IsValid && Guid.TryParse(DirectionId, out directionGuid))
            {
                existingService.Code = Code.Trim();
                existingService.Libelle = Libelle.Trim();
                existingService.DirectionId = directionGuid;
                existingService.DateModification = DateTime.Now;
                existingService.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_SERVICE", "Service", existingService.Id,
                    new { AncienCode = existingService.Code, AncienLibelle = existingService.Libelle },
                    new { NouveauCode = Code, NouveauLibelle = Libelle });

                TempData["Success"] = "Service modifié avec succès.";
                return RedirectToAction(nameof(Services));
            }

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View("Services", await _db.Services.Include(s => s.Direction).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(Guid id)
        {
            var service = await _db.Services.FindAsync(id);
            if (service == null)
                return NotFound();

            service.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_SERVICE", "Service", service.Id);

            TempData["Success"] = "Service supprimé.";
            return RedirectToAction(nameof(Services));
        }
    }
}
