using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // POST: Lancer Demande Clôture
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DemanderCloture(Guid id, string? commentaire, DateTime? dateSouhaiteeCloture)
        {
            var projet = await _db.Projets
                .Include(p => p.DemandeProjet)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageClotureAsync(projet))
            {
                TempData["Error"] = "Seul le Chef de Projet ou le Responsable Solutions IT peut initier une demande de clôture.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Vérifier que le projet est en phase Clôture et que les prérequis UAT/MEP sont soldés
            if (projet.PhaseActuelle != PhaseProjet.ClotureLeconsApprises || !projet.RecetteValidee || !projet.MepEffectuee)
            {
                TempData["Error"] = "Le projet doit être en phase Clôture avec recette validée et MEP effectuée avant de demander la clôture.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Vérifier que les livrables de clôture, bilan et leçons apprises sont renseignés
            var livrablesCloture = await _db.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id && l.Phase == PhaseProjet.ClotureLeconsApprises)
                .AnyAsync();

            if (!livrablesCloture)
            {
                TempData["Error"] = "Veuillez d'abord déposer les livrables de clôture avant de soumettre la demande.";
                return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
            }

            if (string.IsNullOrWhiteSpace(projet.BilanPerimetre) ||
                string.IsNullOrWhiteSpace(projet.BilanPlanning) ||
                string.IsNullOrWhiteSpace(projet.BilanBudget) ||
                string.IsNullOrWhiteSpace(projet.LeconsReussites) ||
                string.IsNullOrWhiteSpace(projet.LeconsRecommandations))
            {
                TempData["Error"] = "Veuillez renseigner le bilan de clôture et les leçons apprises avant de soumettre la demande.";
                return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
            }

            if (projet.FicheProjet == null ||
                !projet.FicheProjet.TransfertRunDocumentation ||
                !projet.FicheProjet.TransfertRunSupportInforme ||
                !projet.FicheProjet.TransfertRunExploitationPrete)
            {
                TempData["Error"] = "Le transfert RUN doit être entièrement renseigné avant la soumission de la clôture.";
                return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
            }

            var demande = new DemandeClotureProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                DemandeParId = userId,
                DateDemande = DateTime.Now,
                DateSouhaiteeCloture = dateSouhaiteeCloture,
                StatutValidationDemandeur = StatutValidationCloture.EnAttente,
                StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente,
                StatutValidationDSI = StatutValidationCloture.EnAttente,
                EstTerminee = false,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule,
                CommentaireInitiateur = commentaire ?? string.Empty,
                CommentaireDemandeur = string.Empty,
                CommentaireDirecteurMetier = string.Empty,
                CommentaireDSI = string.Empty
            };

            _db.DemandesClotureProjets.Add(demande);

            projet.StatutProjet = StatutProjet.ClotureEnCours;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("DEMANDE_CLOTURE", "Projet", projet.Id);
            await _auditService.LogActionAsync("SOUMISSION_DEMANDE_CLOTURE", "DemandeClotureProjet", demande.Id,
                new { ProjetId = projet.Id, DemandeParId = userId },
                new { StatutValidationDemandeur = StatutValidationCloture.EnAttente, StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente, StatutValidationDSI = StatutValidationCloture.EnAttente });

            TempData["Success"] = "Demande de clôture créée.";
            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }

        // GET: Liste des demandes de clôture à valider (Demandeur)
        [Authorize]
        public async Task<IActionResult> ListeValidationClotureDemandeur(string? recherche = null, int page = 1, int pageSize = 20)
        {
            if (!await HasDemandeurProjectAccessAsync())
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            var userId = User.GetUserIdOrThrow();

            var query = _db.DemandesClotureProjets
                .Include(d => d.Projet).ThenInclude(p => p.Direction)
                .Include(d => d.Projet).ThenInclude(p => p.DemandeProjet)
                .Include(d => d.DemandePar)
                .Where(d => !d.EstTerminee &&
                            d.StatutValidationDemandeur == StatutValidationCloture.EnAttente &&
                            d.Projet.DemandeProjet != null &&
                            d.Projet.DemandeProjet.DemandeurId == userId);

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => d.Projet.Titre.Contains(recherche) || d.Projet.CodeProjet.Contains(recherche));

            query = query.OrderByDescending(d => d.DateDemande);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            return View(paged.Items);
        }

        // POST: Valider Clôture - Demandeur
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderClotureDemandeur(Guid demandeClotureId)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                    .ThenInclude(p => p.DemandeProjet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDemandeur") ||
                demande.Projet.DemandeProjet?.DemandeurId != userId)
                return Forbid();

            if (demande.StatutValidationDemandeur != StatutValidationCloture.EnAttente)
            {
                TempData["Error"] = "Cette validation a déjà été effectuée.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            demande.StatutValidationDemandeur = StatutValidationCloture.Validee;
            demande.DateValidationDemandeur = DateTime.Now;

            await VerifierClotureComplete(demande);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_CLOTURE_DEMANDEUR", "DemandeClotureProjet", demande.Id);

            TempData["Success"] = "Clôture validée par le demandeur.";
            return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
        }

        // GET: Liste des demandes de clôture à valider (Directeur Métier)
        [Authorize]
        public async Task<IActionResult> ListeValidationClotureDM(string? recherche = null, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var canPortfolioAccess = await HasPortfolioGovernanceAccessAsync();

            if (!await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM") && !canPortfolioAccess)
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var demandesQuery = _db.DemandesClotureProjets
                .Include(d => d.Projet).ThenInclude(p => p.Direction)
                .Include(d => d.Projet).ThenInclude(p => p.DemandeProjet).ThenInclude(dp => dp.Demandeur)
                .Include(d => d.DemandePar)
                .Where(d => !d.EstTerminee &&
                            d.StatutValidationDemandeur == StatutValidationCloture.Validee &&
                            d.StatutValidationDirecteurMetier == StatutValidationCloture.EnAttente);

            if (!canPortfolioAccess)
            {
                var directionId = await GetCurrentUserDirectionIdAsync(userId);
                demandesQuery = demandesQuery.Where(d =>
                    d.Projet.SponsorId == userId ||
                    (directionId.HasValue && d.Projet.DirectionId == directionId));
            }

            if (!string.IsNullOrWhiteSpace(recherche))
                demandesQuery = demandesQuery.Where(d =>
                    d.Projet.Titre.Contains(recherche) || d.Projet.CodeProjet.Contains(recherche));

            demandesQuery = demandesQuery.OrderByDescending(d => d.DateDemande);

            var paged = await demandesQuery.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            return View(paged.Items);
        }

        // POST: Valider Clôture - Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderClotureDM(Guid demandeClotureId)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateClotureDmAsync(demande, userId))
                return Forbid();

            if (demande.StatutValidationDirecteurMetier != StatutValidationCloture.EnAttente)
            {
                TempData["Error"] = "Cette validation a déjà été effectuée.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            demande.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
            demande.DateValidationDirecteurMetier = DateTime.Now;
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;

            await VerifierClotureComplete(demande);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_CLOTURE_DM", "DemandeClotureProjet", demande.Id,
                new { Decision = "Validee", Commentaire = demande.CommentaireDirecteurMetier });

            TempData["Success"] = "Clôture validée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
        }

        // POST: Rejeter Clôture - Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterClotureDM(Guid demandeClotureId, string commentaire)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateClotureDmAsync(demande, userId))
                return Forbid();

            // Le commentaire est obligatoire pour le rejet
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la clôture.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            if (demande.StatutValidationDirecteurMetier != StatutValidationCloture.EnAttente)
            {
                TempData["Error"] = "Cette validation a déjà été effectuée.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            demande.StatutValidationDirecteurMetier = StatutValidationCloture.Rejetee;
            demande.DateValidationDirecteurMetier = DateTime.Now;
            demande.CommentaireDirecteurMetier = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_CLOTURE_DM", "DemandeClotureProjet", demande.Id,
                new { Commentaire = commentaire, Decision = "Rejetee" });

            TempData["Success"] = "Clôture rejetée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
        }

        // GET: Liste des demandes de clôture à valider (DSI)
        [Authorize]
        public async Task<IActionResult> ListeValidationClotureDSI(string? recherche = null, int page = 1, int pageSize = 20)
        {
            if (!await CanValidateClotureDsiAsync(User.GetUserIdOrThrow()))
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.DemandesClotureProjets
                .Include(d => d.Projet).ThenInclude(p => p.Direction)
                .Include(d => d.Projet).ThenInclude(p => p.DemandeProjet).ThenInclude(dp => dp.Demandeur)
                .Include(d => d.DemandePar)
                .Where(d => !d.EstTerminee &&
                            d.StatutValidationDemandeur == StatutValidationCloture.Validee &&
                            d.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
                            d.StatutValidationDSI == StatutValidationCloture.EnAttente);

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => d.Projet.Titre.Contains(recherche) || d.Projet.CodeProjet.Contains(recherche));

            query = query.OrderByDescending(d => d.DateDemande);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            return View(paged.Items);
        }

        // POST: Valider Clôture - DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderClotureDSI(Guid demandeClotureId)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateClotureDsiAsync(userId))
            {
                return Forbid();
            }

            if (demande.StatutValidationDSI != StatutValidationCloture.EnAttente)
            {
                TempData["Error"] = "Cette validation a déjà été effectuée.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            demande.StatutValidationDSI = StatutValidationCloture.Validee;
            demande.DateValidationDSI = DateTime.Now;

            await VerifierClotureComplete(demande);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_CLOTURE_DSI", "DemandeClotureProjet", demande.Id);

            TempData["Success"] = "Clôture validée par la DSI.";
            return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
        }

        // POST: Ajouter/Modifier commentaire technique (Responsable Solutions IT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterCommentaireTechnique(Guid id, string commentaireTechnique)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanEditTechnicalCommentAsync(projet))
            {
                return Forbid();
            }

            var userId = User.GetUserIdOrThrow();

            projet.CommentaireTechnique = commentaireTechnique?.Trim() ?? string.Empty;
            projet.DateDernierCommentaireTechnique = DateTime.Now;
            projet.DernierCommentaireTechniqueParId = userId;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("AJOUT_COMMENTAIRE_TECHNIQUE", "Projet", projet.Id,
                null,
                new { CommentaireTechnique = commentaireTechnique });

            TempData["Success"] = "Commentaire technique enregistré avec succès.";
            return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
        }

        // POST: Rejeter Clôture - DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RejeterClotureDSI(Guid demandeClotureId, string commentaire)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateClotureDsiAsync(userId))
            {
                return Forbid();
            }

            if (demande.StatutValidationDSI != StatutValidationCloture.EnAttente)
            {
                TempData["Error"] = "Cette validation a déjà été effectuée.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            // Le commentaire est obligatoire
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la clôture.";
                return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
            }

            // Rejeter la clôture : retour en attente de clôture
            demande.StatutValidationDSI = StatutValidationCloture.Rejetee;
            demande.CommentaireDSI = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;

            // Remettre les validations Demandeur et DM en attente pour permettre une nouvelle soumission
            demande.StatutValidationDemandeur = StatutValidationCloture.EnAttente;
            demande.StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente;
            demande.DateValidationDemandeur = null;
            demande.DateValidationDirecteurMetier = null;
            demande.CommentaireDemandeur = string.Empty;
            demande.CommentaireDirecteurMetier = string.Empty;

            // Le projet reste en phase UAT/MEP ou Clôture selon son état
            if (demande.Projet.PhaseActuelle == PhaseProjet.ClotureLeconsApprises)
            {
                demande.Projet.PhaseActuelle = PhaseProjet.UatMep;
            }

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_CLOTURE_DSI", "DemandeClotureProjet", demande.Id,
                new { Commentaire = commentaire });

            TempData["Success"] = "Clôture rejetée. Le projet est retourné en attente de clôture.";
            return RedirectToAction(nameof(Details), new { id = demande.ProjetId, tab = "cloture" });
        }

        // POST: Forcer Statut Projet (Clôture ou Abandon)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ForcerStatutProjet(Guid id, string actionType, string commentaire)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanForceStatus)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "L'action et le commentaire sont obligatoires.";
                return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
            }

            var ancienStatut = projet.StatutProjet;
            var anciennePhase = projet.PhaseActuelle;

            if (actionType == "Cloture")
            {
                // Clôturer le projet (projet terminé avec succès)
                projet.StatutProjet = StatutProjet.Cloture;
                projet.DateFinReelle = DateTime.Now;
                projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

                // Mettre à jour les délégations ChefProjet actives
                var delegationsActives = await _db.DelegationsChefProjet
                    .Where(d => d.ProjetId == projet.Id &&
                               d.EstActive &&
                               d.DateFin == null &&
                               !d.EstSupprime)
                    .ToListAsync();

                foreach (var delegation in delegationsActives)
                {
                    delegation.DateFin = DateTime.Now;
                    delegation.EstActive = false;
                    delegation.DateModification = DateTime.Now;
                    delegation.ModifiePar = _currentUserService.Matricule;
                }

                await _auditService.LogActionAsync("FORCER_CLOTURE_PROJET", "Projet", projet.Id,
                    new { AncienStatut = ancienStatut, AnciennePhase = anciennePhase, Commentaire = commentaire },
                    new { NouveauStatut = projet.StatutProjet, NouvellePhase = projet.PhaseActuelle });
            }
            else if (actionType == "Abandon")
            {
                // Abandonner le projet (travaux arrêtés ou changement d'avis)
                projet.StatutProjet = StatutProjet.Annule;
                projet.DateFinReelle = DateTime.Now;

                // Mettre à jour les délégations ChefProjet actives
                var delegationsActives = await _db.DelegationsChefProjet
                    .Where(d => d.ProjetId == projet.Id &&
                               d.EstActive &&
                               d.DateFin == null &&
                               !d.EstSupprime)
                    .ToListAsync();

                foreach (var delegation in delegationsActives)
                {
                    delegation.DateFin = DateTime.Now;
                    delegation.EstActive = false;
                    delegation.DateModification = DateTime.Now;
                    delegation.ModifiePar = _currentUserService.Matricule;
                }

                await _auditService.LogActionAsync("FORCER_ABANDON_PROJET", "Projet", projet.Id,
                    new { AncienStatut = ancienStatut, AnciennePhase = anciennePhase, Commentaire = commentaire },
                    new { NouveauStatut = projet.StatutProjet });
            }
            else
            {
                TempData["Error"] = "Action invalide. Veuillez choisir Clôture ou Abandon.";
                return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Projet {actionType.ToLower()} avec succès.";
            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // Méthode privée pour vérifier si la clôture est complète
        // Validation stricte : Demandeur + Directeur Métier + DSI
        private async Task VerifierClotureComplete(DemandeClotureProjet demande)
        {
            if (demande.Projet.FicheProjet == null)
            {
                await _db.Entry(demande.Projet)
                    .Reference(p => p.FicheProjet)
                    .LoadAsync();
            }

            var validationComplete =
                demande.StatutValidationDemandeur == StatutValidationCloture.Validee &&
                demande.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
                demande.StatutValidationDSI == StatutValidationCloture.Validee;

            if (validationComplete && !demande.EstTerminee)
            {
                demande.EstTerminee = true;
                demande.DateClotureFinale = DateTime.Now;

                var projet = demande.Projet;
                var statutFinal = demande.Projet.FicheProjet?.StatutFinalCloture?.Trim();
                projet.StatutProjet = string.Equals(statutFinal, "Abandonné", StringComparison.OrdinalIgnoreCase)
                    ? StatutProjet.Annule
                    : StatutProjet.Cloture;
                projet.DateFinReelle = DateTime.Now;
                projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

                // Mettre à jour les délégations ChefProjet actives pour ce projet
                var delegationsActives = await _db.DelegationsChefProjet
                    .Where(d => d.ProjetId == projet.Id &&
                               d.EstActive &&
                               d.DateFin == null &&
                               !d.EstSupprime)
                    .ToListAsync();

                foreach (var delegation in delegationsActives)
                {
                    delegation.DateFin = DateTime.Now;
                    delegation.EstActive = false;
                    delegation.DateModification = DateTime.Now;
                    delegation.ModifiePar = _currentUserService.Matricule;
                }

                await _auditService.LogActionAsync("CLOTURE_PROJET", "Projet", projet.Id);
            }
        }
    }
}
