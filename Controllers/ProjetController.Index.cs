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
        public async Task<IActionResult> Index(Guid? directionId, Guid? chefProjetId, PhaseProjet? phase, StatutProjet? statut, EtatProjet? etat, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var currentUserDirectionId = await _projetQuery.GetUserDirectionIdAsync(userId);
            var canPortfolioAccess = await HasPortfolioGovernanceAccessAsync();
            var hasChefProjetScope = await HasChefProjetWorkflowAccessAsync();
            var hasDmScope = await HasDmWorkflowAccessAsync();
            var hasDemandeurScope = await HasDemandeurProjectAccessAsync();

            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet);

            // Appliquer le scope par rôle via le service
            query = await _projetQuery.AppliquerScopeAsync(
                query, userId, canPortfolioAccess,
                hasChefProjetScope, hasDmScope, hasDemandeurScope,
                currentUserDirectionId);

            // Filtres additionnels (portefeuille seulement)
            var vm = new ProjetIndexViewModel();

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
                // Créer un portefeuille par défaut
                portefeuille = new PortefeuilleProjet
                {
                    Id = Guid.NewGuid(),
                    Nom = "Portefeuille de Projet DSI",
                    ObjectifStrategiqueGlobal = "Assurer l'amélioration globale de l'efficacité opérationnelle et de la satisfaction des parties prenantes au Côte d'Ivoire Terminal.",
                    AvantagesAttendus = @"• Gestion automatisée des notes de frais pour une amélioration de l'efficacité opérationnelle
• Mobilité des employés optimisée avec un système de réservation de bus efficace
• Opérations du centre médical optimisées
• Qualité du parcours client améliorée
• Suivi amélioré de la gestion des équipements
• Flux de contenu vers le scanner optimisé
• Suivi amélioré des demandes, conduisant à une meilleure visibilité des besoins et anticipation des ressources
• Efficacité de prise de décision améliorée grâce à la classification et priorisation des demandes basées sur des critères définis (budget, impact, urgence)
• Support utilisateur amélioré, réduisant le temps de résolution des incidents et augmentant la satisfaction utilisateur
• Gouvernance IT améliorée grâce à la mise en place de processus alignés avec les meilleures pratiques ITIL
• Gestion de la formation optimisée, incluant une planification centralisée, réduction des conflits d'horaires et suivi détaillé des sessions
• Conformité aux politiques de gouvernance IT, cybersécurité et groupe
• Sécurité et autonomie renforcées des échanges de données avec les partenaires externes via une solution SFTP",
                    RisquesEtMitigations = @"Résistance des utilisateurs au changement: Atténué par la sensibilisation des utilisateurs, la formation et le support de déploiement
Retards possibles dans la livraison des composants logiciels: Atténué par la mise en place d'un processus de validation avec des critères clairs et un cadre commun
Risque d'interruption de service pendant la transition: Atténué par une planification détaillée avec une phase de test avant la migration finale et des plans de retour en arrière, ainsi que des campagnes de communication et de sensibilisation actives
Perte ou corruption de données pendant la migration: Atténué par un plan complet de sauvegarde et de restauration, et validation des données à chaque étape de migration
Risques de sécurité liés aux échanges de données sensibles: Atténué par l'implémentation de solutions sécurisées et conformes aux politiques de sécurité",
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

            var vmPortefeuille = new PortefeuilleViewModel
            {
                Projets         = projets,
                Portefeuille    = portefeuille,
                DemandesEnCours = demandesEnCours
            };
            return View(vmPortefeuille);
        }

        // POST: Mettre à jour le portefeuille
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdatePortefeuille(Guid id, string ObjectifStrategiqueGlobal, string AvantagesAttendus, string RisquesEtMitigations)
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

            if (string.IsNullOrWhiteSpace(AvantagesAttendus))
            {
                ModelState.AddModelError("AvantagesAttendus", "Les avantages attendus sont requis.");
            }

            if (string.IsNullOrWhiteSpace(RisquesEtMitigations))
            {
                ModelState.AddModelError("RisquesEtMitigations", "Les risques et mitigations sont requis.");
            }

            if (ModelState.IsValid)
            {
                portefeuille.ObjectifStrategiqueGlobal = ObjectifStrategiqueGlobal.Trim();
                portefeuille.AvantagesAttendus = AvantagesAttendus.Trim();
                portefeuille.RisquesEtMitigations = RisquesEtMitigations.Trim();
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
