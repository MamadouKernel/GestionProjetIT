using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Parametres()
        {
            var parametres = await _db.ParametresSysteme
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Cle)
                .ToListAsync();

            ViewBag.DSIPrincipalId = parametres.FirstOrDefault(p => p.Cle == "DSIPrincipalId")?.Valeur;
            ViewBag.DSIDelegueId = parametres.FirstOrDefault(p => p.Cle == "DSIDelegueId")?.Valeur;
            ViewBag.DelaiInactiviteSessionMinutes = parametres.FirstOrDefault(p => p.Cle == "DelaiInactiviteSessionMinutes")?.Valeur;
            ViewBag.RepertoireStockageRacine = parametres.FirstOrDefault(p => p.Cle == "RepertoireStockageRacine")?.Valeur;
            ViewBag.TypesLivrables = parametres.FirstOrDefault(p => p.Cle == "TypesLivrables")?.Valeur;

            ViewBag.UtilisateursDsi = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                                (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View(parametres);
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
            await UpsertParametreSystemeAsync("DSIPrincipalId", dsiPrincipalId?.Trim() ?? string.Empty, "Identifiant du DSI principal");
            await UpsertParametreSystemeAsync("DSIDelegueId", dsiDelegueId?.Trim() ?? string.Empty, "Identifiant du délégué DSI");
            await UpsertParametreSystemeAsync("DelaiInactiviteSessionMinutes", (delaiInactiviteSessionMinutes ?? 30).ToString(), "Délai d'inactivité de session en minutes");
            await UpsertParametreSystemeAsync("RepertoireStockageRacine", repertoireStockageRacine?.Trim() ?? string.Empty, "Répertoire racine de stockage documentaire");
            await UpsertParametreSystemeAsync("TypesLivrables", typesLivrables?.Trim() ?? string.Empty, "Liste des types de livrables obligatoires");

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("ModificationParametre", "ParametreSysteme", Guid.Empty,
                null,
                new
                {
                    DSIPrincipalId = dsiPrincipalId,
                    DSIDelegueId = dsiDelegueId,
                    DelaiInactiviteSessionMinutes = delaiInactiviteSessionMinutes,
                    RepertoireStockageRacine = repertoireStockageRacine,
                    TypesLivrables = typesLivrables
                });

            TempData["Success"] = "Paramètres workflow enregistrés avec succès.";
            return RedirectToAction(nameof(Parametres));
        }

        [HttpGet]
        public async Task<IActionResult> GetParametre(Guid id)
        {
            var parametre = await _db.ParametresSysteme
                .FirstOrDefaultAsync(p => p.Id == id && !p.EstSupprime);

            if (parametre == null)
                return NotFound();

            return Json(new
            {
                id = parametre.Id,
                cle = parametre.Cle,
                valeur = parametre.Valeur,
                description = parametre.Description ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateParametre(string Cle, string Valeur, string Description)
        {
            if (string.IsNullOrWhiteSpace(Cle))
            {
                ModelState.AddModelError("Cle", "La clé est requise.");
            }

            if (string.IsNullOrWhiteSpace(Valeur))
            {
                ModelState.AddModelError("Valeur", "La valeur est requise.");
            }

            if (!string.IsNullOrWhiteSpace(Cle))
            {
                var cleExists = await _db.ParametresSysteme
                    .AnyAsync(p => p.Cle == Cle && !p.EstSupprime);
                if (cleExists)
                {
                    ModelState.AddModelError("Cle", "Cette clé existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                var parametre = new ParametreSysteme
                {
                    Id = Guid.NewGuid(),
                    Cle = Cle.Trim(),
                    Valeur = Valeur?.Trim() ?? string.Empty,
                    Description = Description?.Trim() ?? string.Empty,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null,
                    DateModification = null
                };

                _db.ParametresSysteme.Add(parametre);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("ModificationParametre", "ParametreSysteme", parametre.Id);

                TempData["Success"] = "Paramètre créé avec succès.";
                return RedirectToAction(nameof(Parametres));
            }

            return View("Parametres", await _db.ParametresSysteme.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateParametre(Guid id, string Cle, string Valeur, string Description)
        {
            var existingParametre = await _db.ParametresSysteme.FindAsync(id);
            if (existingParametre == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(Cle))
            {
                ModelState.AddModelError("Cle", "La clé est requise.");
            }

            if (string.IsNullOrWhiteSpace(Valeur))
            {
                ModelState.AddModelError("Valeur", "La valeur est requise.");
            }

            if (!string.IsNullOrWhiteSpace(Cle) && Cle != existingParametre.Cle)
            {
                var cleExists = await _db.ParametresSysteme
                    .AnyAsync(p => p.Cle == Cle && p.Id != id && !p.EstSupprime);
                if (cleExists)
                {
                    ModelState.AddModelError("Cle", "Cette clé existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                existingParametre.Cle = Cle.Trim();
                existingParametre.Valeur = Valeur?.Trim() ?? string.Empty;
                existingParametre.Description = Description?.Trim() ?? string.Empty;
                existingParametre.DateModification = DateTime.Now;
                existingParametre.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("ModificationParametre", "ParametreSysteme", existingParametre.Id,
                    new { AncienneCle = existingParametre.Cle, AncienneValeur = existingParametre.Valeur },
                    new { NouvelleCle = Cle, NouvelleValeur = Valeur });

                TempData["Success"] = "Paramètre modifié avec succès.";
                return RedirectToAction(nameof(Parametres));
            }

            return View("Parametres", await _db.ParametresSysteme.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteParametre(Guid id)
        {
            var parametre = await _db.ParametresSysteme.FindAsync(id);
            if (parametre == null)
                return NotFound();

            parametre.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_PARAMETRE", "ParametreSysteme", parametre.Id);

            TempData["Success"] = "Paramètre supprimé.";
            return RedirectToAction(nameof(Parametres));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTeamsWebhook(Guid? parametreId, string? webhookUrl)
        {
            var url = webhookUrl?.Trim() ?? string.Empty;

            if (parametreId.HasValue)
            {
                var param = await _db.ParametresSysteme.FindAsync(parametreId.Value);
                if (param != null)
                {
                    param.Valeur = url;
                    param.DateModification = DateTime.Now;
                    param.ModifiePar = _currentUserService.Matricule;
                    await _db.SaveChangesAsync();
                    await _auditService.LogActionAsync("MAJ_TEAMS_WEBHOOK", "ParametreSysteme", param.Id);
                }
            }
            else
            {
                var param = new ParametreSysteme
                {
                    Id = Guid.NewGuid(),
                    Cle = "TeamsWebhookUrl",
                    Valeur = url,
                    Description = "URL du webhook entrant Microsoft Teams pour les notifications",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };
                _db.ParametresSysteme.Add(param);
                await _db.SaveChangesAsync();
                await _auditService.LogActionAsync("CREATION_TEAMS_WEBHOOK", "ParametreSysteme", param.Id);
            }

            TempData["Success"] = string.IsNullOrWhiteSpace(url)
                ? "Webhook Teams supprimé."
                : "Webhook Teams enregistré avec succès.";
            return RedirectToAction(nameof(Parametres));
        }
    }
}
