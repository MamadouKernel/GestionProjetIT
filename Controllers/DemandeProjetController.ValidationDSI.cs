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
        // POST: Valider par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderDSI(Guid id, string? commentaire, Guid? chefProjetId)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId &&
                              d.EstActive &&
                              d.DateDebut <= DateTime.Now &&
                              d.DateFin >= DateTime.Now &&
                              !d.EstSupprime);

            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de valider cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var demande = await _db.DemandesProjets.Include(d => d.Direction).FirstOrDefaultAsync(d => d.Id == id);
            if (demande == null)
                return NotFound();

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation DSI.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande    = StatutDemande.ValideeParDSI;
            demande.DateValidationDSI = DateTime.Now;
            demande.CommentaireDSI   = commentaire ?? string.Empty;
            await _db.SaveChangesAsync();

            // Récupérer ou créer le portefeuille actif
            var portefeuilleActif = await _db.PortefeuillesProjets.FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);
            if (portefeuilleActif == null)
            {
                await EnsurePortefeuilleActifAsync();
                portefeuilleActif = await _db.PortefeuillesProjets.FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);
            }

            var projet = new Projet
            {
                Id            = Guid.NewGuid(),
                CodeProjet    = $"PROJ-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                Titre         = demande.Titre ?? string.Empty,
                Objectif      = demande.Objectifs,
                PortefeuilleProjetId = portefeuilleActif!.Id,
                DemandeProjetId  = demande.Id,
                DirectionId      = demande.DirectionId,
                SponsorId        = demande.DirecteurMetierId,
                ChefProjetId     = chefProjetId,
                StatutProjet     = StatutProjet.NonDemarre,
                PhaseActuelle    = PhaseProjet.AnalyseClarification,
                EtatProjet       = EtatProjet.Vert,
                PourcentageAvancement = 0,
                BilanCloture     = string.Empty,
                LeconsApprises   = string.Empty,
                DateCreation     = DateTime.Now,
                CreePar          = _currentUserService.Matricule ?? string.Empty
            };

            _db.Projets.Add(projet);
            await _db.SaveChangesAsync();

            string actionLog = isDelegue ? "VALIDATION_DSI_PAR_DELEGUE" : "VALIDATION_DSI";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            await _auditService.LogActionAsync("CreationProjet", "Projet", projet.Id, null,
                new { CodeProjet = projet.CodeProjet, Titre = projet.Titre, StatutProjet = projet.StatutProjet, PhaseActuelle = projet.PhaseActuelle });

            _ = _teams.EnvoyerValidationDSIAsync(demande.Titre ?? string.Empty,
                $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim(),
                true, commentaire, demande.Id);
            var demandeurDSI = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
            var dmDSI        = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
            _ = _email.EnvoyerValidationDSIProjetCreeAsync(
                demandeurDSI?.Email ?? string.Empty, dmDSI?.Email,
                demande.Titre ?? string.Empty, projet.CodeProjet);

            TempData["Success"] = $"Demande validée et projet {projet.CodeProjet} créé automatiquement.";
            return RedirectToAction("Details", "Projet", new { id = projet.Id });
        }

        // POST: Rejeter par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterDSI(Guid id, string? commentaire)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && d.EstActive && d.DateDebut <= DateTime.Now && d.DateFin >= DateTime.Now && !d.EstSupprime);

            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de rejeter cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation DSI.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande    = StatutDemande.RejeteeParDSI;
            demande.CommentaireDSI   = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar       = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            string actionLog = isDelegue ? "REJET_DSI_PAR_DELEGUE" : "REJET_DSI";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            var nomDSIRejet = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, nomDSIRejet, "Rejet par la DSI", commentaire, demande.Id);
            var demandeurRejetDSI = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
            var dmRejetDSI        = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
            _ = _email.EnvoyerRejetDSIAsync(
                demandeurRejetDSI?.Email ?? string.Empty, dmRejetDSI?.Email,
                demande.Titre ?? string.Empty, nomDSIRejet, commentaire);

            TempData["Success"] = "Demande rejetée par la DSI.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Renvoyer au demandeur par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RenvoyerAuDemandeurDSI(Guid id, string? commentaire)
        {
            var userId = User.GetUserIdOrThrow();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "HistoriqueActionsDM"))
                return Forbid();

            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && d.EstActive && d.DateDebut <= DateTime.Now && d.DateFin >= DateTime.Now && !d.EstSupprime);

            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de renvoyer cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation DSI.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour renvoyer la demande au demandeur.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande    = StatutDemande.RetourneeAuDemandeurParDSI;
            demande.CommentaireDSI   = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar       = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            string actionLog = isDelegue ? "CORRECTION_DSI_DEMANDEUR_PAR_DELEGUE" : "CORRECTION_DSI_DEMANDEUR";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            var acteurRenvoi = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, acteurRenvoi, "Renvoi au demandeur par la DSI", commentaire, demande.Id);
            var demandeurRenvoi = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
            if (demandeurRenvoi?.Email != null)
                _ = _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                    demandeurRenvoi.Email, $"{demandeurRenvoi.Nom} {demandeurRenvoi.Prenoms}".Trim(),
                    demande.Titre ?? string.Empty, acteurRenvoi, "Renvoi pour correction par la DSI", commentaire);

            TempData["Success"] = "Demande renvoyée au demandeur pour correction.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Renvoyer au Directeur Métier par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RenvoyerAuDMDSI(Guid id, string commentaire)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && d.EstActive && d.DateDebut <= DateTime.Now && d.DateFin >= DateTime.Now && !d.EstSupprime);

            if (!await CanHandleDsiValidationAsync(isDelegue))
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de renvoyer cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation DSI.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour renvoyer la demande au Directeur Métier.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande    = StatutDemande.RetourneeAuDirecteurMetierParDSI;
            demande.CommentaireDSI   = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar       = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            string actionLog = isDelegue ? "CORRECTION_DSI_DM_PAR_DELEGUE" : "CORRECTION_DSI_DM";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            var dmRenvoi = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
            if (dmRenvoi?.Email != null)
                _ = _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                    dmRenvoi.Email, $"{dmRenvoi.Nom} {dmRenvoi.Prenoms}".Trim(),
                    demande.Titre ?? string.Empty,
                    $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim(),
                    "Renvoi au Directeur Métier par la DSI", commentaire);

            TempData["Success"] = "Demande renvoyée au Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
