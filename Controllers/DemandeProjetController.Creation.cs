using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.DemandeProjet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // GET: Récupérer les directeurs métier d'une direction (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetDirecteursMetierByDirection(Guid? directionId)
        {
            if (!directionId.HasValue)
                return Json(new List<object>());

            var directeursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.DirectionId == directionId.Value &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .Select(u => new
                {
                    id = u.Id,
                    nom = u.Nom,
                    prenoms = u.Prenoms,
                    text = $"{u.Nom} {u.Prenoms}"
                })
                .ToListAsync();

            return Json(directeursMetier);
        }

        // GET: Créer nouvelle demande
        [Authorize]
        public async Task<IActionResult> Create()
        {
            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Create"))
                return Forbid();

            Guid? preSelectedDirectionId      = null;
            Guid? preSelectedDirecteurMetierId = null;
            bool isReadOnly = false;

            if (!canManageDemandes)
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.DirectionId.HasValue == true)
                {
                    preSelectedDirectionId = user.DirectionId;
                    isReadOnly = true;

                    var directeurMetier = await _db.Utilisateurs
                        .Include(u => u.UtilisateurRoles)
                        .Where(u => !u.EstSupprime &&
                                   u.DirectionId == user.DirectionId &&
                                   u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                        .FirstOrDefaultAsync();

                    if (directeurMetier != null)
                        preSelectedDirecteurMetierId = directeurMetier.Id;
                }
            }

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            var directeursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            var vm = new DemandeProjetCreateViewModel
            {
                Demande = new Domain.Models.DemandeProjet(),
                Directions = new SelectList(directions, "Id", "Libelle", preSelectedDirectionId),
                DirecteursMetier = directeursMetier.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Nom} {u.Prenoms}",
                    Selected = preSelectedDirecteurMetierId == u.Id
                }),
                IsReadOnly = isReadOnly,
                PreSelectedDirectionId = preSelectedDirectionId,
                PreSelectedDirecteurMetierId = preSelectedDirecteurMetierId
            };

            return View(vm);
        }

        // POST: Créer nouvelle demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Create(DemandeProjet Demande, IFormFile? cahierCharges, List<IFormFile>? annexes)
        {
            const string pfx = "Demande.";
            ModelState.Remove(pfx + nameof(Demande.Projet));
            ModelState.Remove(pfx + nameof(Demande.Demandeur));
            ModelState.Remove(pfx + nameof(Demande.DemandeurId));
            ModelState.Remove(pfx + nameof(Demande.CommentaireDSI));
            ModelState.Remove(pfx + nameof(Demande.CommentaireDirecteurMetier));
            ModelState.Remove(pfx + nameof(Demande.CahierChargesPath));
            ModelState.Remove(pfx + nameof(Demande.StatutDemande));
            ModelState.Remove(pfx + nameof(Demande.DateSoumission));
            ModelState.Remove(pfx + nameof(Demande.DateValidationDM));
            ModelState.Remove(pfx + nameof(Demande.DateValidationDSI));
            ModelState.Remove(pfx + nameof(Demande.CreePar));
            ModelState.Remove(pfx + nameof(Demande.DateCreation));
            ModelState.Remove(pfx + nameof(Demande.ModifiePar));
            ModelState.Remove(pfx + nameof(Demande.DateModification));
            ModelState.Remove(pfx + nameof(Demande.DirectionId));
            ModelState.Remove(pfx + nameof(Demande.DirecteurMetierId));
            ModelState.Remove(pfx + nameof(Demande.Direction));
            ModelState.Remove(pfx + nameof(Demande.DirecteurMetier));
            ModelState.Remove(pfx + nameof(Demande.Titre));
            ModelState.Remove(pfx + nameof(Demande.Description));
            ModelState.Remove(pfx + nameof(Demande.Contexte));
            ModelState.Remove(pfx + nameof(Demande.Objectifs));

            if (string.IsNullOrWhiteSpace(Demande.Titre))
                ModelState.AddModelError(pfx + nameof(Demande.Titre), "Le titre du projet est requis.");

            if (string.IsNullOrWhiteSpace(Demande.Description))
                ModelState.AddModelError(pfx + nameof(Demande.Description), "La description est requise.");

            if (string.IsNullOrWhiteSpace(Demande.Contexte))
                ModelState.AddModelError(pfx + nameof(Demande.Contexte), "Le contexte est requis.");

            if (string.IsNullOrWhiteSpace(Demande.Objectifs))
                ModelState.AddModelError(pfx + nameof(Demande.Objectifs), "Les objectifs sont requis.");

            if (!Demande.DirectionId.HasValue || Demande.DirectionId.Value == Guid.Empty)
                ModelState.AddModelError(pfx + nameof(Demande.DirectionId), "La direction est requise.");

            if (Demande.DirecteurMetierId == Guid.Empty)
                ModelState.AddModelError(pfx + nameof(Demande.DirecteurMetierId), "Le directeur métier est requis.");

            if (ModelState.IsValid)
            {
                var userId = User.GetUserIdOrThrow();

                Demande.DateValidationDM  = null;
                Demande.DateValidationDSI = null;
                Demande.Id              = Guid.NewGuid();
                Demande.DemandeurId     = userId;
                Demande.StatutDemande   = StatutDemande.EnAttenteValidationDirecteurMetier;
                Demande.DateSoumission  = DateTime.Now;
                Demande.DateCreation    = DateTime.Now;
                Demande.CreePar         = _currentUserService.Matricule;

                Demande.Titre                       = Demande.Titre ?? string.Empty;
                Demande.Description                 = Demande.Description ?? string.Empty;
                Demande.Contexte                    = Demande.Contexte ?? string.Empty;
                Demande.Objectifs                   = Demande.Objectifs ?? string.Empty;
                Demande.AvantagesAttendus           = Demande.AvantagesAttendus ?? string.Empty;
                Demande.Perimetre                   = Demande.Perimetre ?? string.Empty;
                Demande.CommentaireDirecteurMetier  = Demande.CommentaireDirecteurMetier ?? string.Empty;
                Demande.CommentaireDSI              = Demande.CommentaireDSI ?? string.Empty;
                Demande.CahierChargesPath           = Demande.CahierChargesPath ?? string.Empty;

                if (cahierCharges != null && cahierCharges.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    var maxSize = 10 * 1024 * 1024;
                    var savedPath = await _fileStorage.SaveFileAsync(cahierCharges, "demandes", Demande.Id.ToString(), allowedExtensions, maxSize);
                    if (!string.IsNullOrEmpty(savedPath))
                        Demande.CahierChargesPath = savedPath;
                }

                if (string.IsNullOrEmpty(Demande.CahierChargesPath))
                    Demande.CahierChargesPath = string.Empty;

                _db.DemandesProjets.Add(Demande);
                await _db.SaveChangesAsync();

                if (annexes != null && annexes.Any())
                {
                    foreach (var annexe in annexes)
                    {
                        if (annexe.Length > 0)
                        {
                            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                            var maxSize = 10 * 1024 * 1024;
                            var path = await _fileStorage.SaveFileAsync(annexe, "demandes", Demande.Id.ToString(), allowedExtensions, maxSize);

                            _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
                            {
                                Id               = Guid.NewGuid(),
                                DemandeProjetId  = Demande.Id,
                                NomFichier       = annexe.FileName,
                                CheminRelatif    = path,
                                DateDepot        = DateTime.Now,
                                DeposeParId      = userId,
                                DateCreation     = DateTime.Now,
                                CreePar          = _currentUserService.Matricule
                            });
                        }
                    }
                    await _db.SaveChangesAsync();
                }

                await _auditService.LogActionAsync("CreationDemande", "DemandeProjet", Demande.Id,
                    null,
                    new { Titre = Demande.Titre, DirectionId = Demande.DirectionId, DirecteurMetierId = Demande.DirecteurMetierId });

                var dm = await _db.Utilisateurs.FindAsync(Demande.DirecteurMetierId);
                var dir = await _db.Directions.FindAsync(Demande.DirectionId);
                var nomDemandeur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
                _ = _teams.EnvoyerNouvelleDemandeAsync(
                    Demande.Titre ?? string.Empty, nomDemandeur, dir?.Libelle ?? "—",
                    dm != null ? $"{dm.Nom} {dm.Prenoms}" : "—", Demande.Id);
                if (dm?.Email != null)
                    _ = _email.EnvoyerNouvelleDemandeAuDMAsync(
                        dm.Email, $"{dm.Nom} {dm.Prenoms}".Trim(), Demande.Titre ?? string.Empty, nomDemandeur, dir?.Libelle ?? "—");

                return RedirectToAction(nameof(Details), new { id = Demande.Id });
            }

            var directionsErr = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            var directeursMetierErr = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            return View(new DemandeProjetCreateViewModel
            {
                Demande = Demande,
                Directions = new SelectList(directionsErr, "Id", "Libelle", Demande?.DirectionId),
                DirecteursMetier = directeursMetierErr.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text  = $"{u.Nom} {u.Prenoms}",
                    Selected = Demande?.DirecteurMetierId == u.Id
                })
            });
        }

        // GET: Éditer demande
        [Authorize]
        public async Task<IActionResult> Edit(Guid id)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Annexes)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var canManageDemandes  = await CanManageDemandesBackofficeAsync();
            var canEditOwnDemande  = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Edit") && demande.DemandeurId == userId;
            var canEditAsDm        = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ValiderDM") && demande.DirecteurMetierId == userId;
            if (!canManageDemandes && !canEditOwnDemande && !canEditAsDm)
                return Forbid();

            if (demande.StatutDemande != StatutDemande.Brouillon &&
                demande.StatutDemande != StatutDemande.CorrectionDemandeeParDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDemandeurParDSI)
            {
                if (!canManageDemandes &&
                    !((canEditOwnDemande &&
                       (demande.StatutDemande == StatutDemande.Brouillon ||
                        demande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier ||
                        demande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)) ||
                      (canEditAsDm &&
                       (demande.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                        demande.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI))))
                {
                    TempData["Error"] = "Cette demande ne peut plus être modifiée.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var directionsEdit = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            var directeursMetierEdit = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && u.Role == RoleUtilisateur.DirecteurMetier)
                .OrderBy(u => u.Nom)
                .ToListAsync();

            return View(new DemandeProjetEditViewModel
            {
                Demande = demande,
                Directions = new SelectList(directionsEdit, "Id", "Libelle", demande?.DirectionId),
                DirecteursMetier = directeursMetierEdit.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Nom} {u.Prenoms}",
                    Selected = demande?.DirecteurMetierId == u.Id
                })
            });
        }

        // POST: Éditer demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Edit(
            Guid id,
            DemandeProjet Demande,
            IFormFile? cahierCharges, List<IFormFile>? annexes)
        {
            var existingDemande = await _db.DemandesProjets.Include(d => d.Annexes).FirstOrDefaultAsync(d => d.Id == id);
            if (existingDemande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();
            var canEditOwnDemande = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Edit") && existingDemande.DemandeurId == userId;
            var canEditAsDm       = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ValiderDM") && existingDemande.DirecteurMetierId == userId;
            if (!canManageDemandes && !canEditOwnDemande && !canEditAsDm)
                return Forbid();

            const string pfx = "Demande.";
            ModelState.Remove(pfx + "Projet"); ModelState.Remove(pfx + "Demandeur"); ModelState.Remove(pfx + "DemandeurId");
            ModelState.Remove(pfx + "CommentaireDSI"); ModelState.Remove(pfx + "CommentaireDirecteurMetier"); ModelState.Remove(pfx + "CahierChargesPath");
            ModelState.Remove(pfx + "StatutDemande"); ModelState.Remove(pfx + "DateSoumission"); ModelState.Remove(pfx + "DateValidationDM");
            ModelState.Remove(pfx + "DateValidationDSI"); ModelState.Remove(pfx + "CreePar"); ModelState.Remove(pfx + "DateCreation");
            ModelState.Remove(pfx + "ModifiePar"); ModelState.Remove(pfx + "DateModification"); ModelState.Remove(pfx + "DirectionId");
            ModelState.Remove(pfx + "DirecteurMetierId"); ModelState.Remove(pfx + "Direction"); ModelState.Remove(pfx + "DirecteurMetier");
            ModelState.Remove(pfx + "Titre"); ModelState.Remove(pfx + "Description"); ModelState.Remove(pfx + "Contexte");
            ModelState.Remove(pfx + "Objectifs"); ModelState.Remove(pfx + "AvantagesAttendus"); ModelState.Remove(pfx + "Id");

            if (string.IsNullOrWhiteSpace(Demande.Titre))         ModelState.AddModelError(pfx + "Titre", "Le titre est requis.");
            if (string.IsNullOrWhiteSpace(Demande.Description))   ModelState.AddModelError(pfx + "Description", "La description est requise.");
            if (string.IsNullOrWhiteSpace(Demande.Contexte))      ModelState.AddModelError(pfx + "Contexte", "Le contexte est requis.");
            if (string.IsNullOrWhiteSpace(Demande.Objectifs))     ModelState.AddModelError(pfx + "Objectifs", "Les objectifs sont requis.");

            if (!Demande.DirectionId.HasValue || Demande.DirectionId.Value == Guid.Empty)
                ModelState.AddModelError(pfx + "DirectionId", "La direction est requise.");

            if (Demande.DirecteurMetierId == Guid.Empty)
                ModelState.AddModelError(pfx + "DirecteurMetierId", "Le directeur métier est requis.");

            if (ModelState.IsValid)
            {
                existingDemande.Titre                 = Demande.Titre?.Trim() ?? "";
                existingDemande.Description           = Demande.Description?.Trim() ?? string.Empty;
                existingDemande.Contexte              = Demande.Contexte?.Trim() ?? string.Empty;
                existingDemande.Objectifs             = Demande.Objectifs?.Trim() ?? string.Empty;
                existingDemande.AvantagesAttendus     = Demande.AvantagesAttendus?.Trim() ?? string.Empty;
                existingDemande.Perimetre             = Demande.Perimetre?.Trim() ?? string.Empty;
                existingDemande.Urgence               = Demande.Urgence;
                existingDemande.Criticite             = Demande.Criticite;
                existingDemande.DateMiseEnOeuvreSouhaitee = Demande.DateMiseEnOeuvreSouhaitee;
                existingDemande.DirectionId           = Demande.DirectionId;
                existingDemande.DirecteurMetierId     = Demande.DirecteurMetierId;

                if (existingDemande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
                    existingDemande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
                else if (existingDemande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                    existingDemande.StatutDemande = StatutDemande.EnAttenteValidationDSI;

                if (cahierCharges != null && cahierCharges.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    var maxSize = 10 * 1024 * 1024;
                    if (!string.IsNullOrEmpty(existingDemande.CahierChargesPath))
                        await _fileStorage.DeleteFileAsync(existingDemande.CahierChargesPath);
                    existingDemande.CahierChargesPath = await _fileStorage.SaveFileAsync(cahierCharges, "demandes", existingDemande.Id.ToString(), allowedExtensions, maxSize);
                }

                if (annexes != null && annexes.Any())
                {
                    foreach (var annexe in annexes)
                    {
                        if (annexe.Length > 0)
                        {
                            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                            var maxSize = 10 * 1024 * 1024;
                            var path = await _fileStorage.SaveFileAsync(annexe, "demandes", existingDemande.Id.ToString(), allowedExtensions, maxSize);
                            _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
                            {
                                Id = Guid.NewGuid(), DemandeProjetId = existingDemande.Id,
                                NomFichier = annexe.FileName, CheminRelatif = path,
                                DateDepot = DateTime.Now, DeposeParId = userId,
                                DateCreation = DateTime.Now, CreePar = _currentUserService.Matricule
                            });
                        }
                    }
                }

                existingDemande.DateModification = DateTime.Now;
                existingDemande.ModifiePar        = _currentUserService.Matricule;
                await _db.SaveChangesAsync();

                var ancienStatut = existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier
                    ? StatutDemande.CorrectionDemandeeParDirecteurMetier
                    : (existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDSI
                        ? StatutDemande.RetourneeAuDemandeurParDSI
                        : existingDemande.StatutDemande);

                if (existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                    existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDSI)
                    await _auditService.LogActionAsync("CORRECTION_DEMANDE", "DemandeProjet", existingDemande.Id,
                        new { AncienStatut = ancienStatut }, new { NouveauStatut = existingDemande.StatutDemande });
                else
                    await _auditService.LogActionAsync("MODIFICATION_DEMANDE", "DemandeProjet", existingDemande.Id);

                TempData["Success"] = "Demande modifiée avec succès.";
                return RedirectToAction(nameof(Details), new { id = existingDemande.Id });
            }

            var directionsPostErr = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            var dmsPostErr = await _db.Utilisateurs.Where(u => !u.EstSupprime && u.Role == RoleUtilisateur.DirecteurMetier).OrderBy(u => u.Nom).ToListAsync();
            return View(new DemandeProjetEditViewModel
            {
                Demande = existingDemande,
                Directions = new SelectList(directionsPostErr, "Id", "Libelle", existingDemande?.DirectionId),
                DirecteursMetier = dmsPostErr.Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Nom} {u.Prenoms}",
                    Selected = existingDemande?.DirecteurMetierId == u.Id
                })
            });
        }
    }
}
