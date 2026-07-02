using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // GET: Liste des projets
        public async Task<IActionResult> Index(
            Guid? directionId, Guid? chefProjetId, PhaseProjet? phase, StatutProjet? statut, EtatProjet? etat,
            int page = 1, int pageSize = 20, bool afficherSupprimes = false)
        {
            var userId = User.GetUserIdOrThrow();
            var currentUserDirectionId = await _projetQuery.GetUserDirectionIdAsync(userId);
            var canPortfolioAccess = await HasPortfolioGovernanceAccessAsync();
            var hasChefProjetScope = await HasChefProjetWorkflowAccessAsync();
            var hasDmScope = await HasDmWorkflowAccessAsync();
            var hasDemandeurScope = await HasDemandeurProjectAccessAsync();
            var isAdminIT = User.IsInRole(nameof(RoleUtilisateur.AdminIT));
            afficherSupprimes = isAdminIT && afficherSupprimes;

            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet);

            // Le filtre global "EstSupprime == false" (voir ApplicationDbContext.AppliquerFiltreSoftDelete)
            // s'applique par défaut à toute requête sur Projets ; on le désactive explicitement
            // pour afficher aussi les projets supprimés dans la corbeille (réservé AdminIT).
            if (afficherSupprimes)
                query = query.IgnoreQueryFilters();

            // Appliquer le scope par rôle via le service
            query = await _projetQuery.AppliquerScopeAsync(
                query, userId, canPortfolioAccess,
                hasChefProjetScope, hasDmScope, hasDemandeurScope,
                currentUserDirectionId);

            // Filtres additionnels (portefeuille seulement)
            var vm = new ProjetIndexViewModel
            {
                CanGererCorbeille = isAdminIT,
                AfficherSupprimes = afficherSupprimes
            };

            if (canPortfolioAccess)
            {
                if (directionId.HasValue)   query = query.Where(p => p.DirectionId == directionId.Value);
                if (chefProjetId.HasValue)  query = query.Where(p => p.ChefProjetId == chefProjetId.Value);
                if (phase.HasValue)         query = query.Where(p => p.PhaseActuelle == phase.Value);
                if (statut.HasValue)        query = query.Where(p => p.StatutProjet == statut.Value);
                if (etat.HasValue)          query = query.Where(p => p.EtatProjet == etat.Value);

                // Charger les listes de référence pour les filtres via le service
                var filtres = await _projetQuery.ChargerFiltresAsync(directionId, chefProjetId, phase, statut, etat);
                vm.Directions  = filtres.Directions;
                vm.ChefsProjet = filtres.ChefsProjet;
                vm.Phases      = filtres.Phases;
                vm.Statuts     = filtres.Statuts;
                vm.Etats       = filtres.Etats;
            }

            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var pagedResult = await query
                .OrderByDescending(p => p.DateCreation)
                .ToPagedResultAsync(page, pageSize);

            vm.PageNumber = pagedResult.PageNumber;
            vm.TotalPages = pagedResult.TotalPages;
            vm.TotalCount = pagedResult.TotalCount;
            vm.PageSize   = pagedResult.PageSize;

            var projets = pagedResult.Items;
            vm.Projets = projets;

            if (hasDmScope && !canPortfolioAccess)
            {
                vm.ReadOnlyProjets = projets
                    .Where(p => p.SponsorId != userId)
                    .Select(p => p.Id)
                    .ToHashSet();
            }

            return View(vm);
        }

        // GET: Historique des projets pour Directeur Métier
        [Authorize]
        public async Task<IActionResult> HistoriqueDM(
            string? recherche = null,
            Guid? directionId = null,
            PhaseProjet? phase = null,
            StatutProjet? statut = null,
            int page = 1, int pageSize = 10)
        {
            var userId = User.GetUserIdOrThrow();
            var canPortfolioAccess = await HasPortfolioGovernanceAccessAsync();
            var hasDmScope = await HasDmWorkflowAccessAsync();
            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);

            if (!canPortfolioAccess && !hasDmScope)
                return Forbid();

            var vmHistorique = await _projetQuery.BuildHistoriqueDMAsync(
                recherche, directionId, phase, statut, page, pageSize,
                userId, canPortfolioAccess, currentUserDirectionId);

            return View(vmHistorique);
        }

        // GET: Portefeuille de Projets (vue stratégique)
        [Authorize]
        public async Task<IActionResult> Portefeuille()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            // Récupérer le portefeuille actif ou créer un par défaut
            var portefeuille = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuille == null)
            {
                // Créer un portefeuille par défaut (sans données de démonstration : les avantages
                // attendus et les risques sont désormais dérivés automatiquement des projets réels).
                portefeuille = new PortefeuilleProjet
                {
                    Id = Guid.NewGuid(),
                    Nom = "Portefeuille de Projet DSI",
                    ObjectifStrategiqueGlobal = string.Empty,
                    AvantagesAttendus = string.Empty,
                    RisquesEtMitigations = string.Empty,
                    EstActif = true,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };
                _db.PortefeuillesProjets.Add(portefeuille);
                await _db.SaveChangesAsync();
            }

            // Récupérer tous les projets du portefeuille actif (ou tous si aucun portefeuille)
            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Where(p => !p.EstSupprime && (portefeuille == null || p.PortefeuilleProjetId == portefeuille.Id))
                .OrderBy(p => p.Titre)
                .ToListAsync();

            // Récupérer toutes les demandes en cours (non validées ou non rejetées définitivement)
            var demandesEnCours = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande != StatutDemande.ValideeParDSI &&
                           d.StatutDemande != StatutDemande.RejeteeParDirecteurMetier &&
                           d.StatutDemande != StatutDemande.RejeteeParDSI)
                .OrderByDescending(d => d.DateSoumission)
                .ToListAsync();

            // Avantages attendus : repris directement des demandes d'origine des projets du portefeuille.
            var avantagesParProjet = projets
                .Where(p => !string.IsNullOrWhiteSpace(p.DemandeProjet?.AvantagesAttendus))
                .Select(p => new AvantageProjetPortefeuille
                {
                    ProjetTitre = p.Titre,
                    AvantagesAttendus = p.DemandeProjet!.AvantagesAttendus
                })
                .ToList();

            // Risques et mitigations : issus des risques réellement saisis sur chaque projet.
            var projetIds = projets.Select(p => p.Id).ToList();
            var risquesProjets = await _db.RisquesProjets
                .Where(r => !r.EstSupprime && projetIds.Contains(r.ProjetId))
                .OrderBy(r => r.DateCreationRisque)
                .ToListAsync();
            var risquesParProjet = projets
                .Select(p => new RisquesProjetPortefeuille
                {
                    ProjetTitre = p.Titre,
                    Risques = risquesProjets.Where(r => r.ProjetId == p.Id).ToList()
                })
                .Where(g => g.Risques.Count > 0)
                .ToList();

            var vmPortefeuille = new PortefeuilleViewModel
            {
                Projets          = projets,
                Portefeuille     = portefeuille,
                DemandesEnCours  = demandesEnCours,
                AvantagesParProjet = avantagesParProjet,
                RisquesParProjet   = risquesParProjet
            };
            return View(vmPortefeuille);
        }

        // POST: Mettre à jour le portefeuille
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdatePortefeuille(Guid id, string ObjectifStrategiqueGlobal)
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var portefeuille = await _db.PortefeuillesProjets.FindAsync(id);
            if (portefeuille == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(ObjectifStrategiqueGlobal))
            {
                ModelState.AddModelError("ObjectifStrategiqueGlobal", "L'objectif stratégique global est requis.");
            }

            if (ModelState.IsValid)
            {
                portefeuille.ObjectifStrategiqueGlobal = ObjectifStrategiqueGlobal.Trim();
                portefeuille.DateModification = DateTime.Now;
                portefeuille.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_PORTEFEUILLE", "PortefeuilleProjet", portefeuille.Id);

                TempData["Success"] = "Portefeuille mis à jour avec succès.";
                return RedirectToAction(nameof(Portefeuille));
            }

            // En cas d'erreur, recharger les données
            var projetsErreur = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            var vmErreur = new PortefeuilleViewModel
            {
                Projets      = projetsErreur,
                Portefeuille = portefeuille,
            };
            return View("Portefeuille", vmErreur);
        }
    }
}
