using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // POST: Valider par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderDM(Guid id, string? commentaire,
            string? titre, string? description, string? objectifs, string? avantagesAttendus)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ValiderDM"))
                return Forbid();

            if (!await HasAdminScopeAsync())
            {
                if (demande.DirecteurMetierId != userId)
                    return Forbid();
            }

            if (demande.DemandeurId == userId)
            {
                TempData["Error"] = "Vous ne pouvez pas valider votre propre demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            var avant = new { Titre = demande.Titre, Description = demande.Description, Objectifs = demande.Objectifs, AvantagesAttendus = demande.AvantagesAttendus, StatutDemande = ancienStatut.ToString() };

            if (!string.IsNullOrWhiteSpace(titre))         demande.Titre           = titre.Trim();
            if (!string.IsNullOrWhiteSpace(description))   demande.Description     = description.Trim();
            if (!string.IsNullOrWhiteSpace(objectifs))     demande.Objectifs       = objectifs.Trim();
            if (avantagesAttendus != null)                  demande.AvantagesAttendus = avantagesAttendus.Trim();

            demande.StatutDemande              = StatutDemande.EnAttenteValidationDSI;
            demande.DateValidationDM           = DateTime.Now;
            demande.CommentaireDirecteurMetier = commentaire ?? string.Empty;
            demande.DateModification           = DateTime.Now;
            demande.ModifiePar                 = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            var apres = new { Titre = demande.Titre, Description = demande.Description, Objectifs = demande.Objectifs, AvantagesAttendus = demande.AvantagesAttendus, StatutDemande = demande.StatutDemande.ToString(), Commentaire = commentaire };
            await _auditService.LogActionAsync("VALIDATION_DM", "DemandeProjet", demande.Id, avant, apres);

            var nomDM = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _teams.EnvoyerValidationDMAsync(demande.Titre ?? string.Empty, nomDM, true, commentaire, demande.Id);
            var dsi = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .Join(_db.UtilisateurRoles.Where(r => r.Role == Domain.Enums.RoleUtilisateur.DSI && !r.EstSupprime),
                      u => u.Id, r => r.UtilisateurId, (u, r) => u)
                .FirstOrDefaultAsync();
            if (dsi?.Email != null)
                _ = _email.EnvoyerValidationDMVersdsIAsync(dsi.Email, demande.Titre ?? string.Empty, nomDM, commentaire);

            TempData["Success"] = "Demande validée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Rejeter par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterDM(Guid id, string commentaire)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "RejeterDM"))
                return Forbid();

            if (!await HasAdminScopeAsync())
            {
                if (demande.DirecteurMetierId != userId)
                    return Forbid();
            }

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le motif du rejet est obligatoire.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande              = StatutDemande.RejeteeParDirecteurMetier;
            demande.CommentaireDirecteurMetier = commentaire.Trim();
            demande.DateModification           = DateTime.Now;
            demande.ModifiePar                 = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_DM", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            var acteurDM = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, acteurDM, "Rejet par Directeur métier", commentaire, demande.Id);
            var demandeurDM = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
            if (demandeurDM?.Email != null)
                _ = _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                    demandeurDM.Email, $"{demandeurDM.Nom} {demandeurDM.Prenoms}".Trim(),
                    demande.Titre ?? string.Empty, acteurDM, "Rejet par le Directeur Métier", commentaire);

            TempData["Success"] = "Demande rejetée.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Demander correction par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DemanderCorrectionDM(Guid id, string commentaire)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "DemanderCorrectionDM"))
                return Forbid();

            if (!await HasAdminScopeAsync())
            {
                if (demande.DirecteurMetierId != userId)
                    return Forbid();
            }

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour demander une correction.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande              = StatutDemande.CorrectionDemandeeParDirecteurMetier;
            demande.CommentaireDirecteurMetier = commentaire.Trim();
            demande.DateModification           = DateTime.Now;
            demande.ModifiePar                 = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CORRECTION_DM", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            var acteurCorr = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, acteurCorr, "Correction demandée par le Directeur métier", commentaire, demande.Id);
            var demandeurCorr = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
            if (demandeurCorr?.Email != null)
                _ = _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                    demandeurCorr.Email, $"{demandeurCorr.Nom} {demandeurCorr.Prenoms}".Trim(),
                    demande.Titre ?? string.Empty, acteurCorr, "Correction demandée par le Directeur Métier", commentaire);

            TempData["Success"] = "Correction demandée au demandeur.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
