using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Delegations(
            string? tab = "dsi",
            string? rechercheDsi = null,
            string? rechercheChef = null,
            int pageDsi = 1, int pageChef = 1,
            int pageSize = 15)
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();
            pageDsi   = Math.Max(1, pageDsi);
            pageChef  = Math.Max(1, pageChef);
            pageSize  = Math.Clamp(pageSize, 5, 100);

            var delegationsDsiQuery = _db.DelegationsValidationDSI
                .Include(d => d.DSI)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime)
                .AsQueryable();

            if (!hasFullAdminScope)
                delegationsDsiQuery = delegationsDsiQuery.Where(d => d.DSIId == userId || d.DelegueId == userId);

            if (!string.IsNullOrWhiteSpace(rechercheDsi))
                delegationsDsiQuery = delegationsDsiQuery.Where(d =>
                    (d.DSI != null && (d.DSI.Nom.Contains(rechercheDsi) || d.DSI.Prenoms.Contains(rechercheDsi))) ||
                    (d.Delegue != null && (d.Delegue.Nom.Contains(rechercheDsi) || d.Delegue.Prenoms.Contains(rechercheDsi))));

            var pagedDsi = await delegationsDsiQuery.OrderByDescending(d => d.DateDebut).ToPagedResultAsync(pageDsi, pageSize);
            var delegationsDSI = pagedDsi.Items;

            ViewBag.PageNumberDsi = pagedDsi.PageNumber;
            ViewBag.TotalPagesDsi = pagedDsi.TotalPages;
            ViewBag.TotalCountDsi = pagedDsi.TotalCount;
            ViewBag.PageSizeDsi   = pagedDsi.PageSize;
            ViewBag.RechercheDsi  = rechercheDsi;

            var dsis = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();
            ViewBag.DSIs = dsis;

            var deleguesDsi = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ResponsableSolutionsIT))
                .OrderBy(u => u.Nom)
                .ToListAsync();
            ViewBag.DeleguesDSI = deleguesDsi;

            IQueryable<DelegationChefProjet> queryChefProjet = _db.DelegationsChefProjet
                .Include(d => d.Delegant)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime)
                .AsQueryable();

            if (!hasFullAdminScope)
                queryChefProjet = queryChefProjet.Where(d => d.DelegantId == userId || d.DelegueId == userId);

            if (!string.IsNullOrWhiteSpace(rechercheChef))
                queryChefProjet = queryChefProjet.Where(d =>
                    (d.Delegant != null && (d.Delegant.Nom.Contains(rechercheChef) || d.Delegant.Prenoms.Contains(rechercheChef))) ||
                    (d.Delegue  != null && (d.Delegue.Nom.Contains(rechercheChef)  || d.Delegue.Prenoms.Contains(rechercheChef))));

            var pagedChef = await queryChefProjet.Include(d => d.Projet).OrderByDescending(d => d.DateDebut).ToPagedResultAsync(pageChef, pageSize);
            var delegationsChefProjet = pagedChef.Items;

            ViewBag.PageNumberChef = pagedChef.PageNumber;
            ViewBag.TotalPagesChef = pagedChef.TotalPages;
            ViewBag.TotalCountChef = pagedChef.TotalCount;
            ViewBag.PageSizeChef   = pagedChef.PageSize;
            ViewBag.RechercheChef  = rechercheChef;

            var projets = await _db.Projets
                .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
                .OrderByDescending(p => p.DateCreation)
                .ToListAsync();
            ViewBag.Projets = projets;

            var delegants = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                           (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)) &&
                           (hasFullAdminScope || u.Id == userId))
                .OrderBy(u => u.Nom)
                .ToListAsync();
            ViewBag.Delegants = delegants;

            var deleguesChefProjet = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                .OrderBy(u => u.Nom)
                .ToListAsync();
            ViewBag.DeleguesChefProjet = deleguesChefProjet;

            ViewBag.CurrentUserId = userId;
            ViewBag.CanAdminDelegations = hasFullAdminScope;
            ViewBag.ActiveTab = tab ?? "dsi";

            var viewModel = new DelegationsPageViewModel
            {
                DelegationsDSI          = delegationsDSI,
                DelegationsChefProjet   = delegationsChefProjet,
                DSIs                    = dsis,
                DeleguesDSI             = deleguesDsi,
                Delegants               = delegants,
                DeleguesChefProjet      = deleguesChefProjet,
                Projets                 = projets,
                ActiveTab               = tab ?? "dsi",
                CanAdminDelegations     = hasFullAdminScope,
                CurrentUserId           = userId,
                PageNumberDsi           = pagedDsi.PageNumber,
                TotalPagesDsi           = pagedDsi.TotalPages,
                TotalCountDsi           = pagedDsi.TotalCount,
                PageSizeDsi             = pagedDsi.PageSize,
                RechercheDsi            = rechercheDsi,
                PageNumberChef          = pagedChef.PageNumber,
                TotalPagesChef          = pagedChef.TotalPages,
                TotalCountChef          = pagedChef.TotalCount,
                PageSizeChef            = pagedChef.PageSize,
                RechercheChef           = rechercheChef
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetDelegation(Guid id)
        {
            var delegation = await _db.DelegationsValidationDSI
                .FirstOrDefaultAsync(d => d.Id == id && !d.EstSupprime);

            if (delegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await HasFullAdminScopeAsync() &&
                delegation.DSIId != userId &&
                delegation.DelegueId != userId)
            {
                return Forbid();
            }

            return Json(new
            {
                id = delegation.Id,
                dsiId = delegation.DSIId.ToString(),
                delegueId = delegation.DelegueId.ToString(),
                dateDebut = delegation.DateDebut.ToString("yyyy-MM-ddTHH:mm:ss"),
                dateFin = delegation.DateFin.ToString("yyyy-MM-ddTHH:mm:ss"),
                estActive = delegation.EstActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelegation(string DSIId, string DelegueId, DateTime DateDebut, DateTime DateFin)
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (string.IsNullOrWhiteSpace(DSIId) || !Guid.TryParse(DSIId, out var dsiGuid))
            {
                ModelState.AddModelError("DSIId", "Le DSI est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            if (DateDebut >= DateFin)
            {
                ModelState.AddModelError("DateFin", "La date de fin doit être postérieure à la date de début.");
            }

            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(DSIId, out dsiGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                if (!hasFullAdminScope && dsiGuid != userId)
                {
                    ModelState.AddModelError("DSIId", "Vous ne pouvez créer une délégation DSI que pour vous-même.");
                }
            }

            if (ModelState.IsValid && Guid.TryParse(DSIId, out dsiGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                var delegation = new DelegationValidationDSI
                {
                    Id = Guid.NewGuid(),
                    DSIId = dsiGuid,
                    DelegueId = delegueGuid,
                    DateDebut = DateDebut,
                    DateFin = DateFin,
                    EstActive = estActive,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null,
                    DateModification = null
                };

                _db.DelegationsValidationDSI.Add(delegation);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CreationDelegationDSI", "DelegationValidationDSI", delegation.Id,
                    null,
                    new { DSIId = delegation.DSIId, DelegueId = delegation.DelegueId, DateDebut = delegation.DateDebut, DateFin = delegation.DateFin });

                TempData["Success"] = "Délégation DSI créée avec succès.";
                return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
            }

            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDelegation(Guid id, string DSIId, string DelegueId, DateTime DateDebut, DateTime DateFin)
        {
            var existingDelegation = await _db.DelegationsValidationDSI.FindAsync(id);
            if (existingDelegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await HasFullAdminScopeAsync() && existingDelegation.DSIId != userId)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(DSIId) || !Guid.TryParse(DSIId, out var dsiGuid))
            {
                ModelState.AddModelError("DSIId", "Le DSI est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            if (DateDebut >= DateFin)
            {
                ModelState.AddModelError("DateFin", "La date de fin doit être postérieure à la date de début.");
            }

            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(DSIId, out dsiGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                existingDelegation.DSIId = dsiGuid;
                existingDelegation.DelegueId = delegueGuid;
                existingDelegation.DateDebut = DateDebut;
                existingDelegation.DateFin = DateFin;
                existingDelegation.EstActive = estActive;
                existingDelegation.DateModification = DateTime.Now;
                existingDelegation.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("ModificationDelegationDSI", "DelegationValidationDSI", existingDelegation.Id);

                TempData["Success"] = "Délégation DSI modifiée avec succès.";
                return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
            }

            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelegation(Guid id)
        {
            var delegation = await _db.DelegationsValidationDSI.FindAsync(id);
            if (delegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await HasFullAdminScopeAsync() && delegation.DSIId != userId)
            {
                return Forbid();
            }

            delegation.EstSupprime = true;
            delegation.EstActive = false;
            delegation.DateModification = DateTime.Now;
            delegation.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("ClotureDelegationDSI", "DelegationValidationDSI", delegation.Id,
                new { DSIId = delegation.DSIId, DelegueId = delegation.DelegueId, DateDebut = delegation.DateDebut, DateFin = delegation.DateFin });

            TempData["Success"] = "Délégation DSI clôturée.";
            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        public async Task<IActionResult> DelegationsChefProjet()
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            IQueryable<DelegationChefProjet> query = _db.DelegationsChefProjet
                .Include(d => d.Delegant)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime);

            if (!hasFullAdminScope)
            {
                query = query.Where(d => d.DelegantId == userId || d.DelegueId == userId);
            }

            var delegations = await query
                .Include(d => d.Projet)
                .OrderByDescending(d => d.DateDebut)
                .ToListAsync();

            var vm = new DelegationsChefProjetPageViewModel
            {
                Delegations = delegations,
                Projets = await _db.Projets
                    .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
                    .OrderByDescending(p => p.DateCreation)
                    .ToListAsync(),
                Delegants = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime &&
                                u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                                                             (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)) &&
                                (hasFullAdminScope || u.Id == userId))
                    .OrderBy(u => u.Nom)
                    .ToListAsync(),
                Delegues = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                    .OrderBy(u => u.Nom)
                    .ToListAsync(),
                CurrentUserId = userId,
                CanAdminDelegations = hasFullAdminScope
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetDelegationChefProjet(Guid id)
        {
            var delegation = await _db.DelegationsChefProjet
                .FirstOrDefaultAsync(d => d.Id == id && !d.EstSupprime);

            if (delegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (!hasFullAdminScope && delegation.DelegantId != userId)
                return Forbid();

            return Json(new
            {
                id = delegation.Id,
                projetId = delegation.ProjetId.ToString(),
                delegantId = delegation.DelegantId.ToString(),
                delegueId = delegation.DelegueId.ToString(),
                dateDebut = delegation.DateDebut.ToString("yyyy-MM-ddTHH:mm:ss"),
                dateFin = delegation.DateFin?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "",
                estActive = delegation.EstActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelegationChefProjet(string ProjetId, string DelegantId, string DelegueId, DateTime DateDebut)
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (string.IsNullOrWhiteSpace(ProjetId) || !Guid.TryParse(ProjetId, out var projetGuid))
            {
                ModelState.AddModelError("ProjetId", "Le projet est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegantId) || !Guid.TryParse(DelegantId, out var delegantGuid))
            {
                ModelState.AddModelError("DelegantId", "Le délégant est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            if (Guid.TryParse(ProjetId, out projetGuid))
            {
                var projet = await _db.Projets.FindAsync(projetGuid);
                if (projet == null || projet.EstSupprime)
                {
                    ModelState.AddModelError("ProjetId", "Le projet sélectionné n'existe pas.");
                }
                else if (projet.StatutProjet == StatutProjet.Cloture)
                {
                    ModelState.AddModelError("ProjetId", "Impossible de créer une délégation pour un projet clôturé.");
                }
            }

            if (Guid.TryParse(DelegantId, out delegantGuid))
            {
                var delegant = await _db.Utilisateurs.FindAsync(delegantGuid);
                if (delegant == null || !await _db.UtilisateurRoles.AnyAsync(ur =>
                        ur.UtilisateurId == delegantGuid &&
                        !ur.EstSupprime &&
                        (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                {
                    ModelState.AddModelError("DelegantId", "Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
                }

                if (!hasFullAdminScope && delegantGuid != userId)
                {
                    ModelState.AddModelError("DelegantId", "Vous ne pouvez créer une délégation que pour vous-même.");
                }
            }

            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(ProjetId, out projetGuid) && Guid.TryParse(DelegantId, out delegantGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                var delegationExistante = await _db.DelegationsChefProjet
                    .AnyAsync(d => d.ProjetId == projetGuid &&
                                 d.EstActive &&
                                 d.DateFin == null &&
                                 !d.EstSupprime);

                if (delegationExistante)
                {
                    ModelState.AddModelError("ProjetId", "Une délégation active existe déjà pour ce projet.");
                }
                else
                {
                    var delegation = new DelegationChefProjet
                    {
                        Id = Guid.NewGuid(),
                        ProjetId = projetGuid,
                        DelegantId = delegantGuid,
                        DelegueId = delegueGuid,
                        DateDebut = DateDebut,
                        DateFin = null,
                        EstActive = estActive,
                        DateCreation = DateTime.Now,
                        CreePar = _currentUserService.Matricule ?? "SYSTEM",
                        EstSupprime = false,
                        ModifiePar = null,
                        DateModification = null
                    };

                    _db.DelegationsChefProjet.Add(delegation);
                    await _db.SaveChangesAsync();

                    await _auditService.LogActionAsync("CREATION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);

                    TempData["Success"] = "Délégation ChefProjet créée avec succès.";
                    return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
                }
            }

            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDelegationChefProjet(Guid id, string ProjetId, string DelegantId, string DelegueId, DateTime DateDebut)
        {
            var existingDelegation = await _db.DelegationsChefProjet.FindAsync(id);
            if (existingDelegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (!hasFullAdminScope && existingDelegation.DelegantId != userId)
                return Forbid();

            if (string.IsNullOrWhiteSpace(ProjetId) || !Guid.TryParse(ProjetId, out var projetGuid))
            {
                ModelState.AddModelError("ProjetId", "Le projet est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegantId) || !Guid.TryParse(DelegantId, out var delegantGuid))
            {
                ModelState.AddModelError("DelegantId", "Le délégant est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            if (Guid.TryParse(ProjetId, out projetGuid))
            {
                var projet = await _db.Projets.FindAsync(projetGuid);
                if (projet == null || projet.EstSupprime)
                {
                    ModelState.AddModelError("ProjetId", "Le projet sélectionné n'existe pas.");
                }
                else if (projet.StatutProjet == StatutProjet.Cloture)
                {
                    ModelState.AddModelError("ProjetId", "Impossible de modifier une délégation pour un projet clôturé.");
                }
            }

            if (Guid.TryParse(DelegantId, out delegantGuid))
            {
                var delegant = await _db.Utilisateurs.FindAsync(delegantGuid);
                if (delegant == null || !await _db.UtilisateurRoles.AnyAsync(ur =>
                        ur.UtilisateurId == delegantGuid &&
                        !ur.EstSupprime &&
                        (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                {
                    ModelState.AddModelError("DelegantId", "Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
                }

                if (!hasFullAdminScope && delegantGuid != userId)
                {
                    ModelState.AddModelError("DelegantId", "Vous ne pouvez modifier que vos propres délégations.");
                }
            }

            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(ProjetId, out projetGuid) && Guid.TryParse(DelegantId, out delegantGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                var autreDelegation = await _db.DelegationsChefProjet
                    .AnyAsync(d => d.ProjetId == projetGuid &&
                                 d.Id != id &&
                                 d.EstActive &&
                                 d.DateFin == null &&
                                 !d.EstSupprime);

                if (autreDelegation)
                {
                    ModelState.AddModelError("ProjetId", "Une autre délégation active existe déjà pour ce projet.");
                }
                else
                {
                    existingDelegation.ProjetId = projetGuid;
                    existingDelegation.DelegantId = delegantGuid;
                    existingDelegation.DelegueId = delegueGuid;
                    existingDelegation.DateDebut = DateDebut;
                    existingDelegation.EstActive = estActive;
                    existingDelegation.DateModification = DateTime.Now;
                    existingDelegation.ModifiePar = _currentUserService.Matricule;

                    await _db.SaveChangesAsync();

                    await _auditService.LogActionAsync("MODIFICATION_DELEGATION_CHEFPROJET", "DelegationChefProjet", existingDelegation.Id);

                    TempData["Success"] = "Délégation ChefProjet modifiée avec succès.";
                    return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
                }
            }

            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelegationChefProjet(Guid id)
        {
            var delegation = await _db.DelegationsChefProjet.FindAsync(id);
            if (delegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (!hasFullAdminScope && delegation.DelegantId != userId)
                return Forbid();

            delegation.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);

            TempData["Success"] = "Délégation ChefProjet supprimée.";
            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }
    }
}
