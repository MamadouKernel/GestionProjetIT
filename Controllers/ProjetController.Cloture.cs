using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Results;
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

            var result = await _clotureWorkflow.DemanderClotureAsync(id, userId, commentaire, dateSouhaiteeCloture);
            return MapClotureWorkflowToProjectDetails(result, id, "cloture");
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
            var isAdminIT = User.IsInRole(nameof(RoleUtilisateur.AdminIT));
            if (!isAdminIT &&
                (!await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDemandeur") ||
                demande.Projet.DemandeProjet?.DemandeurId != userId))
                return Forbid();

            var result = await _clotureWorkflow.ValiderClotureDemandeurAsync(demandeClotureId);
            return MapClotureWorkflowToProjectDetails(result, demande.ProjetId, "cloture");
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

            var result = await _clotureWorkflow.ValiderClotureDmAsync(demandeClotureId);
            return MapClotureWorkflowToProjectDetails(result, demande.ProjetId, "cloture");
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

            var result = await _clotureWorkflow.RejeterClotureDmAsync(demandeClotureId, commentaire);
            return MapClotureWorkflowToProjectDetails(result, demande.ProjetId, "cloture");
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

            var result = await _clotureWorkflow.ValiderClotureDsiAsync(demandeClotureId);
            return MapClotureWorkflowToProjectDetails(result, demande.ProjetId, "cloture");
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
            var result = await _clotureWorkflow.AjouterCommentaireTechniqueAsync(id, userId, commentaireTechnique);
            return MapClotureWorkflowToProjectDetails(result, id, "analyse");
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

            var result = await _clotureWorkflow.RejeterClotureDsiAsync(demandeClotureId, commentaire);
            return MapClotureWorkflowToProjectDetails(result, demande.ProjetId, "cloture");
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

            var result = await _clotureWorkflow.ForcerStatutProjetAsync(id, actionType, commentaire);
            return MapClotureWorkflowToProjectDetails(result, id, "synthese");
        }

        private IActionResult MapClotureWorkflowToProjectDetails(WorkflowResult result, Guid projetId, string tab)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;
            else if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id = projetId, tab });
        }
    }
}
