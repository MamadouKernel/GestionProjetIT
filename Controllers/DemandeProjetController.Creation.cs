using GestionProjects.Application.Common.Extensions;
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

            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", preSelectedDirectionId);
            ViewBag.PreSelectedDirectionId       = preSelectedDirectionId;
            ViewBag.PreSelectedDirecteurMetierId = preSelectedDirecteurMetierId;
            ViewBag.IsReadOnly = isReadOnly;

            var directeursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.DirecteursMetier = directeursMetier.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Nom} {u.Prenoms}",
                Selected = preSelectedDirecteurMetierId == u.Id
            });

            return View();
        }

        // POST: Créer nouvelle demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Create(DemandeProjet demande, IFormFile? cahierCharges, List<IFormFile>? annexes)
        {
            ModelState.Remove(nameof(demande.Projet));
            ModelState.Remove(nameof(demande.Demandeur));
            ModelState.Remove(nameof(demande.DemandeurId));
            ModelState.Remove(nameof(demande.CommentaireDSI));
            ModelState.Remove(nameof(demande.CommentaireDirecteurMetier));
            ModelState.Remove(nameof(demande.CahierChargesPath));
            ModelState.Remove(nameof(demande.StatutDemande));
            ModelState.Remove(nameof(demande.DateSoumission));
            ModelState.Remove(nameof(demande.DateValidationDM));
            ModelState.Remove(nameof(demande.DateValidationDSI));
            ModelState.Remove(nameof(demande.CreePar));
            ModelState.Remove(nameof(demande.DateCreation));
            ModelState.Remove(nameof(demande.ModifiePar));
            ModelState.Remove(nameof(demande.DateModification));
            ModelState.Remove(nameof(demande.DirectionId));
            ModelState.Remove(nameof(demande.DirecteurMetierId));
            ModelState.Remove(nameof(demande.Direction));
            ModelState.Remove(nameof(demande.DirecteurMetier));
            ModelState.Remove(nameof(demande.Titre));
            ModelState.Remove(nameof(demande.Description));
            ModelState.Remove(nameof(demande.Contexte));
            ModelState.Remove(nameof(demande.Objectifs));

            if (string.IsNullOrWhiteSpace(demande.Titre))
                ModelState.AddModelError(nameof(demande.Titre), "Le titre du projet est requis.");

            if (string.IsNullOrWhiteSpace(demande.Description))
                ModelState.AddModelError(nameof(demande.Description), "La description est requise.");

            if (string.IsNullOrWhiteSpace(demande.Contexte))
                ModelState.AddModelError(nameof(demande.Contexte), "Le contexte est requis.");

            if (string.IsNullOrWhiteSpace(demande.Objectifs))
                ModelState.AddModelError(nameof(demande.Objectifs), "Les objectifs sont requis.");

            if (!demande.DirectionId.HasValue || demande.DirectionId.Value == Guid.Empty)
                ModelState.AddModelError(nameof(demande.DirectionId), "La direction est requise.");

            if (demande.DirecteurMetierId == Guid.Empty)
                ModelState.AddModelError(nameof(demande.DirecteurMetierId), "Le directeur métier est requis.");

            if (ModelState.IsValid)
            {
                var userId = User.GetUserIdOrThrow();

                demande.DateValidationDM  = null;
                demande.DateValidationDSI = null;
                demande.Id              = Guid.NewGuid();
                demande.DemandeurId     = userId;
                demande.StatutDemande   = StatutDemande.EnAttenteValidationDirecteurMetier;
                demande.DateSoumission  = DateTime.Now;
                demande.DateCreation    = DateTime.Now;
                demande.CreePar         = _currentUserService.Matricule;

                demande.Titre                       = demande.Titre ?? string.Empty;
                demande.Description                 = demande.Description ?? string.Empty;
                demande.Contexte                    = demande.Contexte ?? string.Empty;
                demande.Objectifs                   = demande.Objectifs ?? string.Empty;
                demande.AvantagesAttendus           = demande.AvantagesAttendus ?? string.Empty;
                demande.Perimetre                   = demande.Perimetre ?? string.Empty;
                demande.CommentaireDirecteurMetier  = demande.CommentaireDirecteurMetier ?? string.Empty;
                demande.CommentaireDSI              = demande.CommentaireDSI ?? string.Empty;
                demande.CahierChargesPath           = demande.CahierChargesPath ?? string.Empty;

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

                var dm = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
                var dir = await _db.Directions.FindAsync(demande.DirectionId);
                var nomDemandeur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
                _ = _teams.EnvoyerNouvelleDemandeAsync(
                    demande.Titre ?? string.Empty, nomDemandeur, dir?.Libelle ?? "—",
                    dm != null ? $"{dm.Nom} {dm.Prenoms}" : "—", demande.Id);
                if (dm?.Email != null)
                    _ = _email.EnvoyerNouvelleDemandeAuDMAsync(
                        dm.Email, $"{dm.Nom} {dm.Prenoms}".Trim(), demande.Titre ?? string.Empty, nomDemandeur, dir?.Libelle ?? "—");

                return RedirectToAction(nameof(Details), new { id = demande.Id });
            }

            var directions = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", demande?.DirectionId);

            var directeursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.DirecteursMetier = directeursMetier.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Nom} {u.Prenoms}",
                Selected = demande?.DirecteurMetierId == u.Id
            });

            return View(demande);
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

            var directions = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", demande?.DirectionId);

            var directeursMetier = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && u.Role == RoleUtilisateur.DirecteurMetier)
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.DirecteursMetier = directeursMetier.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Nom} {u.Prenoms}",
                Selected = demande?.DirecteurMetierId == u.Id
            });

            return View(demande);
        }

        // POST: Éditer demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Edit(
            Guid id,
            string Titre, string Description, string Contexte, string Objectifs,
            string AvantagesAttendus, string? Perimetre, int Urgence, int Criticite,
            DateTime? DateMiseEnOeuvreSouhaitee, string? DirectionId, string DirecteurMetierId,
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

            ModelState.Remove("Projet"); ModelState.Remove("Demandeur"); ModelState.Remove("DemandeurId");
            ModelState.Remove("CommentaireDSI"); ModelState.Remove("CommentaireDirecteurMetier"); ModelState.Remove("CahierChargesPath");
            ModelState.Remove("StatutDemande"); ModelState.Remove("DateSoumission"); ModelState.Remove("DateValidationDM");
            ModelState.Remove("DateValidationDSI"); ModelState.Remove("CreePar"); ModelState.Remove("DateCreation");
            ModelState.Remove("ModifiePar"); ModelState.Remove("DateModification"); ModelState.Remove("DirectionId");
            ModelState.Remove("DirecteurMetierId"); ModelState.Remove("Direction"); ModelState.Remove("DirecteurMetier");
            ModelState.Remove("Titre"); ModelState.Remove("Description"); ModelState.Remove("Contexte");
            ModelState.Remove("Objectifs"); ModelState.Remove("AvantagesAttendus");

            if (string.IsNullOrWhiteSpace(Titre))         ModelState.AddModelError("Titre", "Le titre est requis.");
            if (string.IsNullOrWhiteSpace(Description))   ModelState.AddModelError("Description", "La description est requise.");
            if (string.IsNullOrWhiteSpace(Contexte))      ModelState.AddModelError("Contexte", "Le contexte est requis.");
            if (string.IsNullOrWhiteSpace(Objectifs))     ModelState.AddModelError("Objectifs", "Les objectifs sont requis.");

            Guid? directionGuid = null;
            Guid directeurMetierGuid = Guid.Empty;

            if (string.IsNullOrWhiteSpace(DirectionId) || !Guid.TryParse(DirectionId, out var parsedDirectionGuid))
                ModelState.AddModelError("DirectionId", "La direction est requise.");
            else
                directionGuid = parsedDirectionGuid;

            if (string.IsNullOrWhiteSpace(DirecteurMetierId) || !Guid.TryParse(DirecteurMetierId, out directeurMetierGuid))
                ModelState.AddModelError("DirecteurMetierId", "Le directeur métier est requis.");

            if (ModelState.IsValid)
            {
                existingDemande.Titre                 = Titre.Trim();
                existingDemande.Description           = Description?.Trim() ?? string.Empty;
                existingDemande.Contexte              = Contexte?.Trim() ?? string.Empty;
                existingDemande.Objectifs             = Objectifs?.Trim() ?? string.Empty;
                existingDemande.AvantagesAttendus     = AvantagesAttendus?.Trim() ?? string.Empty;
                existingDemande.Perimetre             = Perimetre?.Trim() ?? string.Empty;
                existingDemande.Urgence               = (UrgenceProjet)Urgence;
                existingDemande.Criticite             = (CriticiteProjet)Criticite;
                existingDemande.DateMiseEnOeuvreSouhaitee = DateMiseEnOeuvreSouhaitee;
                existingDemande.DirectionId           = directionGuid;
                existingDemande.DirecteurMetierId     = directeurMetierGuid;

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

            var directions = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", existingDemande?.DirectionId);

            var dms = await _db.Utilisateurs.Where(u => !u.EstSupprime && u.Role == RoleUtilisateur.DirecteurMetier).OrderBy(u => u.Nom).ToListAsync();
            ViewBag.DirecteursMetier = dms.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(), Text = $"{u.Nom} {u.Prenoms}", Selected = existingDemande?.DirecteurMetierId == u.Id
            });

            return View(existingDemande);
        }
    }
}
