using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.ViewModels.DemandeProjet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                Demande = new DemandeProjetFormModel
                {
                    DirectionId = preSelectedDirectionId,
                    DirecteurMetierId = preSelectedDirecteurMetierId ?? Guid.Empty
                },
                Directions = directions.Select(d => new SelectOption(d.Id.ToString(), d.Libelle, preSelectedDirectionId == d.Id)),
                DirecteursMetier = directeursMetier.Select(u => new SelectOption(
                    u.Id.ToString(),
                    $"{u.Nom} {u.Prenoms}",
                    preSelectedDirecteurMetierId == u.Id)),
                IsReadOnly = isReadOnly,
                PreSelectedDirectionId = preSelectedDirectionId,
                PreSelectedDirecteurMetierId = preSelectedDirecteurMetierId
            };

            ViewBag.CahierChargesRequired = !canManageDemandes;

            return View(vm);
        }

        // POST: Créer nouvelle demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Create(
            [Bind(Prefix = "Demande")] DemandeProjetFormModel demandeForm,
            IFormFile? cahierCharges,
            List<IFormFile>? annexes,
            string? workflowAction = "submit")
        {
            if (demandeForm.DirecteurMetierId == Guid.Empty)
                ModelState.AddModelError("Demande.DirecteurMetierId", "Le directeur metier est requis.");
            if (!demandeForm.DirectionId.HasValue || demandeForm.DirectionId.Value == Guid.Empty)
                ModelState.AddModelError("Demande.DirectionId", "La direction est requise.");

            if (ModelState.IsValid)
            {
                var userId = User.GetUserIdOrThrow();

                var canManage = await CanManageDemandesBackofficeAsync();
                var saveAsDraft = workflowAction == "draft" && canManage;

                var demande = new DemandeProjet
                {
                    Id = Guid.NewGuid(),
                    DemandeurId = userId,
                    StatutDemande = saveAsDraft
                        ? StatutDemande.Brouillon
                        : StatutDemande.EnAttenteValidationDirecteurMetier,
                    DateSoumission = DateTime.Now,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule,
                    Titre = demandeForm.Titre?.Trim() ?? string.Empty,
                    Description = demandeForm.Description?.Trim() ?? string.Empty,
                    Contexte = demandeForm.Contexte?.Trim() ?? string.Empty,
                    Objectifs = demandeForm.Objectifs?.Trim() ?? string.Empty,
                    AvantagesAttendus = demandeForm.AvantagesAttendus?.Trim() ?? string.Empty,
                    Perimetre = demandeForm.Perimetre?.Trim() ?? string.Empty,
                    DirectionId = demandeForm.DirectionId,
                    DirecteurMetierId = demandeForm.DirecteurMetierId,
                    AutreSponsorId = demandeForm.AutreSponsorId,
                    Urgence = demandeForm.Urgence,
                    Criticite = demandeForm.Criticite,
                    DateMiseEnOeuvreSouhaitee = demandeForm.DateMiseEnOeuvreSouhaitee,
                    CommentaireDirecteurMetier = string.Empty,
                    CommentaireDSI = string.Empty,
                    CahierChargesPath = string.Empty
                };

                if (cahierCharges != null && cahierCharges.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    var maxSize = 10 * 1024 * 1024;
                    var savedPath = await _fileStorage.SaveFileAsync(cahierCharges, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);
                    if (!string.IsNullOrEmpty(savedPath))
                        demande.CahierChargesPath = savedPath;
                }

                if (string.IsNullOrEmpty(demande.CahierChargesPath))
                    demande.CahierChargesPath = string.Empty;

                _db.DemandesProjets.Add(demande);
                await _db.SaveChangesAsync();

                if (annexes != null && annexes.Any())
                {
                    foreach (var annexe in annexes)
                    {
                        if (annexe.Length > 0)
                        {
                            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                            var maxSize = 10 * 1024 * 1024;
                            var path = await _fileStorage.SaveFileAsync(annexe, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);

                            _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
                            {
                                Id               = Guid.NewGuid(),
                                DemandeProjetId  = demande.Id,
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

                await _auditService.LogActionAsync("CreationDemande", "DemandeProjet", demande.Id,
                    null,
                    new { Titre = demande.Titre, DirectionId = demande.DirectionId, DirecteurMetierId = demande.DirecteurMetierId });

                if (!saveAsDraft)
                {
                    var dm = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
                    var dir = await _db.Directions.FindAsync(demande.DirectionId);
                    var nomDemandeur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
                    await _teams.EnvoyerNouvelleDemandeAsync(
                        demande.Titre ?? string.Empty, nomDemandeur, dir?.Libelle ?? "—",
                        dm != null ? $"{dm.Nom} {dm.Prenoms}" : "—", demande.Id);
                    if (dm?.Email != null)
                        await _email.EnvoyerNouvelleDemandeAuDMAsync(
                            dm.Email, $"{dm.Nom} {dm.Prenoms}".Trim(), demande.Titre ?? string.Empty, nomDemandeur, dir?.Libelle ?? "—");
                }

                TempData[saveAsDraft ? "Info" : "Success"] = saveAsDraft
                    ? "Demande enregistrée comme brouillon."
                    : "Demande soumise au directeur métier pour validation.";

                return RedirectToAction(nameof(Details), new { id = demande.Id });
            }

            var directionsErr = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            var directeursMetierErr = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            return View(new DemandeProjetCreateViewModel
            {
                Demande = demandeForm,
                Directions = directionsErr.Select(d => new SelectOption(d.Id.ToString(), d.Libelle, demandeForm.DirectionId == d.Id)),
                DirecteursMetier = directeursMetierErr.Select(u => new SelectOption(
                    u.Id.ToString(),
                    $"{u.Nom} {u.Prenoms}",
                    demandeForm.DirecteurMetierId == u.Id))
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
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            return View(new DemandeProjetEditViewModel
            {
                Demande = DemandeProjetFormModel.FromEntity(demande),
                Directions = directionsEdit.Select(d => new SelectOption(d.Id.ToString(), d.Libelle, demande?.DirectionId == d.Id)),
                DirecteursMetier = directeursMetierEdit.Select(u => new SelectOption(
                    u.Id.ToString(),
                    $"{u.Nom} {u.Prenoms}",
                    demande?.DirecteurMetierId == u.Id))
            });
        }

        // POST: Éditer demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind(Prefix = "Demande")] DemandeProjetFormModel demandeForm,
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

            if (!demandeForm.DirectionId.HasValue || demandeForm.DirectionId.Value == Guid.Empty)
                ModelState.AddModelError("Demande.DirectionId", "La direction est requise.");
            if (demandeForm.DirecteurMetierId == Guid.Empty)
                ModelState.AddModelError("Demande.DirecteurMetierId", "Le directeur metier est requis.");

            if (ModelState.IsValid)
            {
                existingDemande.Titre                 = demandeForm.Titre?.Trim() ?? string.Empty;
                existingDemande.Description           = demandeForm.Description?.Trim() ?? string.Empty;
                existingDemande.Contexte              = demandeForm.Contexte?.Trim() ?? string.Empty;
                existingDemande.Objectifs             = demandeForm.Objectifs?.Trim() ?? string.Empty;
                existingDemande.AvantagesAttendus     = demandeForm.AvantagesAttendus?.Trim() ?? string.Empty;
                existingDemande.Perimetre             = demandeForm.Perimetre?.Trim() ?? string.Empty;
                existingDemande.Urgence               = demandeForm.Urgence;
                existingDemande.Criticite             = demandeForm.Criticite;
                existingDemande.DateMiseEnOeuvreSouhaitee = demandeForm.DateMiseEnOeuvreSouhaitee;
                existingDemande.DirectionId           = demandeForm.DirectionId;
                existingDemande.DirecteurMetierId     = demandeForm.DirecteurMetierId;
                existingDemande.AutreSponsorId        = demandeForm.AutreSponsorId;

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
            var dmsPostErr = await _db.Utilisateurs.Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier)).OrderBy(u => u.Nom).ToListAsync();

            demandeForm.Id = existingDemande.Id;
            demandeForm.StatutDemande = existingDemande.StatutDemande;
            demandeForm.DateSoumission = existingDemande.DateSoumission;
            demandeForm.CommentaireDirecteurMetier = existingDemande.CommentaireDirecteurMetier;
            demandeForm.CommentaireDSI = existingDemande.CommentaireDSI;
            demandeForm.CahierChargesPath = existingDemande.CahierChargesPath;
            demandeForm.Annexes = existingDemande.Annexes;

            return View(new DemandeProjetEditViewModel
            {
                Demande = demandeForm,
                Directions = directionsPostErr.Select(d => new SelectOption(d.Id.ToString(), d.Libelle, demandeForm.DirectionId == d.Id)),
                DirecteursMetier = dmsPostErr.Select(u => new SelectOption(
                    u.Id.ToString(),
                    $"{u.Nom} {u.Prenoms}",
                    demandeForm.DirecteurMetierId == u.Id))
            });
        }
    }
}
