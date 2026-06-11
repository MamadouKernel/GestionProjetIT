using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Security;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Infrastructure.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class ProjetController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IAuditService _auditService;
        private readonly IPdfService _pdfService;
        private readonly IExcelService _excelService;
        private readonly IWordService _wordService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly ILivrableValidationService _livrableValidationService;
        private readonly IRAGCalculationService _ragCalculationService;
        private readonly ICacheService _cacheService;
        private readonly IUatValidationService _uatValidation;
        private readonly ICollaborationProjetService _collaboration;
        private readonly IElectronicSignatureService _electronicSignature;
        private readonly IProjetQueryService _projetQuery;

        public ProjetController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IAuditService auditService,
            IPdfService pdfService,
            IExcelService excelService,
            IWordService wordService,
            ICurrentUserService currentUserService,
            IPermissionService permissionService,
            INotificationService notificationService,
            ILivrableValidationService livrableValidationService,
            IRAGCalculationService ragCalculationService,
            ICacheService cacheService,
            IUatValidationService uatValidation,
            ICollaborationProjetService collaboration,
            IElectronicSignatureService electronicSignature,
            IProjetQueryService projetQuery)
        {
            _db = db;
            _fileStorage = fileStorage;
            _auditService = auditService;
            _pdfService = pdfService;
            _excelService = excelService;
            _wordService = wordService;
            _currentUserService = currentUserService;
            _permissionService = permissionService;
            _notificationService = notificationService;
            _livrableValidationService = livrableValidationService;
            _ragCalculationService = ragCalculationService;
            _cacheService = cacheService;
            _uatValidation = uatValidation;
            _collaboration = collaboration;
            _electronicSignature = electronicSignature;
            _projetQuery = projetQuery;
        }

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
            if (canPortfolioAccess)
            {
                if (directionId.HasValue)   query = query.Where(p => p.DirectionId == directionId.Value);
                if (chefProjetId.HasValue)  query = query.Where(p => p.ChefProjetId == chefProjetId.Value);
                if (phase.HasValue)         query = query.Where(p => p.PhaseActuelle == phase.Value);
                if (statut.HasValue)        query = query.Where(p => p.StatutProjet == statut.Value);
                if (etat.HasValue)          query = query.Where(p => p.EtatProjet == etat.Value);

                // Charger les listes de référence pour les filtres via le service
                var filtres = await _projetQuery.ChargerFiltresAsync(directionId, chefProjetId, phase, statut, etat);
                ViewBag.Directions      = filtres.Directions;
                ViewBag.ChefsProjet     = filtres.ChefsProjet;
                ViewBag.Phases          = filtres.Phases;
                ViewBag.Statuts         = filtres.Statuts;
                ViewBag.Etats           = filtres.Etats;
                ViewBag.SelectedDirectionId  = directionId;
                ViewBag.SelectedChefProjetId = chefProjetId;
                ViewBag.SelectedPhase        = phase;
                ViewBag.SelectedStatut       = statut;
            }

            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var pagedResult = await query
                .OrderByDescending(p => p.DateCreation)
                .ToPagedResultAsync(page, pageSize);

            ViewBag.PageNumber = pagedResult.PageNumber;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.PageSize   = pagedResult.PageSize;

            var projets = pagedResult.Items;

            if (hasDmScope && !canPortfolioAccess)
            {
                ViewBag.ReadOnlyProjets = projets
                    .Where(p => p.SponsorId != userId)
                    .Select(p => p.Id)
                    .ToHashSet();
            }

            return View(projets);
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

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.HistoriquePhases)
                    .ThenInclude(h => h.ModifieParUtilisateur);

            if (!canPortfolioAccess)
            {
                query = currentUserDirectionId.HasValue
                    ? query.Where(p => p.SponsorId == userId || p.DirectionId == currentUserDirectionId.Value)
                    : query.Where(p => p.SponsorId == userId);
            }

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(p => p.Titre.Contains(recherche) || p.CodeProjet.Contains(recherche));

            if (directionId.HasValue)
                query = query.Where(p => p.DirectionId == directionId.Value);

            if (phase.HasValue)
                query = query.Where(p => p.PhaseActuelle == phase.Value);

            if (statut.HasValue)
                query = query.Where(p => p.StatutProjet == statut.Value);

            var pagedProjets = await query.OrderByDescending(p => p.DateCreation).ToPagedResultAsync(page, pageSize);
            var projets = pagedProjets.Items;

            ViewBag.PageNumber    = pagedProjets.PageNumber;
            ViewBag.TotalPages    = pagedProjets.TotalPages;
            ViewBag.TotalCount    = pagedProjets.TotalCount;
            ViewBag.PageSize      = pagedProjets.PageSize;
            ViewBag.Recherche     = recherche;
            ViewBag.SelectedDirectionId = directionId;
            ViewBag.SelectedPhase = phase;
            ViewBag.SelectedStatut = statut;

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // Optimisation : charger les données uniquement pour la page courante
            var projetIds = projets.Select(p => p.Id).ToList();
            
            // Charger tous les logs d'audit en une seule requête
            var allAuditLogsList = await _db.AuditLogs
                .Include(a => a.Utilisateur)
                .Where(a => a.Entite == "Projet" && projetIds.Any(id => a.EntiteId == id.ToString()))
                .ToListAsync();
            
            var allAuditLogs = allAuditLogsList
                .Where(a => Guid.TryParse(a.EntiteId, out var id) && projetIds.Contains(id))
                .GroupBy(a => Guid.Parse(a.EntiteId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.DateAction).ToList());

            // Charger toutes les statistiques en une seule requête par type
            var livrablesCounts = await _db.LivrablesProjets
                .Where(l => projetIds.Contains(l.ProjetId))
                .GroupBy(l => l.ProjetId)
                .Select(g => new { ProjetId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjetId, x => x.Count);

            var anomaliesCounts = await _db.AnomaliesProjets
                .Where(a => projetIds.Contains(a.ProjetId))
                .GroupBy(a => a.ProjetId)
                .Select(g => new 
                { 
                    ProjetId = g.Key, 
                    Total = g.Count(),
                    Ouvertes = g.Count(a => a.Statut == Domain.Enums.StatutAnomalie.Ouverte)
                })
                .ToDictionaryAsync(x => x.ProjetId, x => new { x.Total, x.Ouvertes });

            var risquesCounts = await _db.RisquesProjets
                .Where(r => projetIds.Contains(r.ProjetId))
                .GroupBy(r => r.ProjetId)
                .Select(g => new 
                { 
                    ProjetId = g.Key, 
                    Total = g.Count(),
                    Critiques = g.Count(r => r.Impact == Domain.Enums.ImpactRisque.Critique)
                })
                .ToDictionaryAsync(x => x.ProjetId, x => new { x.Total, x.Critiques });

            // Construire la liste avec les données pré-chargées
            var projetsAvecHistorique = new List<dynamic>();
            
            foreach (var projet in projets)
            {
                var auditLogs = allAuditLogs.TryGetValue(projet.Id, out var logs) ? logs : new List<Domain.Models.AuditLog>();
                var totalLivrables = livrablesCounts.TryGetValue(projet.Id, out var livCount) ? livCount : 0;
                var totalAnomalies = anomaliesCounts.TryGetValue(projet.Id, out var anomCount) ? anomCount.Total : 0;
                var anomaliesOuvertes = anomaliesCounts.TryGetValue(projet.Id, out var anomCount2) ? anomCount2.Ouvertes : 0;
                var totalRisques = risquesCounts.TryGetValue(projet.Id, out var riskCount) ? riskCount.Total : 0;
                var risquesCritiques = risquesCounts.TryGetValue(projet.Id, out var riskCount2) ? riskCount2.Critiques : 0;

                projetsAvecHistorique.Add(new
                {
                    Projet = projet,
                    AuditLogs = auditLogs,
                    TotalLivrables = totalLivrables,
                    TotalAnomalies = totalAnomalies,
                    AnomaliesOuvertes = anomaliesOuvertes,
                    TotalRisques = totalRisques,
                    RisquesCritiques = risquesCritiques
                });
            }

            ViewBag.ProjetsAvecHistorique = projetsAvecHistorique;
            return View(projets);
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

            ViewBag.Portefeuille = portefeuille;
            ViewBag.DemandesEnCours = demandesEnCours;
            return View(projets);
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
            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            ViewBag.Portefeuille = portefeuille;
            return View("Portefeuille", projets);
        }

        // GET: Détails projet avec onglets
        public async Task<IActionResult> Details(Guid id, string? tab = "synthese")
        {
            var userId = User.GetUserIdOrThrow();
            
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .Include(p => p.Anomalies)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .Include(p => p.HistoriquePhases)
                    .ThenInclude(h => h.ModifieParUtilisateur)
                .Include(p => p.DemandesCloture)
                .Include(p => p.DernierCommentaireTechniquePar)
                .Include(p => p.CharteValideeParDMUtilisateur)
                .Include(p => p.CharteValideeParDSIUtilisateur)
                .Include(p => p.CharteProjet)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanViewProject)
            {
                return Forbid();
            }

            ViewBag.IsReadOnly = ui.HasDmGovernanceAccess && !ui.IsProjectSponsor;

            await RecalculateProjectProgressAsync(projet, persistChanges: true);

            // Charger les cas de test pour l'onglet UAT
            if (tab == "uat")
            {
                var casTests = await _db.CasTestsProjets
                    .Include(c => c.Executions.OrderByDescending(e => e.DateExecution))
                    .Include(c => c.CampagneTestProjet)
                    .Where(c => c.ProjetId == id && !c.EstSupprime)
                    .OrderBy(c => c.Reference)
                    .ToListAsync();

                var campagnes = await _db.CampagnesTestsProjets
                    .Where(c => c.ProjetId == id && !c.EstSupprime)
                    .OrderByDescending(c => c.DateLancement)
                    .ToListAsync();

                ViewBag.CasTests = casTests;
                ViewBag.Campagnes = campagnes;
            }

            // Charger la collaboration pour les onglets collaboration et exécution
            if (tab == "collaboration" || tab == "execution")
            {
                var collaboration = await _db.CollaborationsProjets
                    .Include(c => c.Taches.OrderBy(t => t.Phase))
                    .FirstOrDefaultAsync(c => c.ProjetId == id && !c.EstSupprime);

                ViewBag.Collaboration = collaboration;
            }

            // Charger les dossiers de signature pour l'onglet planification
            if (tab == "planification")
            {
                var dossiers = await _db.DossiersSignatureProjets
                    .Include(d => d.Signataires.OrderBy(s => s.OrdreSignature))
                        .ThenInclude(s => s.Utilisateur)
                    .Where(d => d.ProjetId == id && !d.EstSupprime)
                    .OrderByDescending(d => d.DateCreation)
                    .ToListAsync();

                ViewBag.DossiersSignature = dossiers;
            }

            // Charger l'historique complet (logs d'audit) pour l'onglet historique
            if (tab == "historique")
            {
                var auditLogs = await _db.AuditLogs
                    .Include(a => a.Utilisateur)
                    .Where(a => a.Entite == "Projet" && a.EntiteId == id.ToString())
                    .OrderByDescending(a => a.DateAction)
                    .ToListAsync();

                ViewBag.AuditLogs = auditLogs;
            }

            if (ui.CanReassignChefProjet)
            {
                // Récupérer les chefs de projet (utilisateurs avec rôle ChefDeProjet)
                var chefsProjet = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToListAsync();

                // Récupérer les utilisateurs avec délégation active ChefProjet pour ce projet
                var delegationsActives = await _db.DelegationsChefProjet
                    .Include(d => d.Delegue)
                    .Where(d => !d.EstSupprime &&
                                d.EstActive &&
                                d.ProjetId == id &&
                                d.DateDebut <= DateTime.Now &&
                                d.DateFin == null)
                    .Select(d => d.Delegue!)
                    .Where(u => !u.EstSupprime)
                    .ToListAsync();

                // Combiner les deux listes et supprimer les doublons
                var tousChefsProjet = chefsProjet
                    .Union(delegationsActives)
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToList();

                ViewBag.ChefsProjet = tousChefsProjet;
            }

            if (ui.IsAssignedChefProjet)
            {
                // Vérifier s'il existe déjà un log de prise en charge pour ce projet et ce chef
                var priseEnChargeExiste = await _db.AuditLogs
                    .AnyAsync(a => a.Entite == "Projet" && 
                                  a.EntiteId == id.ToString() && 
                                  a.TypeAction == "PRISE_EN_CHARGE_PROJET" &&
                                  a.UtilisateurId == userId);

                if (!priseEnChargeExiste)
                {
                    await _auditService.LogActionAsync("PRISE_EN_CHARGE_PROJET", "Projet", projet.Id,
                        null,
                        new { ChefProjetId = userId, CodeProjet = projet.CodeProjet });
                }
            }

            ViewBag.ActiveTab = tab;
            return View(projet);
        }

        // POST: Mettre à jour le ResponsableSolutionsIT (ChefProjet)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateChefProjet(Guid id, Guid? chefProjetId)
        {
            var projet = await _db.Projets
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            // Vérifier que le projet n'est pas clôturé
            if (projet.StatutProjet == StatutProjet.Cloture)
            {
                TempData["Error"] = "Impossible de modifier le ResponsableSolutionsIT d'un projet clôturé.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienChefProjetId = projet.ChefProjetId;
            var ancienChefProjetNom = projet.ChefProjet != null ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}" : "Aucun";

            // Si un chef de projet est spécifié, vérifier qu'il existe et est valide
            if (chefProjetId.HasValue)
            {
                var chefProjet = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles.Where(ur => !ur.EstSupprime))
                    .FirstOrDefaultAsync(u => u.Id == chefProjetId.Value && !u.EstSupprime);

                if (chefProjet == null)
                {
                    TempData["Error"] = "Le ResponsableSolutionsIT sélectionné n'existe pas.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Vérifier que l'utilisateur a le rôle ChefDeProjet OU a une délégation active pour ce projet
                bool isValidChefProjet = chefProjet.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet);
                
                if (!isValidChefProjet)
                {
                    // Vérifier s'il a une délégation active pour ce projet
                    var delegationActive = await _db.DelegationsChefProjet
                        .AnyAsync(d => d.DelegueId == chefProjetId.Value && 
                                      d.ProjetId == id && 
                                      d.EstActive && 
                                      d.DateFin == null && 
                                      !d.EstSupprime);

                    if (!delegationActive)
                    {
                        TempData["Error"] = "Le ResponsableSolutionsIT sélectionné n'est pas valide (doit être ChefDeProjet ou avoir une délégation active pour ce projet).";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }
            }

            // Si l'ancien chef de projet change, enregistrer la fin dans l'historique
            if (ancienChefProjetId.HasValue && ancienChefProjetId != chefProjetId)
            {
                var historiqueAncienChef = await _db.HistoriqueChefProjets
                    .Where(h => h.ProjetId == id && h.ChefProjetId == ancienChefProjetId.Value && h.DateFin == null)
                    .FirstOrDefaultAsync();
                
                if (historiqueAncienChef != null)
                {
                    historiqueAncienChef.DateFin = DateTime.Now;
                    historiqueAncienChef.DateModification = DateTime.Now;
                    historiqueAncienChef.ModifiePar = _currentUserService.Matricule;
                }
            }

            projet.ChefProjetId = chefProjetId;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule ?? "SYSTEM";

            await _db.SaveChangesAsync();

            // Si un nouveau chef de projet est assigné, créer une entrée dans l'historique
            if (chefProjetId.HasValue && ancienChefProjetId != chefProjetId)
            {
                var historiqueChefProjet = new HistoriqueChefProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    ChefProjetId = chefProjetId.Value,
                    DateDebut = DateTime.Now,
                    DateFin = null,
                    Commentaire = "Assignation comme chef de projet",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                };

                _db.HistoriqueChefProjets.Add(historiqueChefProjet);
                await _db.SaveChangesAsync();
            }

            var nouveauChefProjetNom = chefProjetId.HasValue 
                ? await _db.Utilisateurs
                    .Where(u => u.Id == chefProjetId.Value)
                    .Select(u => $"{u.Nom} {u.Prenoms}")
                    .FirstOrDefaultAsync()
                : "Aucun";

            await _auditService.LogActionAsync("UPDATE_CHEFPROJET", "Projet", projet.Id,
                new { AncienChefProjetId = ancienChefProjetId, AncienChefProjet = ancienChefProjetNom },
                new { NouveauChefProjetId = chefProjetId, NouveauChefProjet = nouveauChefProjetNom });

            TempData["Success"] = chefProjetId.HasValue 
                ? $"ResponsableSolutionsIT mis à jour : {nouveauChefProjetNom}"
                : "ResponsableSolutionsIT retiré du projet.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Valider Phase Analyse (Go/No-Go vers Planification)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderPhaseAnalyse(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);

            if (!ui.CanValidateAnalysePhase && !ui.CanChangePhase)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La phase Analyse ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.PlanificationValidation);

            var blocagesAnalyse = BuildAnalyseBlockingItems(projet, validationLivrables);
            if (blocagesAnalyse.Count > 0)
            {
                TempData["ErrorHtml"] = BuildAnalyseBlockingAlertHtml(blocagesAnalyse);
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }

            if (!projet.CharteValidee)
            {
            projet.CharteValidee = true;
            projet.DateCharteValidee = DateTime.Now;
            }
            
            // Passer à la phase Planification
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            if (projet.StatutProjet == StatutProjet.NonDemarre || projet.StatutProjet == StatutProjet.EnCours)
            {
                projet.StatutProjet = StatutProjet.EnCours;
            }

            // Ajouter une entrée à l'historique des phases
            _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.PlanificationValidation,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Validation de la phase Analyse - Passage en phase Planification",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            });

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PHASE_ANALYSE", "Projet", projet.Id,
                new { AnciennePhase = PhaseProjet.AnalyseClarification },
                new { NouvellePhase = PhaseProjet.PlanificationValidation });

            TempData["Success"] = "Phase Analyse validée. Le projet passe en phase Planification.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // GET: Afficher/Éditer la charte de projet
        [Authorize]
        public async Task<IActionResult> CharteProjet(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanViewProject)
            {
                return Forbid();
            }

            var charte = await _db.CharteProjets
                .Include(c => c.Demandeur)
                .Include(c => c.ChefProjet)
                .Include(c => c.SignatureSponsorUtilisateur)
                .Include(c => c.SignatureChefProjetUtilisateur)
                .Include(c => c.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                .Include(c => c.PartiesPrenantes.Where(p => !p.EstSupprime))
                .FirstOrDefaultAsync(c => c.ProjetId == id && !c.EstSupprime);

            // Si la charte n'existe pas, créer une structure par défaut
            if (charte == null)
            {
                charte = new CharteProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    NomProjet = projet.Titre,
                    NumeroProjet = projet.CodeProjet,
                    ObjectifProjet = projet.Objectif ?? projet.DemandeProjet?.Objectifs ?? string.Empty,
                    DemandeurId = projet.DemandeProjet?.DemandeurId ?? Guid.Empty,
                    ChefProjetId = projet.ChefProjetId ?? Guid.Empty,
                    EmailChefProjet = projet.ChefProjet?.Email ?? string.Empty,
                    Sponsors = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}",
                    CodeDocument = $"CIT-CIV-DSI-CP-{projet.CodeProjet}-Rév.01",
                    TypeDocument = "Charte de projet",
                    Departement = "SYSTEME D'INFORMATION",
                    NumeroRevision = 1,
                    DateRevision = DateTime.Now,
                    DescriptionRevision = "Création",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };

                // Ajouter les jalons par défaut
                var jalonsDefaut = new[]
                {
                    new { Nom = "Cahier de charge validé", Description = "Le document détaillé décrivant les besoins et les attentes du projet est officiellement approuvé par les parties prenantes.", Criteres = "Signature du responsable du projet et des parties prenantes, aucune objection majeure ou confirmation par mail." },
                    new { Nom = "Appel d'offre lancé", Description = "La phase d'appel d'offres est officiellement lancée, invitant les fournisseurs potentiels à soumettre leurs propositions.", Criteres = "Documentation complète de l'appel d'offres, diffusion auprès des soumissionnaires potentiels." },
                    new { Nom = "Soumissionnaire choisi", Description = "Un soumissionnaire est sélectionné après l'évaluation des propositions reçues.", Criteres = "Analyse comparative des soumissions, recommandation du comité de sélection, approbation du responsable du projet." },
                    new { Nom = "Infra IT Mise en Place", Description = "L'infrastructure nécessaire au projet est mise en place, comprenant les serveurs, les réseaux, et autres composants essentiels.", Criteres = "Vérification de la disponibilité et de la fonctionnalité de l'infrastructure selon les prérequis" },
                    new { Nom = "Paiement Fournisseur Réalisé", Description = "Le paiement convenu avec le fournisseur est effectué conformément aux termes du contrat", Criteres = "Confirmation de la transaction financière, documentation appropriée du paiement" },
                    new { Nom = "Solution Déployée", Description = "La solution logicielle est installée et configurée conformément aux spécifications du cahier de charge", Criteres = "Réussite des tests de déploiement, absence de problèmes critiques" },
                    new { Nom = "Utilisateurs Formés", Description = "Les utilisateurs concernés sont formés à l'utilisation de la nouvelle solution", Criteres = "Tous les utilisateurs ont suivi la formation, évaluation positive des compétences acquises" },
                    new { Nom = "Solution Utilisée", Description = "La solution est pleinement opérationnelle et utilisée par les utilisateurs finaux", Criteres = "Confirmation de l'utilisation régulière de la solution dans le cadre des activités quotidiennes" }
                };

                for (int i = 0; i < jalonsDefaut.Length; i++)
                {
                    charte.Jalons.Add(new JalonCharte
                    {
                        Id = Guid.NewGuid(),
                        CharteProjetId = charte.Id,
                        Nom = jalonsDefaut[i].Nom,
                        Description = jalonsDefaut[i].Description,
                        CriteresApprobation = jalonsDefaut[i].Criteres,
                        Ordre = i + 1,
                        DateCreation = DateTime.Now,
                        CreePar = _currentUserService.Matricule ?? "SYSTEM",
                        EstSupprime = false
                    });
                }

                _db.CharteProjets.Add(charte);
                await _db.SaveChangesAsync();
            }

            ViewBag.Projet = projet;
            ViewBag.Utilisateurs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();
            ViewBag.CharteSigneeLivrable = await _db.LivrablesProjets
                .Include(l => l.DeposePar)
                .Where(l => l.ProjetId == id &&
                            l.TypeLivrable == TypeLivrable.CharteProjetSignee &&
                            !l.EstSupprime)
                .OrderByDescending(l => l.DateDepot)
                .FirstOrDefaultAsync();
            ViewBag.DossierSignatureCharte = await _db.DossiersSignatureProjets
                .Include(d => d.Signataires.OrderBy(s => s.OrdreSignature))
                    .ThenInclude(s => s.Utilisateur)
                .Where(d => d.ProjetId == id &&
                            d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                            !d.EstSupprime)
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefaultAsync();

            return View(charte);
        }

        // POST: Sauvegarder la charte de projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SauvegarderCharteProjet(Guid id, CharteProjet charte, 
            List<JalonCharte>? Jalons = null, List<PartiePrenanteCharte>? PartiesPrenantes = null)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            NormalizeCharteProjetForPersistence(charte);

            if (!await CanManageAnalyseAsync(projet))
            {
                return Forbid();
            }

            var userId = User.GetUserIdOrThrow();

            var charteExistante = await _db.CharteProjets
                .Include(c => c.Jalons)
                .Include(c => c.PartiesPrenantes)
                .FirstOrDefaultAsync(c => c.ProjetId == id);

            if (charteExistante == null)
            {
                charte.Id = Guid.NewGuid();
                charte.ProjetId = id;
                charte.DateCreation = DateTime.Now;
                charte.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                charte.EstSupprime = false;
                
                // Ajouter les jalons
                if (Jalons != null)
                {
                    foreach (var jalon in Jalons.Where(j => !string.IsNullOrWhiteSpace(j.Nom)))
                    {
                        jalon.Id = jalon.Id == Guid.Empty ? Guid.NewGuid() : jalon.Id;
                        jalon.CharteProjetId = charte.Id;
                        jalon.DateCreation = DateTime.Now;
                        jalon.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                        jalon.EstSupprime = false;
                        charte.Jalons.Add(jalon);
                    }
                }
                
                // Ajouter les parties prenantes
                if (PartiesPrenantes != null)
                {
                    foreach (var partie in PartiesPrenantes.Where(p => !string.IsNullOrWhiteSpace(p.Nom)))
                    {
                        partie.Id = partie.Id == Guid.Empty ? Guid.NewGuid() : partie.Id;
                        partie.CharteProjetId = charte.Id;
                        partie.DateCreation = DateTime.Now;
                        partie.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                        partie.EstSupprime = false;
                        charte.PartiesPrenantes.Add(partie);
                    }
                }
                
                _db.CharteProjets.Add(charte);
            }
            else
            {
                // Mettre à jour les champs
                charteExistante.NomProjet = charte.NomProjet;
                charteExistante.NumeroProjet = charte.NumeroProjet;
                charteExistante.ObjectifProjet = charte.ObjectifProjet;
                charteExistante.AssuranceQualite = charte.AssuranceQualite;
                charteExistante.Perimetre = charte.Perimetre;
                charteExistante.ContraintesInitiales = charte.ContraintesInitiales;
                charteExistante.RisquesInitiaux = charte.RisquesInitiaux;
                charteExistante.Sponsors = charte.Sponsors;
                charteExistante.EmailChefProjet = charte.EmailChefProjet;
                charteExistante.CodeDocument = charte.CodeDocument;
                charteExistante.DescriptionRevision = charte.DescriptionRevision;
                charteExistante.RedigePar = charte.RedigePar;
                charteExistante.VerifiePar = charte.VerifiePar;
                charteExistante.ApprouvePar = charte.ApprouvePar;
                charteExistante.DemandeurId = charte.DemandeurId;
                charteExistante.ChefProjetId = charte.ChefProjetId;
                charteExistante.DateModification = DateTime.Now;
                charteExistante.ModifiePar = _currentUserService.Matricule;

                // Gérer les jalons
                if (Jalons != null)
                {
                    var jalonsIds = Jalons.Where(j => j.Id != Guid.Empty).Select(j => j.Id).ToList();
                    var jalonsASupprimer = charteExistante.Jalons.Where(j => !jalonsIds.Contains(j.Id)).ToList();
                    foreach (var jalon in jalonsASupprimer)
                    {
                        jalon.EstSupprime = true;
                        jalon.DateModification = DateTime.Now;
                        jalon.ModifiePar = _currentUserService.Matricule;
                    }

                    foreach (var jalon in Jalons.Where(j => !string.IsNullOrWhiteSpace(j.Nom)))
                    {
                        if (jalon.Id == Guid.Empty)
                        {
                            // Nouveau jalon
                            var nouveauJalon = new JalonCharte
                            {
                                Id = Guid.NewGuid(),
                                CharteProjetId = charteExistante.Id,
                                Nom = jalon.Nom,
                                Description = jalon.Description,
                                CriteresApprobation = jalon.CriteresApprobation,
                                DatePrevisionnelle = jalon.DatePrevisionnelle,
                                Ordre = jalon.Ordre,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                EstSupprime = false
                            };
                            charteExistante.Jalons.Add(nouveauJalon);
                        }
                        else
                        {
                            // Mettre à jour jalon existant
                            var jalonExistant = charteExistante.Jalons.FirstOrDefault(j => j.Id == jalon.Id);
                            if (jalonExistant != null)
                            {
                                jalonExistant.Nom = jalon.Nom;
                                jalonExistant.Description = jalon.Description;
                                jalonExistant.CriteresApprobation = jalon.CriteresApprobation;
                                jalonExistant.DatePrevisionnelle = jalon.DatePrevisionnelle;
                                jalonExistant.Ordre = jalon.Ordre;
                                jalonExistant.DateModification = DateTime.Now;
                                jalonExistant.ModifiePar = _currentUserService.Matricule;
                            }
                        }
                    }
                }

                // Gérer les parties prenantes
                if (PartiesPrenantes != null)
                {
                    var partiesIds = PartiesPrenantes.Where(p => p.Id != Guid.Empty).Select(p => p.Id).ToList();
                    var partiesASupprimer = charteExistante.PartiesPrenantes.Where(p => !partiesIds.Contains(p.Id)).ToList();
                    foreach (var partie in partiesASupprimer)
                    {
                        partie.EstSupprime = true;
                        partie.DateModification = DateTime.Now;
                        partie.ModifiePar = _currentUserService.Matricule;
                    }

                    foreach (var partie in PartiesPrenantes.Where(p => !string.IsNullOrWhiteSpace(p.Nom)))
                    {
                        if (partie.Id == Guid.Empty)
                        {
                            // Nouvelle partie prenante
                            var nouvellePartie = new PartiePrenanteCharte
                            {
                                Id = Guid.NewGuid(),
                                CharteProjetId = charteExistante.Id,
                                Nom = partie.Nom,
                                Role = partie.Role,
                                UtilisateurId = partie.UtilisateurId,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                EstSupprime = false
                            };
                            charteExistante.PartiesPrenantes.Add(nouvellePartie);
                        }
                        else
                        {
                            // Mettre à jour partie prenante existante
                            var partieExistante = charteExistante.PartiesPrenantes.FirstOrDefault(p => p.Id == partie.Id);
                            if (partieExistante != null)
                            {
                                partieExistante.Nom = partie.Nom;
                                partieExistante.Role = partie.Role;
                                partieExistante.UtilisateurId = partie.UtilisateurId;
                                partieExistante.DateModification = DateTime.Now;
                                partieExistante.ModifiePar = _currentUserService.Matricule;
                            }
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();

            var workflowReset = false;
            if (projet.PhaseActuelle == PhaseProjet.AnalyseClarification)
            {
                var charteActive = charteExistante ?? charte;
                var signedLivrables = await _db.LivrablesProjets
                    .Where(l => l.ProjetId == id &&
                                l.TypeLivrable == TypeLivrable.CharteProjetSignee &&
                                !l.EstSupprime)
                    .ToListAsync();

                if (signedLivrables.Any() ||
                    charteActive.SignatureSponsor ||
                    charteActive.SignatureChefProjet ||
                    projet.CharteValidee ||
                    projet.CharteValideeParDM ||
                    projet.CharteValideeParDSI)
                {
                    var dossierSignature = await _db.DossiersSignatureProjets
                        .Include(d => d.Signataires)
                        .FirstOrDefaultAsync(d => d.ProjetId == id &&
                                                  d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                                  !d.EstSupprime);

                    foreach (var livrableSigne in signedLivrables)
                    {
                        livrableSigne.EstSupprime = true;
                        livrableSigne.DateModification = DateTime.Now;
                        livrableSigne.ModifiePar = _currentUserService.Matricule;
                    }

                    charteActive.SignatureSponsor = false;
                    charteActive.DateSignatureSponsor = null;
                    charteActive.SignatureSponsorId = null;
                    charteActive.SignatureImageSponsor = null;
                    charteActive.DateSignatureImageSponsor = null;
                    charteActive.SignatureChefProjet = false;
                    charteActive.DateSignatureChefProjet = null;
                    charteActive.SignatureChefProjetId = null;
                    charteActive.SignatureImageCP = null;
                    charteActive.DateSignatureImageCP = null;
                    charteActive.DateModification = DateTime.Now;
                    charteActive.ModifiePar = _currentUserService.Matricule;

                    if (dossierSignature != null)
                    {
                        dossierSignature.Statut = StatutDossierSignature.Brouillon;
                        dossierSignature.NomDocumentSigne = null;
                        dossierSignature.CheminDocumentSigne = null;
                        dossierSignature.DateEnvoi = null;
                        dossierSignature.DateFinalisation = null;
                        dossierSignature.DateExpiration = null;
                        dossierSignature.MessageStatut = "La charte a ete modifiee. Le circuit de signature doit etre relance.";
                        dossierSignature.DateModification = DateTime.Now;
                        dossierSignature.ModifiePar = _currentUserService.Matricule;

                        foreach (var signataire in dossierSignature.Signataires)
                        {
                            signataire.Statut = StatutSignataireDossierSignature.EnAttente;
                            signataire.DateSignature = null;
                            signataire.DateModification = DateTime.Now;
                            signataire.ModifiePar = _currentUserService.Matricule;
                        }
                    }

                    ResetCharteValidationState(projet);
                    projet.DateModification = DateTime.Now;
                    projet.ModifiePar = _currentUserService.Matricule;

                    await _db.SaveChangesAsync();
                    workflowReset = true;
                }
            }

            await _auditService.LogActionAsync("SAUVEGARDE_CHARTE_PROJET", "CharteProjet", charteExistante?.Id ?? charte.Id);

            TempData["Success"] = workflowReset
                ? "Charte de projet sauvegardée. La version signée et les validations ont été réinitialisées."
                : "Charte de projet sauvegardée avec succès.";
            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        // POST: Générer Word Charte (version complète)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererCharteCompletWord(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.Demandeur)
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.ChefProjet)
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                .Include(p => p.CharteProjet!)
                    .ThenInclude(c => c.PartiesPrenantes.Where(p => !p.EstSupprime))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (projet.CharteProjet == null)
            {
                TempData["Error"] = "La charte de projet n'a pas encore été créée.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            try
            {
                var wordBytes = await _wordService.GenerateCharteProjetWordAsync(projet.CharteProjet);
                var fileName = $"CharteProjet_Complet_{projet.CodeProjet}_{DateTime.Now:yyyyMMdd}.docx";
                return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du document Word: {ex.Message}";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }
        }

        // POST: Générer Word Fiche Projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererFicheProjetWord(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (projet.FicheProjet == null)
            {
                TempData["Error"] = "La fiche projet n'a pas encore été créée.";
                return RedirectToAction(nameof(FicheProjet), new { id });
            }

            try
            {
                var wordBytes = await _wordService.GenerateFicheProjetWordAsync(projet.FicheProjet);
                var fileName = $"FicheProjet_{projet.CodeProjet}_{DateTime.Now:yyyyMMdd}.docx";
                return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du document Word: {ex.Message}";
                return RedirectToAction(nameof(FicheProjet), new { id });
            }
        }

        // POST: Générer Excel Portefeuille
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererPortefeuilleExcel()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var portefeuille = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuille == null)
            {
                TempData["Error"] = "Aucun portefeuille actif trouvé.";
                return RedirectToAction(nameof(Portefeuille));
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Where(p => !p.EstSupprime && (p.PortefeuilleProjetId == portefeuille.Id || portefeuille == null))
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var excelBytes = await _excelService.GeneratePortefeuilleProjetsExcelAsync(portefeuille, projets);
                var fileName = $"PortefeuilleProjets_{DateTime.Now:yyyyMMdd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du fichier Excel: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer PDF Portefeuille
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererPortefeuillePdf()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var portefeuille = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuille == null)
            {
                TempData["Error"] = "Aucun portefeuille actif trouvé.";
                return RedirectToAction(nameof(Portefeuille));
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Where(p => !p.EstSupprime && p.PortefeuilleProjetId == portefeuille.Id)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var pdfBytes = await _pdfService.GeneratePortefeuilleProjetsPdfAsync(portefeuille, projets);
                var fileName = $"PortefeuilleProjets_{DateTime.Now:yyyyMMdd}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du fichier PDF: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer Rapport DSI/DG PDF
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererRapportDSIDGPdf()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.DemandeProjet)
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var pdfBytes = await _pdfService.GenerateRapportDSIDGPdfAsync(projets);
                var fileName = $"Rapport_DSI_DG_{DateTime.Now:yyyyMMdd}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du rapport PDF: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer Rapport DSI/DG Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererRapportDSIDGExcel()
        {
            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            var projets = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.DemandeProjet)
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Titre)
                .ToListAsync();

            try
            {
                var excelBytes = await _excelService.GenerateRapportDSIDGExcelAsync(projets);
                var fileName = $"Rapport_DSI_DG_{DateTime.Now:yyyyMMdd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du rapport Excel: {ex.Message}";
                return RedirectToAction(nameof(Portefeuille));
            }
        }

        // POST: Générer PDF Charte
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererChartePdf(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (!await CanManageAnalyseAsync(projet))
                return Forbid();

            var userId = User.GetUserIdOrThrow();

            try
            {
                var pdfBytes = await _pdfService.GenerateCharteProjetPdfAsync(projet);

                // Sauvegarder le PDF comme livrable
                var fileName = $"CharteProjet_{projet.CodeProjet}_{DateTime.Now:yyyyMMdd}.pdf";
                var filePath = Path.Combine("projets", projet.CodeProjet, "analyse", fileName);
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", filePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

                var livrable = new LivrableProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    Phase = PhaseProjet.AnalyseClarification,
                    TypeLivrable = TypeLivrable.CharteProjet,
                    NomDocument = fileName,
                    CheminRelatif = Path.Combine("uploads", filePath).Replace('\\', '/'),
                    DateDepot = DateTime.Now,
                    DeposeParId = userId,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };

                _db.LivrablesProjets.Add(livrable);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("GENERATION_CHARTE_PDF", "Projet", projet.Id);

                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du PDF: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> InitialiserDossierSignatureCharte(Guid id, FournisseurSignatureElectronique fournisseur)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanViewProjectAsync(projet))
            {
                return Forbid();
            }

            if (!await CanViewProjectAsync(projet))
            {
                return Forbid();
            }

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "Le dossier de signature de la charte ne peut être initialisé qu'en phase Analyse.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            try
            {
                var result = await _electronicSignature.InitialiserCharteAsync(id, fournisseur, _currentUserService.Matricule);
                TempData["Success"] = result != null
                    ? "Dossier de signature de la charte initialisé."
                    : "Impossible d'initialiser le dossier de signature.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de l'initialisation du dossier : {ex.Message}";
            }

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnvoyerDossierSignatureCharte(Guid id, Guid dossierSignatureId)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            var dossier = await _db.DossiersSignatureProjets
                .FirstOrDefaultAsync(d => d.Id == dossierSignatureId &&
                                          d.ProjetId == id &&
                                          d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                          !d.EstSupprime);
            if (dossier == null)
            {
                TempData["Error"] = "Le dossier de signature de la charte est introuvable.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            try
            {
                var result = await _electronicSignature.EnvoyerDossierAsync(dossierSignatureId, _currentUserService.Matricule);
                TempData[result.Success ? "Success" : "Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de l'envoi du dossier : {ex.Message}";
            }

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MettreAJourSignataireDossierSignatureCharte(Guid id, Guid dossierSignatureId, Guid signataireId, bool approuver)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var dossier = await _db.DossiersSignatureProjets
                .Include(d => d.Signataires)
                .FirstOrDefaultAsync(d => d.Id == dossierSignatureId &&
                                          d.ProjetId == id &&
                                          d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                          !d.EstSupprime);
            if (dossier == null)
            {
                TempData["Error"] = "Le dossier de signature de la charte est introuvable.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var signataire = dossier.Signataires.FirstOrDefault(s => s.Id == signataireId);
            if (signataire == null)
            {
                TempData["Error"] = "Le signataire sélectionné est introuvable.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            var canManageForProject = ui.CanManageDossierSignature;
            var isCurrentSignatory = signataire.UtilisateurId == userId;
            var isSponsorDecisionByDm = await CanValidateCharteAsDirecteurMetierAsync(projet, userId) &&
                                        projet.SponsorId == userId &&
                                        signataire.Role == RoleSignataireProjet.Sponsor;

            if (!canManageForProject && !isCurrentSignatory && !isSponsorDecisionByDm)
                return Forbid();

            try
            {
                var result = await _electronicSignature.EnregistrerDecisionSignataireAsync(
                    dossierSignatureId, signataireId, approuver, _currentUserService.Matricule);

                if (result.Success && projet.CharteProjet != null)
                {
                    if (signataire.Role == RoleSignataireProjet.Sponsor)
                    {
                        projet.CharteProjet.SignatureSponsor = approuver;
                        projet.CharteProjet.DateSignatureSponsor = approuver ? (signataire.DateSignature ?? DateTime.Now) : null;
                        projet.CharteProjet.SignatureSponsorId = approuver ? (signataire.UtilisateurId ?? projet.SponsorId) : null;
                        if (!approuver)
                        {
                            projet.CharteProjet.SignatureImageSponsor = null;
                            projet.CharteProjet.DateSignatureImageSponsor = null;
                        }
                    }

                    if (signataire.Role == RoleSignataireProjet.ChefDeProjet)
                    {
                        projet.CharteProjet.SignatureChefProjet = approuver;
                        projet.CharteProjet.DateSignatureChefProjet = approuver ? (signataire.DateSignature ?? DateTime.Now) : null;
                        projet.CharteProjet.SignatureChefProjetId = approuver ? (signataire.UtilisateurId ?? projet.ChefProjetId) : null;
                        if (!approuver)
                        {
                            projet.CharteProjet.SignatureImageCP = null;
                            projet.CharteProjet.DateSignatureImageCP = null;
                        }
                    }

                    ResetCharteValidationState(projet);
                    projet.DateModification = DateTime.Now;
                    projet.ModifiePar = _currentUserService.Matricule;
                    projet.CharteProjet.DateModification = DateTime.Now;
                    projet.CharteProjet.ModifiePar = _currentUserService.Matricule;
                    await _db.SaveChangesAsync();
                }

                TempData[result.Success ? "Success" : "Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la mise à jour de la signature : {ex.Message}";
            }

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> UploadCharteSignee(
            Guid id,
            IFormFile fichierSigne,
            string? version,
            string? commentaire,
            bool signatureSponsor = false,
            bool signatureChefProjet = false,
            Guid? dossierSignatureId = null)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            var userId = User.GetUserIdOrThrow();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "Le dépôt de la charte signée est réservé à la phase Analyse.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            if (fichierSigne == null || fichierSigne.Length == 0)
            {
                TempData["Error"] = "Veuillez sélectionner le document signé à déposer.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            if (!_fileStorage.IsValidFileExtension(fichierSigne.FileName, allowedExtensions))
            {
                TempData["Error"] = "Extension de fichier non autorisée pour la charte signée.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            DossierSignatureProjet? dossierSignature = null;
            if (dossierSignatureId.HasValue)
            {
                dossierSignature = await _db.DossiersSignatureProjets
                    .Include(d => d.Signataires)
                    .FirstOrDefaultAsync(d => d.Id == dossierSignatureId.Value &&
                                              d.ProjetId == id &&
                                              d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                              !d.EstSupprime);
                if (dossierSignature == null)
                {
                    TempData["Error"] = "Le dossier de signature sélectionné est introuvable.";
                    return RedirectToAction(nameof(CharteProjet), new { id });
                }

                if (!AreRequiredCharteSignaturesCompleted(dossierSignature))
                {
                    TempData["Error"] = "Le document signé ne peut être versé qu'après la signature du Sponsor et du Chef de Projet.";
                    return RedirectToAction(nameof(CharteProjet), new { id });
                }
            }

            if (projet.CharteProjet == null)
            {
                TempData["Error"] = "La charte doit être initialisée avant de déposer sa version signée.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var maxSize = 10 * 1024 * 1024;
            var path = await _fileStorage.SaveFileAsync(
                fichierSigne,
                $"projets/{projet.CodeProjet}/analyse/charte-signee",
                null,
                allowedExtensions,
                maxSize);

            var livrable = projet.Livrables
                .Where(l => !l.EstSupprime && l.TypeLivrable == TypeLivrable.CharteProjetSignee)
                .OrderByDescending(l => l.DateDepot)
                .FirstOrDefault();

            if (livrable == null)
            {
                livrable = new LivrableProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = id,
                    Phase = PhaseProjet.AnalyseClarification,
                    TypeLivrable = TypeLivrable.CharteProjetSignee,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };
                _db.LivrablesProjets.Add(livrable);
            }
            else
            {
                livrable.DateModification = DateTime.Now;
                livrable.ModifiePar = _currentUserService.Matricule;
            }

            livrable.NomDocument = fichierSigne.FileName;
            livrable.CheminRelatif = path;
            livrable.DateDepot = DateTime.Now;
            livrable.DeposeParId = userId;
            livrable.Commentaire = commentaire ?? string.Empty;
            livrable.Version = version ?? string.Empty;
            livrable.Phase = PhaseProjet.AnalyseClarification;
            livrable.TypeLivrable = TypeLivrable.CharteProjetSignee;

            projet.CharteProjet.SignatureSponsor = signatureSponsor;
            projet.CharteProjet.DateSignatureSponsor = signatureSponsor ? DateTime.Now : null;
            projet.CharteProjet.SignatureSponsorId = signatureSponsor ? projet.SponsorId : null;
            if (!signatureSponsor)
            {
                projet.CharteProjet.SignatureImageSponsor = null;
                projet.CharteProjet.DateSignatureImageSponsor = null;
            }

            projet.CharteProjet.SignatureChefProjet = signatureChefProjet;
            projet.CharteProjet.DateSignatureChefProjet = signatureChefProjet ? DateTime.Now : null;
            projet.CharteProjet.SignatureChefProjetId = signatureChefProjet ? projet.ChefProjetId : null;
            if (!signatureChefProjet)
            {
                projet.CharteProjet.SignatureImageCP = null;
                projet.CharteProjet.DateSignatureImageCP = null;
            }

            ResetCharteValidationState(projet);
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            projet.CharteProjet.DateModification = DateTime.Now;
            projet.CharteProjet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            if (dossierSignature != null)
            {
                var finalisation = await _electronicSignature.FinaliserDossierAsync(
                    dossierSignature.Id,
                    fichierSigne.FileName,
                    path,
                    _currentUserService.Matricule);

                if (!finalisation.Success)
                {
                    TempData["Error"] = $"Charte déposée, mais le dossier de signature n'a pas pu être finalisé : {finalisation.Message}";
                    return RedirectToAction(nameof(CharteProjet), new { id });
                }
            }

            await _auditService.LogActionAsync("UPLOAD_CHARTE_SIGNEE", "LivrableProjet", livrable.Id,
                null,
                new
                {
                    ProjetId = projet.Id,
                    livrable.NomDocument,
                    livrable.Version,
                    SignatureSponsor = projet.CharteProjet.SignatureSponsor,
                    SignatureChefProjet = projet.CharteProjet.SignatureChefProjet
                });

            TempData["Success"] = "Version signée de la charte déposée. Les validations DM/DSI ont été réinitialisées.";
            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MettreAJourSignaturesCharte(
            Guid id,
            bool signatureSponsor = false,
            bool signatureChefProjet = false)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La mise à jour des signatures est réservée à la phase Analyse.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            if (projet.CharteProjet == null)
            {
                TempData["Error"] = "La charte doit être initialisée avant de gérer les signatures.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var hasChanged = false;

            if (projet.CharteProjet.SignatureSponsor != signatureSponsor)
            {
                projet.CharteProjet.SignatureSponsor = signatureSponsor;
                projet.CharteProjet.DateSignatureSponsor = signatureSponsor ? DateTime.Now : null;
                projet.CharteProjet.SignatureSponsorId = signatureSponsor ? projet.SponsorId : null;
                if (!signatureSponsor)
                {
                    projet.CharteProjet.SignatureImageSponsor = null;
                    projet.CharteProjet.DateSignatureImageSponsor = null;
                }

                hasChanged = true;
            }

            if (projet.CharteProjet.SignatureChefProjet != signatureChefProjet)
            {
                projet.CharteProjet.SignatureChefProjet = signatureChefProjet;
                projet.CharteProjet.DateSignatureChefProjet = signatureChefProjet ? DateTime.Now : null;
                projet.CharteProjet.SignatureChefProjetId = signatureChefProjet ? projet.ChefProjetId : null;
                if (!signatureChefProjet)
                {
                    projet.CharteProjet.SignatureImageCP = null;
                    projet.CharteProjet.DateSignatureImageCP = null;
                }

                hasChanged = true;
            }

            if (!hasChanged)
            {
                TempData["Info"] = "Aucun changement détecté sur les signatures de la charte.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            ResetCharteValidationState(projet);
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            projet.CharteProjet.DateModification = DateTime.Now;
            projet.CharteProjet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MISE_A_JOUR_SIGNATURES_CHARTE", "CharteProjet", projet.CharteProjet.Id,
                null,
                new
                {
                    ProjetId = projet.Id,
                    SignatureSponsor = projet.CharteProjet.SignatureSponsor,
                    SignatureChefProjet = projet.CharteProjet.SignatureChefProjet
                });

            TempData["Success"] = "Signatures de la charte mises à jour. Les validations DM/DSI ont été réinitialisées.";
            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        // GET: Afficher/Éditer la fiche projet CIT
        [Authorize]
        public async Task<IActionResult> FicheProjet(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var fiche = await _db.FicheProjets
                .Include(f => f.DerniereMiseAJourPar)
                .FirstOrDefaultAsync(f => f.ProjetId == id && !f.EstSupprime);

            // Si la fiche n'existe pas, créer une structure par défaut
            if (fiche == null)
            {
                fiche = new FicheProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    TitreCourt = projet.Titre,
                    TitreLong = projet.Titre,
                    ObjectifPrincipal = projet.Objectif ?? projet.DemandeProjet?.Objectifs ?? string.Empty,
                    ContexteProblemeAdresse = projet.DemandeProjet?.Contexte ?? string.Empty,
                    DescriptionSynthetique = projet.DemandeProjet?.Description ?? string.Empty,
                    ResultatsAttendus = projet.DemandeProjet?.AvantagesAttendus ?? string.Empty,
                    CriticiteUrgence = $"{projet.DemandeProjet?.Criticite} / {projet.DemandeProjet?.Urgence}",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };

                // Vérifier les livrables obligatoires
                fiche.CharteProjetPresente = projet.Livrables?.Any(l => l.TypeLivrable == TypeLivrable.CharteProjet) ?? false;
                fiche.WBSPlanningRACIBudgetPresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.PlanificationValidation && 
                    (l.TypeLivrable == TypeLivrable.Wbs || l.TypeLivrable == TypeLivrable.PlanningDetaille || l.TypeLivrable == TypeLivrable.MatriceRaci || l.TypeLivrable == TypeLivrable.BudgetPrevisionnel)) ?? false;
                fiche.CRReunionsPresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.ExecutionSuivi && l.TypeLivrable == TypeLivrable.CompteRenduReunion) ?? false;
                fiche.CahierTestsPVRecettePVMEPPresent = projet.Livrables?.Any(l => (l.Phase == PhaseProjet.UatMep || l.Phase == PhaseProjet.ClotureLeconsApprises) && 
                    (l.TypeLivrable == TypeLivrable.CahierTests || l.TypeLivrable == TypeLivrable.PvRecette || l.TypeLivrable == TypeLivrable.PvMep)) ?? false;
                fiche.RapportLeconsApprisesPVCloturePresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.ClotureLeconsApprises && 
                    (l.TypeLivrable == TypeLivrable.RapportCloture || l.TypeLivrable == TypeLivrable.PvCloture)) ?? false;

                // Construire la liste de l'équipe projet
                var equipeProjet = new List<string>();
                if (projet.ChefProjet != null)
                    equipeProjet.Add($"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms} - Chef de Projet");
                foreach (var membre in projet.Membres?.Where(m => !m.EstSupprime) ?? Enumerable.Empty<MembreProjet>())
                {
                    equipeProjet.Add($"{membre.Nom} {membre.Prenom} - {membre.RoleDansProjet}");
                }
                fiche.EquipeProjet = string.Join("\n", equipeProjet);

                _db.FicheProjets.Add(fiche);
                await _db.SaveChangesAsync();
            }
            else
            {
                // Mettre à jour les livrables obligatoires
                fiche.CharteProjetPresente = projet.Livrables?.Any(l => l.TypeLivrable == TypeLivrable.CharteProjet) ?? false;
                fiche.WBSPlanningRACIBudgetPresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.PlanificationValidation && 
                    (l.TypeLivrable == TypeLivrable.Wbs || l.TypeLivrable == TypeLivrable.PlanningDetaille || l.TypeLivrable == TypeLivrable.MatriceRaci || l.TypeLivrable == TypeLivrable.BudgetPrevisionnel)) ?? false;
                fiche.CRReunionsPresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.ExecutionSuivi && l.TypeLivrable == TypeLivrable.CompteRenduReunion) ?? false;
                fiche.CahierTestsPVRecettePVMEPPresent = projet.Livrables?.Any(l => (l.Phase == PhaseProjet.UatMep || l.Phase == PhaseProjet.ClotureLeconsApprises) && 
                    (l.TypeLivrable == TypeLivrable.CahierTests || l.TypeLivrable == TypeLivrable.PvRecette || l.TypeLivrable == TypeLivrable.PvMep)) ?? false;
                fiche.RapportLeconsApprisesPVCloturePresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.ClotureLeconsApprises && 
                    (l.TypeLivrable == TypeLivrable.RapportCloture || l.TypeLivrable == TypeLivrable.PvCloture)) ?? false;
                await _db.SaveChangesAsync();
            }

            ViewBag.Projet = projet;
            return View(fiche);
        }

        // POST: Sauvegarder la fiche projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SauvegarderFicheProjet(Guid id, FicheProjet fiche)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanEditFicheProjetAsync(projet))
            {
                return Forbid();
            }

            var userId = User.GetUserIdOrThrow();

            var ficheExistante = await _db.FicheProjets
                .FirstOrDefaultAsync(f => f.ProjetId == id && !f.EstSupprime);

            if (ficheExistante == null)
            {
                fiche.Id = Guid.NewGuid();
                fiche.ProjetId = id;
                fiche.DateCreation = DateTime.Now;
                fiche.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                fiche.EstSupprime = false;
                fiche.DateDerniereMiseAJour = DateTime.Now;
                fiche.DerniereMiseAJourParId = userId;
                _db.FicheProjets.Add(fiche);
            }
            else
            {
                // Mettre à jour les champs
                ficheExistante.TitreCourt = fiche.TitreCourt;
                ficheExistante.TitreLong = fiche.TitreLong;
                ficheExistante.ObjectifPrincipal = fiche.ObjectifPrincipal;
                ficheExistante.ContexteProblemeAdresse = fiche.ContexteProblemeAdresse;
                ficheExistante.DescriptionSynthetique = fiche.DescriptionSynthetique;
                ficheExistante.ResultatsAttendus = fiche.ResultatsAttendus;
                ficheExistante.PerimetreInclus = fiche.PerimetreInclus;
                ficheExistante.PerimetreExclu = fiche.PerimetreExclu;
                ficheExistante.BeneficesAttendus = fiche.BeneficesAttendus;
                ficheExistante.CriticiteUrgence = fiche.CriticiteUrgence;
                ficheExistante.TypeProjet = fiche.TypeProjet;
                ficheExistante.ProchainJalon = fiche.ProchainJalon;
                ficheExistante.JalonsPrincipaux = fiche.JalonsPrincipaux;
                ficheExistante.DecoupageLotsTravail = fiche.DecoupageLotsTravail;
                ficheExistante.PlanificationRessources = fiche.PlanificationRessources;
                ficheExistante.RaciParActivite = fiche.RaciParActivite;
                ficheExistante.FrequenceReunions = fiche.FrequenceReunions;
                ficheExistante.ParticipantsReunions = fiche.ParticipantsReunions;
                ficheExistante.CanalCommunication = fiche.CanalCommunication;
                ficheExistante.CopilPrevu = fiche.CopilPrevu;
                ficheExistante.CommentaireBudgetPlanification = fiche.CommentaireBudgetPlanification;
                ficheExistante.CommentaireValidationPlanification = fiche.CommentaireValidationPlanification;
                ficheExistante.SyntheseRisques = fiche.SyntheseRisques;
                ficheExistante.EquipeProjet = fiche.EquipeProjet;
                ficheExistante.PartiesPrenantesCles = fiche.PartiesPrenantesCles;
                ficheExistante.BudgetPrevisionnel = fiche.BudgetPrevisionnel;
                ficheExistante.BudgetConsomme = fiche.BudgetConsomme;
                ficheExistante.EcartsBudget = ficheExistante.BudgetPrevisionnel.HasValue && ficheExistante.BudgetConsomme.HasValue 
                    ? ficheExistante.BudgetConsomme.Value - ficheExistante.BudgetPrevisionnel.Value 
                    : null;
                
                // Validation : justification obligatoire si écart > 10%
                if (ficheExistante.BudgetPrevisionnel.HasValue && ficheExistante.BudgetConsomme.HasValue && ficheExistante.BudgetPrevisionnel.Value > 0)
                {
                    var ecartPourcentage = Math.Abs((ficheExistante.BudgetConsomme.Value - ficheExistante.BudgetPrevisionnel.Value) / ficheExistante.BudgetPrevisionnel.Value * 100);
                    if (ecartPourcentage > 10 && string.IsNullOrWhiteSpace(fiche.JustificationEcartBudget))
                    {
                        TempData["Error"] = $"Un écart de {ecartPourcentage:F1}% a été détecté. Une justification est obligatoire pour les écarts supérieurs à 10%.";
                        return RedirectToAction(nameof(FicheProjet), new { id });
                    }
                    
                    if (ecartPourcentage > 10 && !string.IsNullOrWhiteSpace(fiche.JustificationEcartBudget))
                    {
                        ficheExistante.JustificationEcartBudget = fiche.JustificationEcartBudget;
                        ficheExistante.DateJustificationEcart = DateTime.Now;
                        ficheExistante.JustificationParId = userId;
                    }
                }
                ficheExistante.DateDebutReelleExecution = fiche.DateDebutReelleExecution;
                ficheExistante.DateFinEstimeeExecution = fiche.DateFinEstimeeExecution;
                ficheExistante.JustificationRetardExecution = fiche.JustificationRetardExecution;
                ficheExistante.CommentaireAvancementExecution = fiche.CommentaireAvancementExecution;
                ficheExistante.ActionsRealiseesExecution = fiche.ActionsRealiseesExecution;
                ficheExistante.ActionsAVenirExecution = fiche.ActionsAVenirExecution;
                ficheExistante.ProblemesBlocagesExecution = fiche.ProblemesBlocagesExecution;
                ficheExistante.JustificationBudgetExecution = fiche.JustificationBudgetExecution;
                ficheExistante.SyntheseChargesExecution = fiche.SyntheseChargesExecution;
                ficheExistante.DecisionsExecution = fiche.DecisionsExecution;
                ficheExistante.DateDebutRecette = fiche.DateDebutRecette;
                ficheExistante.DateFinRecette = fiche.DateFinRecette;
                ficheExistante.UtilisateursTesteurs = fiche.UtilisateursTesteurs;
                ficheExistante.PerimetreTeste = fiche.PerimetreTeste;
                ficheExistante.DateMepPrevue = fiche.DateMepPrevue;
                ficheExistante.PrerequisMep = fiche.PrerequisMep;
                ficheExistante.PlanMep = fiche.PlanMep;
                ficheExistante.PlanRollback = fiche.PlanRollback;
                ficheExistante.ChangeRequis = fiche.ChangeRequis;
                ficheExistante.ReferenceChange = fiche.ReferenceChange;
                ficheExistante.StatutValidationChange = fiche.StatutValidationChange;
                ficheExistante.ResultatMep = fiche.ResultatMep;
                ficheExistante.IncidentsMep = fiche.IncidentsMep;
                ficheExistante.PeriodeHypercare = fiche.PeriodeHypercare;
                ficheExistante.IncidentsPostMep = fiche.IncidentsPostMep;
                ficheExistante.StatutHypercare = fiche.StatutHypercare;
                ficheExistante.HypercareTermine = fiche.HypercareTermine;
                ficheExistante.TransfertRunDocumentation = fiche.TransfertRunDocumentation;
                ficheExistante.TransfertRunAcces = fiche.TransfertRunAcces;
                ficheExistante.TransfertRunSupportInforme = fiche.TransfertRunSupportInforme;
                ficheExistante.TransfertRunExploitationPrete = fiche.TransfertRunExploitationPrete;
                ficheExistante.StatutFinalCloture = fiche.StatutFinalCloture;
                ficheExistante.CommentaireStatutFinal = fiche.CommentaireStatutFinal;
                ficheExistante.PointsForts = fiche.PointsForts;
                ficheExistante.PointsVigilance = fiche.PointsVigilance;
                ficheExistante.DecisionsAttendues = fiche.DecisionsAttendues;
                ficheExistante.DemandesArbitrage = fiche.DemandesArbitrage;
                ficheExistante.DateDerniereMiseAJour = DateTime.Now;
                ficheExistante.DerniereMiseAJourParId = userId;
                ficheExistante.DateModification = DateTime.Now;
                ficheExistante.ModifiePar = _currentUserService.Matricule;
            }

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SAUVEGARDE_FICHE_PROJET", "FicheProjet", ficheExistante?.Id ?? fiche.Id);

            TempData["Success"] = "Fiche projet sauvegardée avec succès.";
            return RedirectToAction(nameof(FicheProjet), new { id });
        }

        // POST: Valider Planification par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
        public async Task<IActionResult> ValiderPlanifDM(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderPlanificationDM") ||
                !await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.PlanificationValidation)
            {
                TempData["Error"] = "La planification ne peut être validée qu'en phase Planification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;
            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PLANIF_DM", "Projet", projet.Id);

            TempData["Success"] = "Planification validée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // POST: Valider Planification par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        public async Task<IActionResult> ValiderPlanifDSI(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            if (!await CanViewProjectAsync(projet))
            {
                return Forbid();
            }

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            if (projet.PhaseActuelle != PhaseProjet.PlanificationValidation)
            {
                TempData["Error"] = "La planification ne peut être validée qu'en phase Planification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!projet.PlanningValideParDM)
            {
                TempData["Error"] = "La planification doit d'abord être validée par le Directeur Métier.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // ⛔ VALIDATION DES LIVRABLES OBLIGATOIRES (PRD - Blocage automatique)
            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.ExecutionSuivi);
            
            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
                return RedirectToAction(nameof(Details), new { id, tab = "planification" });
            }

            projet.PlanningValideParDSI = true;
            projet.DatePlanningValideParDSI = DateTime.Now;
            
            // Passer à la phase Exécution
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            projet.StatutProjet = StatutProjet.EnCours;
            if (!projet.DateDebut.HasValue)
                projet.DateDebut = DateTime.Now;

            // Enregistrer dans l'historique
            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.ExecutionSuivi,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Passage en phase Exécution après validation planification DSI",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PLANIF_DSI", "Projet", projet.Id);
            await _auditService.LogActionAsync("VALIDATION_PHASE_PLANIFICATION", "Projet", projet.Id,
                new { AnciennePhase = PhaseProjet.PlanificationValidation },
                new { NouvellePhase = PhaseProjet.ExecutionSuivi });

            TempData["Success"] = "Planification validée par la DSI. Le projet passe en phase Exécution.";
            return RedirectToAction(nameof(Details), new { id, tab = "execution" });
        }

        // POST: Prêt pour UAT
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> PretUAT(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.ExecutionSuivi)
            {
                TempData["Error"] = "Le projet doit être en phase Exécution.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.UatMep);
            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
                return RedirectToAction(nameof(Details), new { id, tab = "execution" });
            }

            projet.PhaseActuelle = PhaseProjet.UatMep;
            
            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.UatMep,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Projet prêt pour UAT",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("PASSAGE_UAT", "Projet", projet.Id);

            // Notifier les Responsables Solutions IT de l'entrée en phase UAT
            await _notificationService.NotifierResponsablesSolutionsITAsync(
                TypeNotification.ProjetEntreEnUAT,
                $"Projet {projet.CodeProjet} - Entrée en phase UAT",
                $"Le projet '{projet.Titre}' est entré en phase UAT & MEP. Veuillez suivre les tests et la mise en production.",
                "Projet",
                projet.Id,
                new { CodeProjet = projet.CodeProjet, Titre = projet.Titre, Phase = "UatMep" });

            TempData["Success"] = "Projet passé en phase UAT & MEP.";
            return RedirectToAction(nameof(Details), new { id, tab = "uat" });
        }

        // POST: Valider Recette
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
        public async Task<IActionResult> ValiderRecette(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderRecette") ||
                !await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.UatMep)
            {
                TempData["Error"] = "Le projet doit être en phase UAT & MEP.";
                return RedirectToAction(nameof(Details), new { id });
            }

            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;
            projet.RecetteValideeParId = userId;
            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_RECETTE", "Projet", projet.Id);

            // Notifier les Responsables Solutions IT que la recette est validée (MEP possible)
            await _notificationService.NotifierResponsablesSolutionsITAsync(
                TypeNotification.ProjetEntreEnMEP,
                $"Projet {projet.CodeProjet} - Recette validée, prêt pour MEP",
                $"La recette du projet '{projet.Titre}' a été validée. Le projet est prêt pour la mise en production (MEP).",
                "Projet",
                projet.Id,
                new { CodeProjet = projet.CodeProjet, Titre = projet.Titre, RecetteValidee = true });

            TempData["Success"] = "Recette validée.";
            return RedirectToAction(nameof(Details), new { id, tab = "uat" });
        }

        // POST: Fin UAT - Prêt pour Clôture
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> FinUAT(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageUatAsync(projet))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.UatMep)
            {
                TempData["Error"] = "Le projet doit être en phase UAT & MEP.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!projet.RecetteValidee)
            {
                TempData["Error"] = "La recette doit être validée avant de passer à la clôture.";
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            if (!projet.MepEffectuee)
            {
                TempData["Error"] = "La MEP doit être enregistrée avant de passer à la clôture.";
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            // ⛔ VALIDATION DES LIVRABLES OBLIGATOIRES (PRD - Blocage automatique)
            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.ClotureLeconsApprises);
            
            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            if (projet.FicheProjet == null ||
                !projet.FicheProjet.HypercareTermine ||
                string.IsNullOrWhiteSpace(projet.FicheProjet.PeriodeHypercare) ||
                string.IsNullOrWhiteSpace(projet.FicheProjet.StatutHypercare))
            {
                TempData["Error"] = "Le passage en clôture est bloqué tant que l'hypercare n'est pas renseigné et terminé.";
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            var anciennePhase = projet.PhaseActuelle;
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;
            projet.StatutProjet = StatutProjet.ClotureEnCours;

            // Ajouter une entrée à l'historique des phases
            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.ClotureLeconsApprises,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Fin UAT - Passage en phase Clôture",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("FIN_UAT", "Projet", projet.Id,
                new { AnciennePhase = anciennePhase },
                new { NouvellePhase = PhaseProjet.ClotureLeconsApprises });

            TempData["Success"] = "Projet passé en phase Clôture & Leçons apprises.";
            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }

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
                DemandeParId = userId, // Chef de Projet qui initie
                DateDemande = DateTime.Now,
                DateSouhaiteeCloture = dateSouhaiteeCloture,
                StatutValidationDemandeur = StatutValidationCloture.EnAttente,
                StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente, // DM notifié mais peut valider en lieu et place du demandeur
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

        // GET: Upload Livrable (formulaire)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> UploadLivrableForm(Guid projetId, PhaseProjet phase)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, phase))
                return Forbid();

            ViewBag.ProjetId = projetId;
            ViewBag.Phase = phase;
            ViewBag.TypesLivrables = Enum.GetValues<TypeLivrable>();
            return PartialView("_UploadLivrableModal");
        }

        // POST: Upload Livrable
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> UploadLivrable(Guid projetId, PhaseProjet phase, TypeLivrable typeLivrable, IFormFile fichier, string? commentaire, string? version)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, phase))
                return Forbid();

            var userId = User.GetUserIdOrThrow();

            if (fichier == null || fichier.Length == 0)
            {
                TempData["Error"] = "Veuillez sélectionner un fichier.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".pptx", ".zip" };
            if (!_fileStorage.IsValidFileExtension(fichier.FileName, allowedExtensions))
            {
                TempData["Error"] = "Extension de fichier non autorisée.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
            }

            var subfolder = GetSubfolderForPhase(phase);
            var maxSize = 10 * 1024 * 1024; // 10 MB
            var path = await _fileStorage.SaveFileAsync(
                fichier, 
                $"projets/{projet.CodeProjet}/{subfolder}", 
                null, 
                allowedExtensions, 
                maxSize);

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Phase = phase,
                TypeLivrable = typeLivrable,
                NomDocument = fichier.FileName,
                CheminRelatif = path,
                DateDepot = DateTime.Now,
                DeposeParId = userId,
                Commentaire = commentaire ?? string.Empty,
                Version = version ?? string.Empty,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };

            _db.LivrablesProjets.Add(livrable);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPLOAD_LIVRABLE", "LivrableProjet", livrable.Id);

            TempData["Success"] = "Livrable déposé avec succès.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = GetTabForPhase(phase) });
        }

        // POST: Ajouter Risque
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterRisque(
            Guid projetId,
            string description,
            ProbabiliteRisque probabilite,
            ImpactRisque impact,
            string? planMitigation,
            string? responsable)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageAnalyseAsync(projet))
                return Forbid();

            if (!string.IsNullOrWhiteSpace(description))
            {
                var risque = new RisqueProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    Description = description.Trim(),
                    Probabilite = probabilite,
                    Impact = impact,
                    PlanMitigation = string.IsNullOrWhiteSpace(planMitigation) ? string.Empty : planMitigation.Trim(),
                    Responsable = string.IsNullOrWhiteSpace(responsable) ? string.Empty : responsable.Trim(),
                    EstSupprime = false,
                    DateCreationRisque = DateTime.Now,
                    Statut = StatutRisque.Identifie,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? User.Identity?.Name ?? "SYSTEM"
                };

                // Réinitialiser les champs système pour éviter le mass assignment
                _db.RisquesProjets.Add(risque);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("AJOUT_RISQUE", "RisqueProjet", risque.Id);

                TempData["Success"] = "Risque ajouté.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "synthese" });
            }

            TempData["Error"] = "La description du risque est obligatoire.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "synthese" });
        }

        // POST: Mettre à jour un risque
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateRisque(Guid id, Guid risqueId, string? description, ProbabiliteRisque? probabilite, ImpactRisque? impact, StatutRisque? statut, string? planMitigation, string? responsable, DateTime? echeance)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanManageAnalyseAsync(projet))
                return Forbid();

            var risque = await _db.RisquesProjets.FindAsync(risqueId);
            if (risque == null || risque.ProjetId != id)
                return NotFound();

            // Mettre à jour les champs fournis
            if (!string.IsNullOrWhiteSpace(description))
                risque.Description = description;

            if (probabilite.HasValue)
                risque.Probabilite = probabilite.Value;

            if (impact.HasValue)
                risque.Impact = impact.Value;

            if (statut.HasValue)
            {
                risque.Statut = statut.Value;
                if (statut.Value == StatutRisque.Clos && !risque.DateCloture.HasValue)
                {
                    risque.DateCloture = DateTime.Now;
                }
            }

            if (!string.IsNullOrWhiteSpace(planMitigation))
                risque.PlanMitigation = string.IsNullOrWhiteSpace(planMitigation) ? string.Empty : planMitigation.Trim();

            if (!string.IsNullOrWhiteSpace(responsable))
                risque.Responsable = string.IsNullOrWhiteSpace(responsable) ? string.Empty : responsable.Trim();

            risque.DateModification = DateTime.Now;
            risque.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MISE_A_JOUR_RISQUE", "RisqueProjet", risque.Id,
                new { ProjetId = id },
                new { Statut = risque.Statut, Probabilite = risque.Probabilite, Impact = risque.Impact });

            TempData["Success"] = "Risque mis à jour avec succès.";
            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // POST: Ajouter Anomalie
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterAnomalie(Guid projetId, AnomalieProjet anomalie)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            if (ModelState.IsValid)
            {
                // Réinitialiser les champs système pour éviter le mass assignment
                anomalie.Id = Guid.NewGuid();
                anomalie.ProjetId = projetId;
                anomalie.EstSupprime = false;
                anomalie.Reference = $"ANOM-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
                anomalie.DateCreationAnomalie = DateTime.Now;
                anomalie.Statut = StatutAnomalie.Ouverte;
                anomalie.RapporteePar = User.FindFirstValue("Nom") + " " + User.FindFirstValue("Prenoms");
                anomalie.DateCreation = DateTime.Now;
                anomalie.CreePar = _currentUserService.Matricule ?? User.Identity?.Name ?? "SYSTEM";

                _db.AnomaliesProjets.Add(anomalie);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("AJOUT_ANOMALIE", "AnomalieProjet", anomalie.Id);

                // Notifier les Responsables Solutions IT si l'anomalie est critique ou haute priorité (demande de support technique)
                if (anomalie.Priorite == PrioriteAnomalie.Critique || anomalie.Priorite == PrioriteAnomalie.Haute)
                {
                    await _notificationService.NotifierResponsablesSolutionsITAsync(
                        TypeNotification.DemandeSupportTechnique,
                        $"Projet {projet.CodeProjet} - Anomalie {anomalie.Priorite}",
                        $"Une anomalie {anomalie.Priorite} a été signalée sur le projet '{projet.Titre}': {anomalie.Description}. Référence: {anomalie.Reference}",
                        "AnomalieProjet",
                        anomalie.Id,
                        new { 
                            CodeProjet = projet.CodeProjet, 
                            Titre = projet.Titre, 
                            Reference = anomalie.Reference,
                            Priorite = anomalie.Priorite.ToString(),
                            Environnement = anomalie.Environnement.ToString()
                        });
                }

                TempData["Success"] = "Anomalie ajoutée.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "execution" });
            }

            TempData["Error"] = "Erreur lors de l'ajout de l'anomalie.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "execution" });
        }

        // POST: Mettre à jour pourcentage avancement
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateAvancement(Guid id, int? pourcentageAvancement = null, EtatProjet? etatProjet = null)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .Include(p => p.CharteProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.DemandesCloture)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            if (!await CanUpdateProjectProgressAsync(projet))
                return Forbid();

            var ancienPourcentage = projet.PourcentageAvancement;

            if (etatProjet.HasValue)
            {
                projet.EtatProjet = etatProjet.Value;
            }

            await RecalculateProjectProgressAsync(projet);

            // Calcul automatique du RAG
            projet.IndicateurRAG = await _ragCalculationService.CalculerRAGAsync(projet);
            projet.DateDernierCalculRAG = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_AVANCEMENT", "Projet", projet.Id,
                new { PourcentageAvancement = ancienPourcentage },
                new { PourcentageAvancement = projet.PourcentageAvancement, IndicateurRAG = projet.IndicateurRAG, EtatProjet = projet.EtatProjet });

            TempData["Success"] = "Statut mis à jour. L'avancement est recalculé automatiquement.";
            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // GET: Gestion des charges du projet
        [Authorize]
        public async Task<IActionResult> Charges(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.Membres)
                .Include(p => p.Charges)
                    .ThenInclude(c => c.Ressource)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == userId);
            var ui = await BuildProjectUiAsync(projet);
            var isPilotage = ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
            var isProjectMember = user != null &&
                                  projet.Membres.Any(m => !m.EstSupprime &&
                                                          !string.IsNullOrWhiteSpace(m.Email) &&
                                                          m.Email == user.Email);

            if (!isPilotage && !isProjectMember)
                return Forbid();

            var ressources = new List<Utilisateur>();

            if (projet.ChefProjetId.HasValue)
            {
                var chefProjet = await _db.Utilisateurs.FindAsync(projet.ChefProjetId.Value);
                if (chefProjet != null && !chefProjet.EstSupprime)
                    ressources.Add(chefProjet);
            }

            var emailsMembres = projet.Membres
                .Where(m => !m.EstSupprime && !string.IsNullOrWhiteSpace(m.Email))
                .Select(m => m.Email)
                .Distinct()
                .ToList();

            if (emailsMembres.Any())
            {
                var utilisateursParEmail = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime && emailsMembres.Contains(u.Email))
                    .ToListAsync();

                foreach (var membre in utilisateursParEmail)
                {
                    if (!ressources.Any(r => r.Id == membre.Id))
                        ressources.Add(membre);
                }
            }

            foreach (var chargeRessource in projet.Charges
                         .Where(c => !c.EstSupprime && c.Ressource != null)
                         .Select(c => c.Ressource!)
                         .DistinctBy(r => r.Id))
            {
                if (!ressources.Any(r => r.Id == chargeRessource.Id))
                    ressources.Add(chargeRessource);
            }

            ressources = ressources
                .OrderBy(r => r.Nom)
                .ThenBy(r => r.Prenoms)
                .ToList();

            var currentWeek = NormalizeToMonday(DateTime.Now);
            var semaines = Enumerable.Range(-2, 6)
                .Select(offset => currentWeek.AddDays(offset * 7))
                .ToList();

            var activeCharges = projet.Charges
                .Where(c => !c.EstSupprime)
                .ToList();

            var weekModels = semaines
                .Select(week => new ProjetChargesWeekViewModel
                {
                    StartDate = week,
                    Label = $"S{System.Globalization.ISOWeek.GetWeekOfYear(week)}",
                    Subtitle = week.ToString("dd MMM", new System.Globalization.CultureInfo("fr-FR")),
                    IsCurrent = week == currentWeek,
                    IsPast = week < currentWeek,
                    IsFuture = week > currentWeek
                })
                .ToList();

            var canEditForecast = isPilotage;
            var canEditActual = isPilotage || isProjectMember;
            var canValidateCharges = ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
            var canExport = isPilotage;

            var resourceRows = new List<ProjetChargesResourceRowViewModel>();
            foreach (var ressource in ressources)
            {
                var isCurrentResource = user != null && user.Id == ressource.Id;
                var row = new ProjetChargesResourceRowViewModel
                {
                    ResourceId = ressource.Id,
                    FullName = $"{ressource.Nom} {ressource.Prenoms}".Trim(),
                    Email = ressource.Email,
                    RoleLabel = GetProfilRessourceLabel(ressource.ProfilRessource),
                    WeeklyCapacity = ressource.CapaciteHebdomadaire,
                    CapacityTotal = ressource.CapaciteHebdomadaire * weekModels.Count
                };

                foreach (var week in weekModels)
                {
                    var charge = activeCharges.FirstOrDefault(c =>
                        c.RessourceId == ressource.Id &&
                        NormalizeToMonday(c.SemaineDebut) == week.StartDate);

                    var planned = charge?.ChargePrevisionnelle ?? 0m;
                    var actual = charge?.ChargeReelle;
                    var loadReference = actual ?? planned;
                    var utilization = ressource.CapaciteHebdomadaire > 0
                        ? Math.Round((double)(loadReference / ressource.CapaciteHebdomadaire) * 100, 1)
                        : 0;
                    var allocation = ressource.CapaciteHebdomadaire > 0
                        ? Math.Round(planned / ressource.CapaciteHebdomadaire * 100, 1)
                        : 0;

                    var (status, statusClass) = GetCapacityStatus(utilization);

                    row.Cells.Add(new ProjetChargeCellViewModel
                    {
                        ResourceId = ressource.Id,
                        WeekStart = week.StartDate,
                        PlannedHours = planned,
                        ActualHours = actual,
                        VarianceHours = actual.HasValue ? actual.Value - planned : 0,
                        AllocationPercentage = allocation,
                        Comment = charge?.Commentaire ?? string.Empty,
                        TypeActivite = charge?.TypeActivite ?? string.Empty,
                        Activite = charge?.Activite ?? string.Empty,
                        ValidationComment = charge?.CommentaireValidation ?? string.Empty,
                        ValidationStatus = charge?.StatutValidation ?? StatutValidationCharge.Brouillon,
                        ValidationStatusLabel = GetValidationChargeLabel(charge?.StatutValidation ?? StatutValidationCharge.Brouillon),
                        ValidationStatusClass = GetValidationChargeClass(charge?.StatutValidation ?? StatutValidationCharge.Brouillon),
                        UtilizationRate = utilization,
                        CapacityStatus = status,
                        CapacityStatusClass = statusClass,
                        IsMissingActual = !week.IsFuture && planned > 0 && !actual.HasValue,
                        CanEditForecast = canEditForecast,
                        CanEditActual = isPilotage || isCurrentResource,
                        CanSubmit = isPilotage || isCurrentResource,
                        CanReview = canValidateCharges
                    });
                }

                row.PlannedTotal = row.Cells.Sum(c => c.PlannedHours);
                row.ActualTotal = row.Cells.Where(c => c.ActualHours.HasValue).Sum(c => c.ActualHours ?? 0);
                row.VarianceTotal = row.ActualTotal - row.PlannedTotal;
                row.RemainingCapacity = row.CapacityTotal - row.ActualTotal;
                row.UtilizationRate = row.CapacityTotal > 0
                    ? Math.Round((double)(row.ActualTotal / row.CapacityTotal) * 100, 1)
                    : 0;

                var (rowStatus, rowStatusClass) = GetCapacityStatus(row.UtilizationRate);
                row.CapacityStatus = rowStatus;
                row.CapacityStatusClass = rowStatusClass;

                resourceRows.Add(row);
            }

            var weeklySummaries = new List<ProjetChargesWeeklySummaryViewModel>();
            foreach (var week in weekModels)
            {
                var cells = resourceRows.SelectMany(r => r.Cells).Where(c => c.WeekStart == week.StartDate).ToList();
                var planned = cells.Sum(c => c.PlannedHours);
                var actual = cells.Sum(c => c.ActualHours ?? 0);
                var capacity = ressources.Sum(r => r.CapaciteHebdomadaire);
                var missing = cells.Count(c => c.IsMissingActual);
                var pending = cells.Count(c => c.ValidationStatus == StatutValidationCharge.EnAttente);

                string status;
                string statusClass;
                if (week.IsFuture)
                {
                    status = "Prévision";
                    statusClass = "badge-modern-info";
                }
                else if (pending > 0)
                {
                    status = "À valider";
                    statusClass = "badge-modern-warning";
                }
                else if (missing > 0)
                {
                    status = "Saisie incomplète";
                    statusClass = "badge-modern-warning";
                }
                else if (actual > planned && planned > 0)
                {
                    status = "Dérive";
                    statusClass = "badge-modern-danger";
                }
                else
                {
                    status = "Saisie complète";
                    statusClass = "badge-modern-success";
                }

                weeklySummaries.Add(new ProjetChargesWeeklySummaryViewModel
                {
                    WeekStart = week.StartDate,
                    Label = $"{week.Label} · {week.Subtitle}",
                    PlannedTotal = planned,
                    ActualTotal = actual,
                    CapacityTotal = capacity,
                    MissingEntries = missing,
                    PendingValidations = pending,
                    Status = status,
                    StatusClass = statusClass
                });
            }

            var totalPlanned = resourceRows.Sum(r => r.PlannedTotal);
            var totalActual = resourceRows.Sum(r => r.ActualTotal);
            var totalCapacity = resourceRows.Sum(r => r.CapacityTotal);
            var totalVariance = totalActual - totalPlanned;

            var alerts = new List<ProjetChargeAlertViewModel>();
            var overloadedResources = resourceRows.Where(r => r.UtilizationRate > 100).ToList();
            if (overloadedResources.Any())
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "danger",
                    IconClass = "bi bi-exclamation-triangle-fill",
                    Message = $"{overloadedResources.Count} ressource(s) dépassent la capacité disponible sur la période affichée."
                });
            }

            var weeksPending = weeklySummaries.Count(w => w.PendingValidations > 0);
            if (weeksPending > 0)
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-clock-history",
                    Message = $"{weeksPending} semaine(s) comportent des charges en attente de validation."
                });
            }

            var missingEntries = weeklySummaries.Sum(w => w.MissingEntries);
            if (missingEntries > 0)
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-hourglass-split",
                    Message = $"{missingEntries} saisie(s) réelle(s) restent à compléter."
                });
            }

            if (totalActual > totalPlanned && totalPlanned > 0)
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "danger",
                    IconClass = "bi bi-graph-up-arrow",
                    Message = $"La charge réelle cumulée dépasse le prévisionnel de {(totalActual - totalPlanned):N1} h."
                });
            }

            if (projet.PourcentageAvancement < 50 && totalActual > 0 && totalPlanned > 0 && totalActual < (totalPlanned * 0.4m))
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-slash-circle",
                    Message = "Le projet présente un avancement faible avec une consommation de charge anormale."
                });
            }

            if (projet.FicheProjet != null &&
                projet.FicheProjet.BudgetPrevisionnel.GetValueOrDefault() > 0 &&
                projet.FicheProjet.BudgetConsomme.GetValueOrDefault() > projet.FicheProjet.BudgetPrevisionnel.GetValueOrDefault())
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-cash-stack",
                    Message = "Le budget consommé dépasse le budget prévisionnel du projet."
                });
            }

            var activities = activeCharges
                .Where(c => !string.IsNullOrWhiteSpace(c.Commentaire) || c.ChargeReelle.HasValue || !string.IsNullOrWhiteSpace(c.Activite))
                .OrderByDescending(c => c.DateSaisieChargeReelle ?? c.DateModification ?? c.DateCreation)
                .Take(12)
                .Select(c => new ProjetChargeActivityViewModel
                {
                    DateLabel = (c.DateSaisieChargeReelle ?? c.DateModification ?? c.DateCreation).ToString("dd/MM/yyyy"),
                    Resource = $"{c.Ressource?.Nom} {c.Ressource?.Prenoms}".Trim(),
                    Phase = projet.PhaseWorkflowLabel,
                    TypeActivite = string.IsNullOrWhiteSpace(c.TypeActivite) ? "Non précisé" : c.TypeActivite,
                    Activite = string.IsNullOrWhiteSpace(c.Activite) ? "Activité non renseignée." : c.Activite,
                    Hours = c.ChargeReelle ?? c.ChargePrevisionnelle,
                    Comment = string.IsNullOrWhiteSpace(c.Commentaire) ? "Aucun détail saisi." : c.Commentaire!
                })
                .ToList();

            var viewModel = new ProjetChargesViewModel
            {
                ProjetId = projet.Id,
                CodeProjet = projet.CodeProjet,
                Titre = projet.Titre,
                Direction = projet.Direction?.Libelle ?? "Non définie",
                Sponsor = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}".Trim(),
                ChefProjet = projet.ChefProjet != null ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}".Trim() : "Non affecté",
                Phase = projet.PhaseWorkflowLabel,
                Statut = projet.StatutWorkflowLabel,
                Avancement = projet.PourcentageAvancement,
                Etat = projet.EtatProjet.ToString(),
                ProchainJalon = projet.FicheProjet?.ProchainJalon ?? "À définir",
                BudgetPrevisionnel = projet.FicheProjet?.BudgetPrevisionnel ?? 0,
                BudgetConsomme = projet.FicheProjet?.BudgetConsomme ?? 0,
                BudgetEcart = (projet.FicheProjet?.BudgetConsomme ?? 0) - (projet.FicheProjet?.BudgetPrevisionnel ?? 0),
                ChargePrevisionnelleTotale = totalPlanned,
                ChargeReelleTotale = totalActual,
                ChargeRestanteEstimee = totalPlanned - totalActual,
                ChargeEcartTotale = totalVariance,
                CapaciteTotale = totalCapacity,
                TauxCapaciteUtilise = totalCapacity > 0 ? Math.Round((double)(totalActual / totalCapacity) * 100, 1) : 0,
                TauxConsommation = totalPlanned > 0 ? Math.Round((double)(totalActual / totalPlanned) * 100, 1) : 0,
                NombreRessources = resourceRows.Count,
                RessourcesSurchargees = overloadedResources.Count,
                ChargesEnAttenteValidation = activeCharges.Count(c => c.StatutValidation == StatutValidationCharge.EnAttente),
                CanEditForecast = canEditForecast,
                CanEditActual = canEditActual,
                CanValidateCharges = canValidateCharges,
                CanExport = canExport,
                Weeks = weekModels,
                Resources = resourceRows,
                WeeklySummaries = weeklySummaries,
                Alerts = alerts,
                Activities = activities
            };

            return View(viewModel);
        }

        // POST: Saisir / mettre à jour une charge
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SaisirCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut, decimal? chargePrevisionnelle, decimal? chargeReelle, string? commentaire, string? typeActivite, string? activite)
        {
            var projet = await _db.Projets
                .Include(p => p.Membres)
                .FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            var isPilotage = ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
            var isResource = userId == ressourceId;

            if (!isPilotage && !isResource)
                return Forbid();

            var canEditForecast = isPilotage;
            var canEditActual = isPilotage || isResource;

            var lundiSemaine = NormalizeToMonday(semaineDebut);

            var charge = await _db.ChargesProjets
                .FirstOrDefaultAsync(c => c.ProjetId == projetId &&
                                         c.RessourceId == ressourceId &&
                                         c.SemaineDebut.Date == lundiSemaine.Date);

            if (charge == null)
            {
                charge = new ChargeProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    RessourceId = ressourceId,
                    SemaineDebut = lundiSemaine,
                    ChargePrevisionnelle = canEditForecast ? Math.Max(chargePrevisionnelle ?? 0, 0) : 0,
                    ChargeReelle = canEditActual ? chargeReelle : null,
                    DateSaisieChargeReelle = canEditActual && chargeReelle.HasValue ? DateTime.Now : null,
                    SaisieParId = canEditActual && chargeReelle.HasValue ? userId : null,
                    Commentaire = commentaire ?? string.Empty,
                    TypeActivite = typeActivite?.Trim() ?? string.Empty,
                    Activite = activite?.Trim() ?? string.Empty,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };
                _db.ChargesProjets.Add(charge);
            }
            else
            {
                if (canEditForecast && chargePrevisionnelle.HasValue)
                    charge.ChargePrevisionnelle = Math.Max(chargePrevisionnelle.Value, 0);

                if (canEditActual)
                {
                    charge.ChargeReelle = chargeReelle;
                    charge.DateSaisieChargeReelle = chargeReelle.HasValue ? DateTime.Now : charge.DateSaisieChargeReelle;
                    charge.SaisieParId = chargeReelle.HasValue ? userId : charge.SaisieParId;
                    charge.Commentaire = commentaire ?? string.Empty;
                    charge.TypeActivite = typeActivite?.Trim() ?? string.Empty;
                    charge.Activite = activite?.Trim() ?? string.Empty;
                }

                charge.DateModification = DateTime.Now;
                charge.ModifiePar = _currentUserService.Matricule;
            }

            charge.StatutValidation = StatutValidationCharge.Brouillon;
            charge.DateSoumissionValidation = null;
            charge.DateValidation = null;
            charge.ValideeParId = null;
            charge.CommentaireValidation = string.Empty;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_CHARGE", "ChargeProjet", charge.Id,
                new { ProjetId = projetId, RessourceId = ressourceId, Semaine = lundiSemaine },
                new
                {
                    ChargePrevisionnelle = charge.ChargePrevisionnelle,
                    ChargeReelle = charge.ChargeReelle,
                    Commentaire = charge.Commentaire,
                    TypeActivite = charge.TypeActivite,
                    Activite = charge.Activite
                });

            return Json(new
            {
                success = true,
                planned = charge.ChargePrevisionnelle,
                actual = charge.ChargeReelle,
                comment = charge.Commentaire,
                typeActivite = charge.TypeActivite,
                activite = charge.Activite,
                validationStatus = GetValidationChargeLabel(charge.StatutValidation)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SoumettreCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut)
        {
            var projet = await _db.Projets
                .Include(p => p.Membres)
                .FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var isPilotage = await CanValidateChargesAsync(projet);
            var isResource = userId == ressourceId;
            if (!isPilotage && !isResource)
                return Forbid();

            var lundiSemaine = NormalizeToMonday(semaineDebut);
            var charge = await _db.ChargesProjets.FirstOrDefaultAsync(c => c.ProjetId == projetId && c.RessourceId == ressourceId && c.SemaineDebut.Date == lundiSemaine.Date);
            if (charge == null)
                return NotFound();

            charge.StatutValidation = StatutValidationCharge.EnAttente;
            charge.DateSoumissionValidation = DateTime.Now;
            charge.DateModification = DateTime.Now;
            charge.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("SOUMISSION_CHARGE", "ChargeProjet", charge.Id);

            return Json(new { success = true, status = GetValidationChargeLabel(charge.StatutValidation) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MettreAJourValidationCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut, StatutValidationCharge statut, string? commentaireValidation)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateChargesAsync(projet))
                return Forbid();

            var lundiSemaine = NormalizeToMonday(semaineDebut);
            var charge = await _db.ChargesProjets.FirstOrDefaultAsync(c => c.ProjetId == projetId && c.RessourceId == ressourceId && c.SemaineDebut.Date == lundiSemaine.Date);
            if (charge == null)
                return NotFound();

            if (statut == StatutValidationCharge.Brouillon)
                return BadRequest();

            charge.StatutValidation = statut;
            charge.DateValidation = DateTime.Now;
            charge.ValideeParId = userId;
            charge.CommentaireValidation = commentaireValidation?.Trim() ?? string.Empty;
            charge.DateModification = DateTime.Now;
            charge.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("VALIDATION_CHARGE", "ChargeProjet", charge.Id,
                new { Statut = statut.ToString(), Commentaire = charge.CommentaireValidation });

            return Json(new
            {
                success = true,
                status = GetValidationChargeLabel(charge.StatutValidation),
                statusClass = GetValidationChargeClass(charge.StatutValidation),
                comment = charge.CommentaireValidation
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ExportChargesCsv(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Charges)
                    .ThenInclude(c => c.Ressource)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            if (!await CanAccessChargesAsync(projet, User.GetUserIdOrThrow()))
                return Forbid();

            var builder = new StringBuilder();
            builder.AppendLine("Projet;Ressource;Semaine;Charge prévue;Charge réelle;Ecart;Type activité;Activité;Commentaire;Validation");

            foreach (var charge in projet.Charges.Where(c => !c.EstSupprime).OrderBy(c => c.SemaineDebut).ThenBy(c => c.Ressource.Nom))
            {
                var line = string.Join(";",
                    EscapeCsv(projet.CodeProjet),
                    EscapeCsv($"{charge.Ressource?.Nom} {charge.Ressource?.Prenoms}".Trim()),
                    EscapeCsv(charge.SemaineDebut.ToString("dd/MM/yyyy")),
                    EscapeCsv(charge.ChargePrevisionnelle.ToString("N1")),
                    EscapeCsv((charge.ChargeReelle ?? 0m).ToString("N1")),
                    EscapeCsv(((charge.ChargeReelle ?? 0m) - charge.ChargePrevisionnelle).ToString("N1")),
                    EscapeCsv(charge.TypeActivite),
                    EscapeCsv(charge.Activite),
                    EscapeCsv(charge.Commentaire),
                    EscapeCsv(GetValidationChargeLabel(charge.StatutValidation)));
                builder.AppendLine(line);
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv; charset=utf-8", $"charges-{projet.CodeProjet}.csv");
        }

        private static DateTime NormalizeToMonday(DateTime date)
        {
            return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        }

        private static (string Label, string CssClass) GetCapacityStatus(double utilizationRate)
        {
            if (utilizationRate > 100)
            {
                return ("Surchargé", "badge-modern-danger");
            }

            if (utilizationRate > 80)
            {
                return ("Saturé", "badge-modern-warning");
            }

            if (utilizationRate > 50)
            {
                return ("Presque saturé", "badge-modern-info");
            }

            return ("Disponible", "badge-modern-success");
        }

        private static string GetValidationChargeLabel(StatutValidationCharge statut)
        {
            return statut switch
            {
                StatutValidationCharge.Brouillon => "Brouillon",
                StatutValidationCharge.EnAttente => "En attente",
                StatutValidationCharge.Validee => "Validée",
                StatutValidationCharge.Commentee => "Commentée",
                StatutValidationCharge.Rejetee => "Rejetée",
                _ => "Brouillon"
            };
        }

        private static string GetValidationChargeClass(StatutValidationCharge statut)
        {
            return statut switch
            {
                StatutValidationCharge.Brouillon => "badge-modern-secondary",
                StatutValidationCharge.EnAttente => "badge-modern-warning",
                StatutValidationCharge.Validee => "badge-modern-success",
                StatutValidationCharge.Commentee => "badge-modern-info",
                StatutValidationCharge.Rejetee => "badge-modern-danger",
                _ => "badge-modern-secondary"
            };
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string GetProfilRessourceLabel(ProfilRessource? profil)
        {
            return profil switch
            {
                ProfilRessource.Developpement => "Développement",
                ProfilRessource.Infrastructure => "Infrastructure",
                ProfilRessource.Support => "Support",
                ProfilRessource.DBA => "DBA",
                ProfilRessource.ChefProjet => "Chefferie projet",
                ProfilRessource.Architecte => "Architecture",
                ProfilRessource.Analyste => "Analyse",
                ProfilRessource.Autre => "Autre",
                _ => "Non défini"
            };
        }

        private Task<bool> CurrentUserHasPermissionAsync(string controleur, string action)
            => _permissionService.CurrentUserHasPermissionAsync(controleur, action);

        /// <summary>
        /// Délègue à IProjetQueryService. Conservé pour compatibilité interne — à migrer progressivement.
        /// </summary>
        private Task<Guid?> GetCurrentUserDirectionIdAsync(Guid userId)
            => _projetQuery.GetUserDirectionIdAsync(userId);

        private async Task<bool> HasAdminScopeAsync()
        {
            return await CurrentUserHasPermissionAsync("Admin", "Users");
        }

        private async Task<bool> HasPortfolioGovernanceAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "Portefeuille");
        }

        private async Task<bool> HasChefProjetWorkflowAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "UpdateProgress") ||
                   await CurrentUserHasPermissionAsync("Projet", "ValiderAnalyse") ||
                   await CurrentUserHasPermissionAsync("Projet", "EditPlanification");
        }

        private async Task<bool> HasDmWorkflowAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "ValidationsProjet") ||
                   await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM");
        }

        private async Task<bool> HasDemandeurProjectAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDemandeur");
        }

        private async Task<ProjetUiPermissions> BuildProjectUiAsync(Projet projet, bool isReadOnly = false)
        {
            var userId = User.GetUserIdOrThrow();
            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            var isDemandeurProject = projet.DemandeProjet?.DemandeurId == userId;

            return await ProjetUiPermissionBuilder.BuildAsync(
                _permissionService,
                User,
                projet,
                isReadOnly: isReadOnly,
                isDemandeurProject: isDemandeurProject,
                currentUserDirectionId: currentUserDirectionId);
        }

        private async Task<bool> CanManageProjectAsChefProjetOrAdminAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanViewProjectAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanViewProject;
        }

        private async Task<bool> CanManageAnalyseAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanEditFicheProjetAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCharte || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanManagePlanificationAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditPlanification;
        }

        private async Task<bool> CanManageExecutionAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditExecution;
        }

        private async Task<bool> CanManageUatAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditUat;
        }

        private async Task<bool> CanManageClotureAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCloture;
        }

        private async Task<bool> CanManageCollaborationAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCollaboration;
        }

        private async Task<bool> CanManageLivrableAsync(Projet projet, PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => await CanManageAnalyseAsync(projet),
                PhaseProjet.PlanificationValidation => await CanManagePlanificationAsync(projet),
                PhaseProjet.ExecutionSuivi => await CanManageExecutionAsync(projet),
                PhaseProjet.UatMep => await CanManageUatAsync(projet),
                PhaseProjet.ClotureLeconsApprises => await CanManageClotureAsync(projet),
                _ => false
            };
        }

        private async Task<bool> CanUpdateProjectProgressAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanUpdateProgress || ui.CanForceStatus;
        }

        private async Task<bool> CanManageProjectMembersAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanEditTechnicalCommentAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditTechnicalComment;
        }

        private async Task<bool> CanAccessChargesAsync(Projet projet, Guid userId, Guid? ressourceId = null)
        {
            var ui = await BuildProjectUiAsync(projet);
            if (ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess)
            {
                return true;
            }

            if (ressourceId.HasValue && ressourceId.Value == userId)
            {
                return true;
            }

            var userEmail = await _db.Utilisateurs
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.EstSupprime)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return !string.IsNullOrWhiteSpace(userEmail) &&
                   projet.Membres.Any(m => !m.EstSupprime &&
                                          !string.IsNullOrWhiteSpace(m.Email) &&
                                          m.Email == userEmail);
        }

        private async Task<bool> CanValidateChargesAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanValidateClotureDmAsync(DemandeClotureProjet demande, Guid userId)
        {
            if (!await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM"))
            {
                return false;
            }

            if (demande.Projet.SponsorId == userId)
            {
                return true;
            }

            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            return currentUserDirectionId.HasValue && demande.Projet.DirectionId == currentUserDirectionId.Value;
        }

        private async Task<bool> CanValidateClotureDsiAsync(Guid userId)
        {
            return await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDSI") &&
                   await CanValidateCharteAsDsiAsync(userId);
        }

        private async Task<FicheProjet> GetOrCreateFicheProjetAsync(Guid projetId, Guid userId)
        {
            var fiche = await _db.FicheProjets.FirstOrDefaultAsync(f => f.ProjetId == projetId && !f.EstSupprime);
            if (fiche != null)
            {
                return fiche;
            }

            fiche = new FicheProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                DateDerniereMiseAJour = DateTime.Now,
                DerniereMiseAJourParId = userId
            };

            _db.FicheProjets.Add(fiche);
            return fiche;
        }

        private async Task EnsureProjectProgressDataLoadedAsync(Projet projet)
        {
            var entry = _db.Entry(projet);

            if (!entry.Collection(p => p.Livrables).IsLoaded)
                await entry.Collection(p => p.Livrables).LoadAsync();

            if (!entry.Collection(p => p.TachesPlanning).IsLoaded)
                await entry.Collection(p => p.TachesPlanning).LoadAsync();

            if (!entry.Collection(p => p.LignesRaci).IsLoaded)
                await entry.Collection(p => p.LignesRaci).LoadAsync();

            if (!entry.Collection(p => p.LignesCommunication).IsLoaded)
                await entry.Collection(p => p.LignesCommunication).LoadAsync();

            if (!entry.Collection(p => p.LignesBudgetPlanification).IsLoaded)
                await entry.Collection(p => p.LignesBudgetPlanification).LoadAsync();

            if (!entry.Collection(p => p.DemandesCloture).IsLoaded)
                await entry.Collection(p => p.DemandesCloture).LoadAsync();

            if (!entry.Reference(p => p.PvKickOff).IsLoaded)
                await entry.Reference(p => p.PvKickOff).LoadAsync();

            if (!entry.Reference(p => p.CharteProjet).IsLoaded)
                await entry.Reference(p => p.CharteProjet).LoadAsync();

            if (!entry.Reference(p => p.FicheProjet).IsLoaded)
                await entry.Reference(p => p.FicheProjet).LoadAsync();
        }

        private async Task RecalculateProjectProgressAsync(Projet projet, bool persistChanges = false)
        {
            await EnsureProjectProgressDataLoadedAsync(projet);

            var automaticProgress = ComputeAutomaticProgress(projet);
            if (projet.PourcentageAvancement == automaticProgress)
            {
                return;
            }

            projet.PourcentageAvancement = automaticProgress;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            if (persistChanges)
            {
                await _db.SaveChangesAsync();
            }
        }

        private static int ComputeAutomaticProgress(Projet projet)
        {
            if (projet.StatutProjet == StatutProjet.Cloture || projet.StatutProjet == StatutProjet.Annule)
            {
                return 100;
            }

            var livrables = projet.Livrables?
                .Where(l => !l.EstSupprime)
                .Select(l => l.TypeLivrable)
                .ToHashSet() ?? new HashSet<TypeLivrable>();

            var fiche = projet.FicheProjet;

            return projet.PhaseActuelle switch
            {
                PhaseProjet.AnalyseClarification => ComputeProgressBand(
                    0,
                    20,
                    CountCompletedChecks(
                        livrables.Contains(TypeLivrable.CharteProjet),
                        HasCompleteSignedCharte(projet),
                        projet.CharteValideeParDM,
                        projet.CharteValideeParDSI),
                    4),
                PhaseProjet.PlanificationValidation => ComputeProgressBand(
                    20,
                    40,
                    CountCompletedChecks(
                        livrables.Contains(TypeLivrable.PlanningDetaille),
                        livrables.Contains(TypeLivrable.Wbs),
                        livrables.Contains(TypeLivrable.MatriceRaci),
                        livrables.Contains(TypeLivrable.SchemaCommunication),
                        livrables.Contains(TypeLivrable.BudgetPrevisionnel),
                        livrables.Contains(TypeLivrable.PvKickOff),
                        projet.PlanningValideParDM),
                    7),
                PhaseProjet.ExecutionSuivi => ComputeExecutionProgress(projet),
                PhaseProjet.UatMep => ComputeProgressBand(
                    80,
                    95,
                    CountCompletedChecks(
                        fiche?.DateDebutRecette.HasValue == true &&
                        fiche.DateFinRecette.HasValue &&
                        !string.IsNullOrWhiteSpace(fiche.UtilisateursTesteurs) &&
                        !string.IsNullOrWhiteSpace(fiche.PerimetreTeste),
                        fiche?.DateMepPrevue.HasValue == true &&
                        !string.IsNullOrWhiteSpace(fiche.PlanMep) &&
                        !string.IsNullOrWhiteSpace(fiche.PlanRollback),
                        livrables.Contains(TypeLivrable.PvRecette),
                        projet.RecetteValidee,
                        projet.MepEffectuee,
                        fiche?.HypercareTermine == true),
                    6),
                PhaseProjet.ClotureLeconsApprises => ComputeProgressBand(
                    95,
                    99,
                    CountCompletedChecks(
                        HasClosureSummary(projet),
                        HasRunTransferReady(fiche),
                        GetActiveClosureRequest(projet) != null,
                        GetActiveClosureRequest(projet)?.StatutValidationDemandeur == StatutValidationCloture.Validee,
                        GetActiveClosureRequest(projet)?.StatutValidationDirecteurMetier == StatutValidationCloture.Validee,
                        GetActiveClosureRequest(projet)?.StatutValidationDSI == StatutValidationCloture.Validee),
                    6),
                _ => projet.PourcentageAvancement
            };
        }

        private static int ComputeExecutionProgress(Projet projet)
        {
            var activeTasks = projet.TachesPlanning?
                .Where(t => !t.EstSupprime)
                .ToList() ?? new List<TachePlanningProjet>();

            if (activeTasks.Count > 0)
            {
                var averageTaskProgress = activeTasks.Average(t => Math.Clamp(t.Avancement, 0, 100));
                return ComputeProgressBand(40, 80, averageTaskProgress, 100d);
            }

            var fiche = projet.FicheProjet;
            var completed = CountCompletedChecks(
                fiche?.DateDebutReelleExecution.HasValue == true,
                !string.IsNullOrWhiteSpace(fiche?.ActionsRealiseesExecution),
                !string.IsNullOrWhiteSpace(fiche?.ActionsAVenirExecution),
                !string.IsNullOrWhiteSpace(fiche?.SyntheseChargesExecution));

            return ComputeProgressBand(40, 80, completed, 4);
        }

        private static int CountCompletedChecks(params bool[] checks)
        {
            return checks.Count(check => check);
        }

        private static int ComputeProgressBand(int start, int end, int completed, int total)
        {
            if (total <= 0)
            {
                return start;
            }

            return ComputeProgressBand(start, end, completed, (double)total);
        }

        private static int ComputeProgressBand(int start, int end, double completed, double total)
        {
            if (total <= 0)
            {
                return start;
            }

            var ratio = Math.Clamp(completed / total, 0d, 1d);
            return Math.Clamp(start + (int)Math.Round((end - start) * ratio), start, end);
        }

        private static DemandeClotureProjet? GetActiveClosureRequest(Projet projet)
        {
            return projet.DemandesCloture?
                .Where(d => !d.EstSupprime)
                .OrderByDescending(d => d.DateDemande)
                .FirstOrDefault(d => !d.EstTerminee);
        }

        private static bool HasRunTransferReady(FicheProjet? fiche)
        {
            return fiche?.TransfertRunDocumentation == true &&
                   fiche.TransfertRunAcces &&
                   fiche.TransfertRunSupportInforme &&
                   fiche.TransfertRunExploitationPrete;
        }

        private static bool HasClosureSummary(Projet projet)
        {
            return !string.IsNullOrWhiteSpace(projet.BilanPerimetre) &&
                   !string.IsNullOrWhiteSpace(projet.BilanPlanning) &&
                   !string.IsNullOrWhiteSpace(projet.BilanBudget) &&
                   !string.IsNullOrWhiteSpace(projet.BilanReussites) &&
                   !string.IsNullOrWhiteSpace(projet.BilanDifficultes);
        }

        private List<PlanningTacheInputViewModel> ParsePlanningTaches(string? ganttPayload)
        {
            if (string.IsNullOrWhiteSpace(ganttPayload))
            {
                return new List<PlanningTacheInputViewModel>();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tasks = JsonSerializer.Deserialize<List<PlanningTacheInputViewModel>>(ganttPayload, options)
                ?? new List<PlanningTacheInputViewModel>();

            var filtered = new List<PlanningTacheInputViewModel>();
            for (var index = 0; index < tasks.Count; index++)
            {
                var task = tasks[index];
                var isCompletelyEmpty = string.IsNullOrWhiteSpace(task.Libelle)
                    && string.IsNullOrWhiteSpace(task.CodeWbs)
                    && !task.DateDebutPrevue.HasValue
                    && !task.DateFinPrevue.HasValue
                    && string.IsNullOrWhiteSpace(task.Responsable)
                    && string.IsNullOrWhiteSpace(task.Dependances)
                    && string.IsNullOrWhiteSpace(task.Commentaire)
                    && task.Avancement == 0
                    && !task.EstJalon;

                if (isCompletelyEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(task.Libelle))
                    throw new InvalidOperationException($"La tâche #{index + 1} doit avoir un libellé.");

                if (!task.DateDebutPrevue.HasValue || !task.DateFinPrevue.HasValue)
                    throw new InvalidOperationException($"La tâche \"{task.Libelle}\" doit avoir une date de début et une date de fin.");

                if (task.DateFinPrevue.Value.Date < task.DateDebutPrevue.Value.Date)
                    throw new InvalidOperationException($"La tâche \"{task.Libelle}\" a une date de fin antérieure à sa date de début.");

                task.CodeWbs = string.IsNullOrWhiteSpace(task.CodeWbs) ? $"{filtered.Count + 1}" : task.CodeWbs.Trim();
                task.Libelle = task.Libelle.Trim();
                task.Responsable = task.Responsable?.Trim() ?? string.Empty;
                task.Dependances = task.Dependances?.Trim() ?? string.Empty;
                task.Commentaire = task.Commentaire?.Trim() ?? string.Empty;
                task.Avancement = Math.Clamp(task.Avancement, 0, 100);
                task.Ordre = filtered.Count;
                filtered.Add(task);
            }

            return filtered;
        }

        private async Task ReplacePlanningTasksAsync(Projet projet, Guid userId, IReadOnlyList<PlanningTacheInputViewModel> tasks)
        {
            var existingTasks = await _db.TachesPlanningProjets
                .Where(t => t.ProjetId == projet.Id && !t.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingTasks)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            if (tasks.Count == 0)
            {
                return;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var task in tasks)
            {
                _db.TachesPlanningProjets.Add(new TachePlanningProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    CodeWbs = task.CodeWbs,
                    Libelle = task.Libelle,
                    Responsable = task.Responsable,
                    Dependances = task.Dependances,
                    Commentaire = task.Commentaire,
                    DateDebutPrevue = task.DateDebutPrevue!.Value.Date,
                    DateFinPrevue = task.DateFinPrevue!.Value.Date,
                    Avancement = Math.Clamp(task.Avancement, 0, 100),
                    Ordre = task.Ordre,
                    EstJalon = task.EstJalon,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private static string FormatPlanningTaskSummary(TachePlanningProjet task)
        {
            var suffix = task.EstJalon ? " [jalon]" : string.Empty;
            return $"{task.CodeWbs} - {task.Libelle} ({task.DateDebutPrevue:dd/MM/yyyy} -> {task.DateFinPrevue:dd/MM/yyyy}){suffix}";
        }

        private void SynchronizePlanningSummary(Projet projet, FicheProjet ficheProjet, IReadOnlyList<PlanningTacheInputViewModel> taskInputs)
        {
            if (taskInputs.Count == 0)
            {
                return;
            }

            var taskEntities = taskInputs
                .Select(t => new TachePlanningProjet
                {
                    CodeWbs = t.CodeWbs,
                    Libelle = t.Libelle,
                    Responsable = t.Responsable,
                    Dependances = t.Dependances,
                    Commentaire = t.Commentaire,
                    DateDebutPrevue = t.DateDebutPrevue!.Value.Date,
                    DateFinPrevue = t.DateFinPrevue!.Value.Date,
                    Avancement = t.Avancement,
                    Ordre = t.Ordre,
                    EstJalon = t.EstJalon
                })
                .OrderBy(t => t.DateDebutPrevue)
                .ThenBy(t => t.Ordre)
                .ToList();

            var nextMilestone = taskEntities
                .Where(t => t.EstJalon && t.Avancement < 100)
                .OrderBy(t => t.DateDebutPrevue)
                .FirstOrDefault()
                ?? taskEntities
                    .Where(t => t.Avancement < 100)
                    .OrderBy(t => t.DateDebutPrevue)
                    .FirstOrDefault();

            var milestoneTasks = taskEntities.Where(t => t.EstJalon).OrderBy(t => t.DateDebutPrevue).ToList();

            ficheProjet.ProchainJalon = nextMilestone == null
                ? ficheProjet.ProchainJalon
                : $"{nextMilestone.CodeWbs} - {nextMilestone.Libelle} ({nextMilestone.DateDebutPrevue:dd/MM/yyyy})";
            ficheProjet.JalonsPrincipaux = string.Join(Environment.NewLine,
                (milestoneTasks.Any() ? milestoneTasks : taskEntities.Take(6))
                .Select(FormatPlanningTaskSummary));
            ficheProjet.DecoupageLotsTravail = string.Join(Environment.NewLine, taskEntities.Select(FormatPlanningTaskSummary));
            ficheProjet.PlanificationRessources = string.Join(Environment.NewLine,
                taskEntities
                    .Where(t => !string.IsNullOrWhiteSpace(t.Responsable))
                    .GroupBy(t => t.Responsable)
                    .Select(g => $"{g.Key} : {string.Join(", ", g.Select(t => t.Libelle))}"));

            projet.DateDebut = taskEntities.Min(t => t.DateDebutPrevue);
            projet.DateFinPrevue = taskEntities.Max(t => t.DateFinPrevue);
        }

        private List<RaciLigneInputViewModel> ParseRaciLignes(string? raciPayload)
        {
            if (string.IsNullOrWhiteSpace(raciPayload))
            {
                return new List<RaciLigneInputViewModel>();
            }

            var lines = JsonSerializer.Deserialize<List<RaciLigneInputViewModel>>(raciPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<RaciLigneInputViewModel>();

            var filtered = new List<RaciLigneInputViewModel>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var isEmpty = string.IsNullOrWhiteSpace(line.Activite)
                    && string.IsNullOrWhiteSpace(line.CodeActivite)
                    && string.IsNullOrWhiteSpace(line.Responsable)
                    && string.IsNullOrWhiteSpace(line.Approbateur)
                    && string.IsNullOrWhiteSpace(line.Consulte)
                    && string.IsNullOrWhiteSpace(line.Informe);

                if (isEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Activite))
                    throw new InvalidOperationException($"La ligne RACI #{index + 1} doit préciser une activité.");

                line.CodeActivite = line.CodeActivite?.Trim() ?? string.Empty;
                line.Activite = line.Activite.Trim();
                line.Responsable = line.Responsable?.Trim() ?? string.Empty;
                line.Approbateur = line.Approbateur?.Trim() ?? string.Empty;
                line.Consulte = line.Consulte?.Trim() ?? string.Empty;
                line.Informe = line.Informe?.Trim() ?? string.Empty;
                line.Ordre = filtered.Count;
                filtered.Add(line);
            }

            return filtered;
        }

        private List<CommunicationLigneInputViewModel> ParseCommunicationLignes(string? communicationPayload)
        {
            if (string.IsNullOrWhiteSpace(communicationPayload))
            {
                return new List<CommunicationLigneInputViewModel>();
            }

            var lines = JsonSerializer.Deserialize<List<CommunicationLigneInputViewModel>>(communicationPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CommunicationLigneInputViewModel>();

            var filtered = new List<CommunicationLigneInputViewModel>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var isEmpty = string.IsNullOrWhiteSpace(line.Instance)
                    && string.IsNullOrWhiteSpace(line.Objectif)
                    && string.IsNullOrWhiteSpace(line.Frequence)
                    && string.IsNullOrWhiteSpace(line.Canal)
                    && string.IsNullOrWhiteSpace(line.Participants)
                    && string.IsNullOrWhiteSpace(line.Responsable)
                    && !line.EstCopil;

                if (isEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Instance))
                    throw new InvalidOperationException($"La ligne communication #{index + 1} doit préciser une instance.");

                line.Instance = line.Instance.Trim();
                line.Objectif = line.Objectif?.Trim() ?? string.Empty;
                line.Frequence = line.Frequence?.Trim() ?? string.Empty;
                line.Canal = line.Canal?.Trim() ?? string.Empty;
                line.Participants = line.Participants?.Trim() ?? string.Empty;
                line.Responsable = line.Responsable?.Trim() ?? string.Empty;
                line.Ordre = filtered.Count;
                filtered.Add(line);
            }

            return filtered;
        }

        private List<BudgetLigneInputViewModel> ParseBudgetLignes(string? budgetPayload)
        {
            if (string.IsNullOrWhiteSpace(budgetPayload))
            {
                return new List<BudgetLigneInputViewModel>();
            }

            var lines = JsonSerializer.Deserialize<List<BudgetLigneInputViewModel>>(budgetPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<BudgetLigneInputViewModel>();

            var filtered = new List<BudgetLigneInputViewModel>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var isEmpty = string.IsNullOrWhiteSpace(line.Poste)
                    && string.IsNullOrWhiteSpace(line.Description)
                    && line.Montant == 0
                    && string.IsNullOrWhiteSpace(line.Commentaire);

                if (isEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Poste))
                    throw new InvalidOperationException($"La ligne budget #{index + 1} doit préciser un poste.");

                if (line.Montant < 0)
                    throw new InvalidOperationException($"La ligne budget \"{line.Poste}\" ne peut pas avoir un montant négatif.");

                line.Poste = line.Poste.Trim();
                line.Description = line.Description?.Trim() ?? string.Empty;
                line.Commentaire = line.Commentaire?.Trim() ?? string.Empty;
                line.Ordre = filtered.Count;
                filtered.Add(line);
            }

            return filtered;
        }

        private PvKickOffInputViewModel ParsePvKickOff(string? kickOffPayload)
        {
            if (string.IsNullOrWhiteSpace(kickOffPayload))
            {
                return new PvKickOffInputViewModel();
            }

            var model = JsonSerializer.Deserialize<PvKickOffInputViewModel>(kickOffPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PvKickOffInputViewModel();

            model.Heure = model.Heure?.Trim() ?? string.Empty;
            model.Lieu = model.Lieu?.Trim() ?? string.Empty;
            model.Animateur = model.Animateur?.Trim() ?? string.Empty;
            model.Objectifs = model.Objectifs?.Trim() ?? string.Empty;
            model.Participants = model.Participants?.Trim() ?? string.Empty;
            model.OrdreDuJour = model.OrdreDuJour?.Trim() ?? string.Empty;
            model.Decisions = model.Decisions?.Trim() ?? string.Empty;
            model.Actions = model.Actions?.Trim() ?? string.Empty;
            model.Commentaires = model.Commentaires?.Trim() ?? string.Empty;
            return model;
        }

        private static bool HasKickOffData(PvKickOffInputViewModel? model)
        {
            return model != null
                && (model.DateReunion.HasValue
                    || !string.IsNullOrWhiteSpace(model.Heure)
                    || !string.IsNullOrWhiteSpace(model.Lieu)
                    || !string.IsNullOrWhiteSpace(model.Animateur)
                    || !string.IsNullOrWhiteSpace(model.Objectifs)
                    || !string.IsNullOrWhiteSpace(model.Participants)
                    || !string.IsNullOrWhiteSpace(model.OrdreDuJour)
                    || !string.IsNullOrWhiteSpace(model.Decisions)
                    || !string.IsNullOrWhiteSpace(model.Actions)
                    || !string.IsNullOrWhiteSpace(model.Commentaires));
        }

        private async Task ReplaceRaciLinesAsync(Projet projet, IReadOnlyList<RaciLigneInputViewModel> lines)
        {
            var existingLines = await _db.LignesRaciProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLines)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var line in lines)
            {
                _db.LignesRaciProjets.Add(new LigneRaciProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    CodeActivite = line.CodeActivite,
                    Activite = line.Activite,
                    Responsable = line.Responsable,
                    Approbateur = line.Approbateur,
                    Consulte = line.Consulte,
                    Informe = line.Informe,
                    Ordre = line.Ordre,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private async Task ReplaceCommunicationLinesAsync(Projet projet, IReadOnlyList<CommunicationLigneInputViewModel> lines)
        {
            var existingLines = await _db.LignesCommunicationProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLines)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var line in lines)
            {
                _db.LignesCommunicationProjets.Add(new LigneCommunicationProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    Instance = line.Instance,
                    Objectif = line.Objectif,
                    Frequence = line.Frequence,
                    Canal = line.Canal,
                    Participants = line.Participants,
                    Responsable = line.Responsable,
                    EstCopil = line.EstCopil,
                    Ordre = line.Ordre,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private async Task ReplaceBudgetLinesAsync(Projet projet, IReadOnlyList<BudgetLigneInputViewModel> lines)
        {
            var existingLines = await _db.LignesBudgetPlanificationProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLines)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var line in lines)
            {
                _db.LignesBudgetPlanificationProjets.Add(new LigneBudgetPlanificationProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    Poste = line.Poste,
                    Description = line.Description,
                    Montant = line.Montant,
                    Commentaire = line.Commentaire,
                    Ordre = line.Ordre,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private async Task UpsertPvKickOffAsync(Projet projet, Guid userId, PvKickOffInputViewModel kickOff)
        {
            var existing = await _db.PvKickOffProjets
                .FirstOrDefaultAsync(pv => pv.ProjetId == projet.Id && !pv.EstSupprime);

            if (!HasKickOffData(kickOff))
            {
                if (existing != null)
                {
                    existing.EstSupprime = true;
                    existing.DateModification = DateTime.Now;
                    existing.ModifiePar = _currentUserService.Matricule;
                }

                return;
            }

            if (existing == null)
            {
                existing = new PvKickOffProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                };
                _db.PvKickOffProjets.Add(existing);
            }

            existing.DateReunion = kickOff.DateReunion?.Date;
            existing.Heure = kickOff.Heure;
            existing.Lieu = kickOff.Lieu;
            existing.Animateur = kickOff.Animateur;
            existing.Objectifs = kickOff.Objectifs;
            existing.Participants = kickOff.Participants;
            existing.OrdreDuJour = kickOff.OrdreDuJour;
            existing.Decisions = kickOff.Decisions;
            existing.Actions = kickOff.Actions;
            existing.Commentaires = kickOff.Commentaires;
            existing.DateModification = DateTime.Now;
            existing.ModifiePar = _currentUserService.Matricule;
        }

        private void SynchronizeRaciSummary(FicheProjet ficheProjet, IReadOnlyList<RaciLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                ficheProjet.RaciParActivite = string.Empty;
                return;
            }

            ficheProjet.RaciParActivite = string.Join(Environment.NewLine,
                lines.Select(l => $"{(string.IsNullOrWhiteSpace(l.CodeActivite) ? string.Empty : $"{l.CodeActivite} - ")}{l.Activite} | R: {l.Responsable} | A: {l.Approbateur} | C: {l.Consulte} | I: {l.Informe}"));
        }

        private void SynchronizeCommunicationSummary(FicheProjet ficheProjet, IReadOnlyList<CommunicationLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            ficheProjet.FrequenceReunions = string.Join(" ; ", lines.Select(l => $"{l.Instance}: {l.Frequence}").Where(v => !v.EndsWith(": ")));
            ficheProjet.ParticipantsReunions = string.Join(Environment.NewLine,
                lines.Select(l => $"{l.Instance}: {l.Participants}").Where(v => !v.EndsWith(": ")));
            ficheProjet.CanalCommunication = string.Join(" ; ",
                lines.Select(l => $"{l.Instance}: {l.Canal}").Where(v => !v.EndsWith(": ")));
            ficheProjet.CopilPrevu = lines.Any(l => l.EstCopil);
        }

        private void SynchronizeBudgetSummary(FicheProjet ficheProjet, IReadOnlyList<BudgetLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            ficheProjet.BudgetPrevisionnel = lines.Sum(l => l.Montant);
            ficheProjet.CommentaireBudgetPlanification = string.Join(Environment.NewLine,
                lines.Select(l => $"{l.Poste}: {l.Montant:N2} FCFA{(string.IsNullOrWhiteSpace(l.Commentaire) ? string.Empty : $" - {l.Commentaire}")}"));
        }

        private async Task<(List<string> Generated, List<string> Missing)> GenerateNativePlanificationLivrablesAsync(
            Projet projet,
            Guid userId,
            IReadOnlyList<PlanningTacheInputViewModel> planningTasks,
            IReadOnlyList<RaciLigneInputViewModel> raciLines,
            IReadOnlyList<CommunicationLigneInputViewModel> communicationLines,
            IReadOnlyList<BudgetLigneInputViewModel> budgetLines,
            PvKickOffInputViewModel kickOff)
        {
            var generated = new List<string>();
            var missing = new List<string>();
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var planningEntities = planningTasks
                .Select((task, index) => new TachePlanningProjet
                {
                    CodeWbs = task.CodeWbs,
                    Libelle = task.Libelle,
                    Responsable = task.Responsable,
                    DateDebutPrevue = task.DateDebutPrevue ?? DateTime.Today,
                    DateFinPrevue = task.DateFinPrevue ?? task.DateDebutPrevue ?? DateTime.Today,
                    Avancement = task.Avancement,
                    EstJalon = task.EstJalon,
                    Dependances = task.Dependances,
                    Commentaire = task.Commentaire,
                    Ordre = index
                })
                .ToList();

            if (planningEntities.Any())
            {
                var planningBytes = await _excelService.GeneratePlanningDetailleExcelAsync(projet, planningEntities);
                var wbsBytes = await _excelService.GenerateWbsExcelAsync(projet, planningEntities);

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.PlanningDetaille,
                    $"PlanningDetaille_{projet.CodeProjet}_{timestamp}.xlsx",
                    planningBytes,
                    "Généré automatiquement depuis le planning interactif.");
                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.Wbs,
                    $"WBS_{projet.CodeProjet}_{timestamp}.xlsx",
                    wbsBytes,
                    "Généré automatiquement depuis le planning interactif.");

                generated.Add("Planning détaillé");
                generated.Add("WBS");
            }
            else
            {
                missing.Add("Planning détaillé / WBS");
            }

            if (raciLines.Any())
            {
                var raciBytes = await _excelService.GenerateMatriceRaciExcelAsync(projet, raciLines
                    .Select((line, index) => new LigneRaciProjet
                    {
                        CodeActivite = line.CodeActivite,
                        Activite = line.Activite,
                        Responsable = line.Responsable,
                        Approbateur = line.Approbateur,
                        Consulte = line.Consulte,
                        Informe = line.Informe,
                        Ordre = index
                    })
                    .ToList());

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.MatriceRaci,
                    $"MatriceRaci_{projet.CodeProjet}_{timestamp}.xlsx",
                    raciBytes,
                    "Généré automatiquement depuis la matrice RACI native.");
                generated.Add("Matrice RACI");
            }
            else
            {
                missing.Add("Matrice RACI");
            }

            if (communicationLines.Any())
            {
                var communicationBytes = await _excelService.GenerateSchemaCommunicationExcelAsync(projet, communicationLines
                    .Select((line, index) => new LigneCommunicationProjet
                    {
                        Instance = line.Instance,
                        Objectif = line.Objectif,
                        Frequence = line.Frequence,
                        Canal = line.Canal,
                        Participants = line.Participants,
                        Responsable = line.Responsable,
                        EstCopil = line.EstCopil,
                        Ordre = index
                    })
                    .ToList());

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.SchemaCommunication,
                    $"SchemaCommunication_{projet.CodeProjet}_{timestamp}.xlsx",
                    communicationBytes,
                    "Généré automatiquement depuis le plan de communication natif.");
                generated.Add("Schéma de communication");
            }
            else
            {
                missing.Add("Schéma de communication");
            }

            if (budgetLines.Any())
            {
                var budgetBytes = await _excelService.GenerateBudgetPrevisionnelExcelAsync(projet, budgetLines
                    .Select((line, index) => new LigneBudgetPlanificationProjet
                    {
                        Poste = line.Poste,
                        Description = line.Description,
                        Montant = line.Montant,
                        Commentaire = line.Commentaire,
                        Ordre = index
                    })
                    .ToList(), projet.FicheProjet);

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.BudgetPrevisionnel,
                    $"BudgetPrevisionnel_{projet.CodeProjet}_{timestamp}.xlsx",
                    budgetBytes,
                    "Généré automatiquement depuis le budget natif.");
                generated.Add("Budget prévisionnel");
            }
            else if (projet.FicheProjet?.BudgetPrevisionnel.HasValue == true && projet.FicheProjet.BudgetPrevisionnel.Value > 0)
            {
                var budgetBytes = await _excelService.GenerateBudgetPrevisionnelExcelAsync(projet, Array.Empty<LigneBudgetPlanificationProjet>(), projet.FicheProjet);

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.BudgetPrevisionnel,
                    $"BudgetPrevisionnel_{projet.CodeProjet}_{timestamp}.xlsx",
                    budgetBytes,
                    "Généré automatiquement depuis le budget natif.");
                generated.Add("Budget prévisionnel");
            }
            else
            {
                missing.Add("Budget prévisionnel");
            }

            if (HasKickOffData(kickOff))
            {
                var kickOffBytes = await _excelService.GeneratePvKickOffExcelAsync(projet, new PvKickOffProjet
                {
                    DateReunion = kickOff.DateReunion,
                    Heure = kickOff.Heure,
                    Lieu = kickOff.Lieu,
                    Animateur = kickOff.Animateur,
                    Objectifs = kickOff.Objectifs,
                    Participants = kickOff.Participants,
                    OrdreDuJour = kickOff.OrdreDuJour,
                    Decisions = kickOff.Decisions,
                    Actions = kickOff.Actions,
                    Commentaires = kickOff.Commentaires
                });

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.PvKickOff,
                    $"PVKickOff_{projet.CodeProjet}_{timestamp}.xlsx",
                    kickOffBytes,
                    "Généré automatiquement depuis le PV de kick-off natif.");
                generated.Add("PV de kick-off");
            }
            else
            {
                missing.Add("PV de kick-off");
            }

            return (generated, missing);
        }

        private static byte[] BuildCsvBytes(IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(';', row.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        }

        private async Task ReplaceGeneratedPlanningLivrableAsync(
            Projet projet,
            Guid userId,
            TypeLivrable typeLivrable,
            string fileName,
            byte[] content,
            string comment)
        {
            var existingLivrables = await _db.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id
                    && l.Phase == PhaseProjet.PlanificationValidation
                    && l.TypeLivrable == typeLivrable
                    && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLivrables)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var relativePath = await _fileStorage.SaveGeneratedFileAsync(
                content,
                fileName,
                Path.Combine("projets", projet.CodeProjet, "planification", "generated"));

            _db.LivrablesProjets.Add(new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.PlanificationValidation,
                TypeLivrable = typeLivrable,
                NomDocument = fileName,
                CheminRelatif = relativePath,
                DateDepot = DateTime.Now,
                DeposeParId = userId,
                Commentaire = comment,
                Version = $"auto-{DateTime.Now:yyyyMMddHHmmss}",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM"
            });
        }

        // POST: Mettre à jour les données structurées de planification
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdatePlanification(
            Guid id,
            FicheProjet fiche,
            string? ganttPayload = null,
            string? raciPayload = null,
            string? communicationPayload = null,
            string? budgetPayload = null,
            string? kickOffPayload = null)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManagePlanificationAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);
            ficheProjet.ProchainJalon = fiche.ProchainJalon;
            ficheProjet.JalonsPrincipaux = fiche.JalonsPrincipaux;
            ficheProjet.DecoupageLotsTravail = fiche.DecoupageLotsTravail;
            ficheProjet.PlanificationRessources = fiche.PlanificationRessources;
            ficheProjet.RaciParActivite = fiche.RaciParActivite;
            ficheProjet.FrequenceReunions = fiche.FrequenceReunions;
            ficheProjet.ParticipantsReunions = fiche.ParticipantsReunions;
            ficheProjet.CanalCommunication = fiche.CanalCommunication;
            ficheProjet.CopilPrevu = fiche.CopilPrevu;
            ficheProjet.BudgetPrevisionnel = fiche.BudgetPrevisionnel;
            ficheProjet.CommentaireBudgetPlanification = fiche.CommentaireBudgetPlanification;
            ficheProjet.CommentaireValidationPlanification = fiche.CommentaireValidationPlanification;
            ficheProjet.SyntheseRisques = fiche.SyntheseRisques;
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            IReadOnlyList<PlanningTacheInputViewModel> planningTasks;
            IReadOnlyList<RaciLigneInputViewModel> raciLines;
            IReadOnlyList<CommunicationLigneInputViewModel> communicationLines;
            IReadOnlyList<BudgetLigneInputViewModel> budgetLines;
            PvKickOffInputViewModel kickOff;
            try
            {
                planningTasks = ParsePlanningTaches(ganttPayload);
                raciLines = ParseRaciLignes(raciPayload);
                communicationLines = ParseCommunicationLignes(communicationPayload);
                budgetLines = ParseBudgetLignes(budgetPayload);
                kickOff = ParsePvKickOff(kickOffPayload);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id, tab = "planification" });
            }

            if (fiche.BudgetPrevisionnel.HasValue && ficheProjet.BudgetConsomme.HasValue)
            {
                ficheProjet.EcartsBudget = ficheProjet.BudgetConsomme.Value - fiche.BudgetPrevisionnel.Value;
            }

            if (planningTasks.Count > 0)
            {
                SynchronizePlanningSummary(projet, ficheProjet, planningTasks);
            }

            SynchronizeRaciSummary(ficheProjet, raciLines);
            SynchronizeCommunicationSummary(ficheProjet, communicationLines);
            SynchronizeBudgetSummary(ficheProjet, budgetLines);

            await ReplacePlanningTasksAsync(projet, userId, planningTasks);
            await ReplaceRaciLinesAsync(projet, raciLines);
            await ReplaceCommunicationLinesAsync(projet, communicationLines);
            await ReplaceBudgetLinesAsync(projet, budgetLines);
            await UpsertPvKickOffAsync(projet, userId, kickOff);

            await _db.SaveChangesAsync();

            var generation = await GenerateNativePlanificationLivrablesAsync(
                projet,
                userId,
                planningTasks,
                raciLines,
                communicationLines,
                budgetLines,
                kickOff);

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_PLANIFICATION", "Projet", projet.Id);

            TempData["Success"] = generation.Generated.Any()
                ? $"Planification mise à jour. Livrables natifs générés : {string.Join(", ", generation.Generated)}."
                : "Planification mise à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererLivrablesPlanificationDepuisGantt(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManagePlanificationAsync(projet))
                return Forbid();

            var tasks = projet.TachesPlanning
                .Where(t => !t.EstSupprime)
                .OrderBy(t => t.Ordre)
                .ThenBy(t => t.DateDebutPrevue)
                .Select(t => new PlanningTacheInputViewModel
                {
                    CodeWbs = t.CodeWbs,
                    Libelle = t.Libelle,
                    Responsable = t.Responsable,
                    DateDebutPrevue = t.DateDebutPrevue,
                    DateFinPrevue = t.DateFinPrevue,
                    Avancement = t.Avancement,
                    EstJalon = t.EstJalon,
                    Dependances = t.Dependances,
                    Commentaire = t.Commentaire,
                    Ordre = t.Ordre
                })
                .ToList();

            var raciLines = projet.LignesRaci
                .Where(l => !l.EstSupprime)
                .OrderBy(l => l.Ordre)
                .Select(l => new RaciLigneInputViewModel
                {
                    CodeActivite = l.CodeActivite,
                    Activite = l.Activite,
                    Responsable = l.Responsable,
                    Approbateur = l.Approbateur,
                    Consulte = l.Consulte,
                    Informe = l.Informe,
                    Ordre = l.Ordre
                })
                .ToList();

            var communicationLines = projet.LignesCommunication
                .Where(l => !l.EstSupprime)
                .OrderBy(l => l.Ordre)
                .Select(l => new CommunicationLigneInputViewModel
                {
                    Instance = l.Instance,
                    Objectif = l.Objectif,
                    Frequence = l.Frequence,
                    Canal = l.Canal,
                    Participants = l.Participants,
                    Responsable = l.Responsable,
                    EstCopil = l.EstCopil,
                    Ordre = l.Ordre
                })
                .ToList();

            var budgetLines = projet.LignesBudgetPlanification
                .Where(l => !l.EstSupprime)
                .OrderBy(l => l.Ordre)
                .Select(l => new BudgetLigneInputViewModel
                {
                    Poste = l.Poste,
                    Description = l.Description,
                    Montant = l.Montant,
                    Commentaire = l.Commentaire,
                    Ordre = l.Ordre
                })
                .ToList();

            var kickOff = projet.PvKickOff == null || projet.PvKickOff.EstSupprime
                ? new PvKickOffInputViewModel()
                : new PvKickOffInputViewModel
                {
                    DateReunion = projet.PvKickOff.DateReunion,
                    Heure = projet.PvKickOff.Heure,
                    Lieu = projet.PvKickOff.Lieu,
                    Animateur = projet.PvKickOff.Animateur,
                    Objectifs = projet.PvKickOff.Objectifs,
                    Participants = projet.PvKickOff.Participants,
                    OrdreDuJour = projet.PvKickOff.OrdreDuJour,
                    Decisions = projet.PvKickOff.Decisions,
                    Actions = projet.PvKickOff.Actions,
                    Commentaires = projet.PvKickOff.Commentaires
                };

            var generation = await GenerateNativePlanificationLivrablesAsync(
                projet,
                userId,
                tasks,
                raciLines,
                communicationLines,
                budgetLines,
                kickOff);

            if (!generation.Generated.Any())
            {
                TempData["Error"] = "Complétez d'abord les sections natives de planification avant de générer les livrables.";
                return RedirectToAction(nameof(Details), new { id, tab = "planification" });
            }

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("GENERATE_PLANIFICATION_LIVRABLES_FROM_GANTT", "Projet", projet.Id);

            TempData["Success"] = $"Les livrables natifs ont été générés : {string.Join(", ", generation.Generated)}.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // POST: Mettre à jour les données de pilotage d'exécution
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateExecution(Guid id, FicheProjet fiche, EtatProjet etatProjet, DateTime? dateFinPrevue, string? prochainJalon, int? pourcentageAvancement = null)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);
            ficheProjet.DateDebutReelleExecution = fiche.DateDebutReelleExecution;
            ficheProjet.DateFinEstimeeExecution = fiche.DateFinEstimeeExecution ?? dateFinPrevue;
            ficheProjet.JustificationRetardExecution = fiche.JustificationRetardExecution;
            ficheProjet.CommentaireAvancementExecution = fiche.CommentaireAvancementExecution;
            ficheProjet.ActionsRealiseesExecution = fiche.ActionsRealiseesExecution;
            ficheProjet.ActionsAVenirExecution = fiche.ActionsAVenirExecution;
            ficheProjet.ProblemesBlocagesExecution = fiche.ProblemesBlocagesExecution;
            ficheProjet.BudgetConsomme = fiche.BudgetConsomme;
            ficheProjet.JustificationBudgetExecution = fiche.JustificationBudgetExecution;
            ficheProjet.SyntheseChargesExecution = fiche.SyntheseChargesExecution;
            ficheProjet.DecisionsExecution = fiche.DecisionsExecution;
            ficheProjet.ProchainJalon = prochainJalon;
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            projet.EtatProjet = etatProjet;
            projet.DateFinPrevue = dateFinPrevue;

            if (ficheProjet.BudgetPrevisionnel.HasValue && ficheProjet.BudgetConsomme.HasValue)
            {
                ficheProjet.EcartsBudget = ficheProjet.BudgetConsomme.Value - ficheProjet.BudgetPrevisionnel.Value;
            }

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_EXECUTION", "Projet", projet.Id);

            TempData["Success"] = "Suivi d'exécution mis à jour. L'avancement est recalculé automatiquement.";
            return RedirectToAction(nameof(Details), new { id, tab = "execution" });
        }

        // POST: Mettre à jour les données UAT / MEP
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateUat(Guid id, FicheProjet fiche, DateTime? dateMepReelle, bool mepEffectuee)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageUatAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);
            ficheProjet.DateDebutRecette = fiche.DateDebutRecette;
            ficheProjet.DateFinRecette = fiche.DateFinRecette;
            ficheProjet.UtilisateursTesteurs = fiche.UtilisateursTesteurs;
            ficheProjet.PerimetreTeste = fiche.PerimetreTeste;
            ficheProjet.DateMepPrevue = fiche.DateMepPrevue;
            ficheProjet.PrerequisMep = fiche.PrerequisMep;
            ficheProjet.PlanMep = fiche.PlanMep;
            ficheProjet.PlanRollback = fiche.PlanRollback;
            ficheProjet.ChangeRequis = fiche.ChangeRequis;
            ficheProjet.ReferenceChange = fiche.ReferenceChange;
            ficheProjet.StatutValidationChange = fiche.StatutValidationChange;
            ficheProjet.ResultatMep = fiche.ResultatMep;
            ficheProjet.IncidentsMep = fiche.IncidentsMep;
            ficheProjet.PeriodeHypercare = fiche.PeriodeHypercare;
            ficheProjet.IncidentsPostMep = fiche.IncidentsPostMep;
            ficheProjet.StatutHypercare = fiche.StatutHypercare;
            ficheProjet.HypercareTermine = fiche.HypercareTermine;
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            projet.MepEffectuee = mepEffectuee;
            projet.DateMep = mepEffectuee ? dateMepReelle ?? projet.DateMep ?? DateTime.Now : null;

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_UAT_MEP", "Projet", projet.Id);

            TempData["Success"] = "Données UAT / MEP mises à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "uat" });
        }

        // POST: Mettre à jour Bilan
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateBilan(
            Guid id,
            string? bilanPerimetre,
            string? bilanPlanning,
            string? bilanBudget,
            string? bilanDifficultes,
            string? bilanReussites,
            string? leconsReussites,
            string? leconsEchecs,
            string? leconsRecommandations,
            bool transfertRunDocumentation,
            bool transfertRunAcces,
            bool transfertRunSupportInforme,
            bool transfertRunExploitationPrete,
            string? statutFinalCloture,
            string? commentaireStatutFinal)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.Charges)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageClotureAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);

            projet.BilanPerimetre = bilanPerimetre?.Trim();
            projet.BilanPlanning = bilanPlanning?.Trim();
            projet.BilanBudget = bilanBudget?.Trim();
            projet.BilanDifficultes = bilanDifficultes?.Trim();
            projet.BilanReussites = bilanReussites?.Trim();
            projet.LeconsReussites = leconsReussites?.Trim();
            projet.LeconsEchecs = leconsEchecs?.Trim();
            projet.LeconsRecommandations = leconsRecommandations?.Trim();

            ficheProjet.TransfertRunDocumentation = transfertRunDocumentation;
            ficheProjet.TransfertRunAcces = transfertRunAcces;
            ficheProjet.TransfertRunSupportInforme = transfertRunSupportInforme;
            ficheProjet.TransfertRunExploitationPrete = transfertRunExploitationPrete;
            ficheProjet.StatutFinalCloture = string.IsNullOrWhiteSpace(statutFinalCloture) ? "Clôturé" : statutFinalCloture.Trim();
            ficheProjet.CommentaireStatutFinal = commentaireStatutFinal?.Trim();
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            var chargePrevue = projet.Charges.Where(c => !c.EstSupprime).Sum(c => c.ChargePrevisionnelle);
            var chargeReelle = projet.Charges.Where(c => !c.EstSupprime).Sum(c => c.ChargeReelle ?? 0m);
            projet.BilanCloture =
                $"Périmètre: {projet.BilanPerimetre}\n" +
                $"Planning: {projet.BilanPlanning}\n" +
                $"Budget: {projet.BilanBudget}\n" +
                $"Charge: prévu {chargePrevue:N1} h / réel {chargeReelle:N1} h\n" +
                $"Difficultés: {projet.BilanDifficultes}\n" +
                $"Réussites: {projet.BilanReussites}";

            projet.LeconsApprises =
                $"Réussites: {projet.LeconsReussites}\n" +
                $"Échecs: {projet.LeconsEchecs}\n" +
                $"Recommandations: {projet.LeconsRecommandations}";

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_BILAN", "Projet", projet.Id);

            TempData["Success"] = "Bilan de clôture mis à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }

        // POST: Ajouter Membre
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterMembre(Guid projetId, Guid utilisateurId, string roleDansProjet)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageProjectMembersAsync(projet))
                return Forbid();

            var utilisateur = await _db.Utilisateurs.FindAsync(utilisateurId);
            if (utilisateur == null)
            {
                TempData["Error"] = "Utilisateur non trouvé.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "analyse" });
            }

            var membre = new MembreProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Nom = utilisateur.Nom,
                Prenom = utilisateur.Prenoms,
                RoleDansProjet = roleDansProjet,
                Email = utilisateur.Email,
                DirectionLibelle = utilisateur.Direction?.Libelle ?? string.Empty,
                EstActif = true,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };

            _db.MembresProjets.Add(membre);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("AJOUT_MEMBRE", "MembreProjet", membre.Id);

            TempData["Success"] = "Membre ajouté au projet.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "analyse" });
        }

        // POST: Retirer/Désactiver un membre du projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RetirerMembre(Guid id, Guid membreId)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanManageProjectMembersAsync(projet))
                return Forbid();

            var membre = await _db.MembresProjets.FindAsync(membreId);
            if (membre == null || membre.ProjetId != id)
                return NotFound();

            // Soft delete : marquer comme supprimé
            membre.EstActif = false;
            membre.EstSupprime = true;
            membre.DateModification = DateTime.Now;
            membre.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("RETRAIT_MEMBRE_PROJET", "MembreProjet", membre.Id,
                new { ProjetId = id, MembreNom = $"{membre.Nom} {membre.Prenom}" },
                new { Action = "Retiré/Désactivé" });

            TempData["Success"] = "Membre retiré du projet.";
            return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
        }

        // POST: Mettre à jour un livrable (statut, commentaire, etc.)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateLivrable(Guid id, Guid livrableId, string? commentaire, string? version)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var livrable = await _db.LivrablesProjets.FindAsync(livrableId);
            if (livrable == null || livrable.ProjetId != id)
                return NotFound();

            if (!await CanManageLivrableAsync(projet, livrable.Phase))
                return Forbid();

            // Mettre à jour les champs fournis
            if (!string.IsNullOrWhiteSpace(commentaire))
                livrable.Commentaire = commentaire;

            if (!string.IsNullOrWhiteSpace(version))
                livrable.Version = version;

            livrable.DateModification = DateTime.Now;
            livrable.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MISE_A_JOUR_LIVRABLE", "LivrableProjet", livrable.Id,
                new { ProjetId = id, NomDocument = livrable.NomDocument },
                new { Version = livrable.Version, Commentaire = livrable.Commentaire });

            TempData["Success"] = "Livrable mis à jour avec succès.";
            return RedirectToAction(nameof(Details), new { id, tab = GetTabForPhase(livrable.Phase) });
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

        private string GetSubfolderForPhase(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => "analyse",
                PhaseProjet.PlanificationValidation => "planification",
                PhaseProjet.ExecutionSuivi => "execution",
                PhaseProjet.UatMep => "uat",
                PhaseProjet.ClotureLeconsApprises => "cloture",
                _ => "autres"
            };
        }

        private string GetTabForPhase(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => "analyse",
                PhaseProjet.PlanificationValidation => "planification",
                PhaseProjet.ExecutionSuivi => "execution",
                PhaseProjet.UatMep => "uat",
                PhaseProjet.ClotureLeconsApprises => "cloture",
                _ => "synthese"
            };
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

        // Helper method: Vérifie si un utilisateur peut agir comme chef de projet
        private async Task<bool> CanActAsChefProjetAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> IsActiveDsiDelegateAsync(Guid userId)
        {
            return await _db.DelegationsValidationDSI.AnyAsync(d =>
                d.DelegueId == userId &&
                d.EstActive &&
                d.DateDebut <= DateTime.Now &&
                d.DateFin >= DateTime.Now &&
                !d.EstSupprime);
        }

        private async Task<bool> CanValidateCharteAsDirecteurMetierAsync(Projet projet, Guid userId)
        {
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderCharteDM"))
            {
                return false;
            }

            if (projet.SponsorId == userId)
            {
                return true;
            }

            var userDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            return userDirectionId.HasValue && projet.DirectionId == userDirectionId.Value;
        }

        private async Task<bool> CanValidateCharteAsDsiAsync(Guid userId)
        {
            if (await CurrentUserHasPermissionAsync("Projet", "ValiderCharteDSI"))
            {
                return true;
            }

            return await IsActiveDsiDelegateAsync(userId);
        }

        private static bool AreRequiredCharteSignaturesCompleted(DossierSignatureProjet dossier)
        {
            var requiredSignataires = dossier.Signataires
                .Where(s => s.Role == RoleSignataireProjet.Sponsor || s.Role == RoleSignataireProjet.ChefDeProjet)
                .ToList();

            return requiredSignataires.Count == 2 &&
                   requiredSignataires.All(s => s.Statut == StatutSignataireDossierSignature.Signe);
        }

        private static void ResetCharteValidationState(Projet projet)
        {
            projet.CharteValideeParDM = false;
            projet.DateCharteValideeParDM = null;
            projet.CharteValideeParDMId = null;
            projet.CommentaireRefusCharteDM = null;

            projet.CharteValideeParDSI = false;
            projet.DateCharteValideeParDSI = null;
            projet.CharteValideeParDSIId = null;
            projet.CommentaireRefusCharteDSI = null;

            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
        }

        private static bool HasCompleteSignedCharte(Projet projet)
        {
            var hasSignedLivrable = projet.Livrables.Any(l =>
                !l.EstSupprime &&
                l.TypeLivrable == TypeLivrable.CharteProjetSignee);

            return hasSignedLivrable &&
                   projet.CharteProjet?.SignatureSponsor == true &&
                   projet.CharteProjet?.SignatureChefProjet == true;
        }

        private static List<string> BuildAnalyseBlockingItems(Projet projet, LivrableValidationResult validationLivrables)
        {
            var blocages = new List<string>();

            foreach (var livrable in validationLivrables.LivrablesManquants.Distinct())
            {
                blocages.Add($"Livrable manquant : {GetLivrableDisplayName(livrable)}");
            }

            var hasSignedLivrable = projet.Livrables.Any(l =>
                !l.EstSupprime &&
                l.TypeLivrable == TypeLivrable.CharteProjetSignee);

            if (!hasSignedLivrable && validationLivrables.LivrablesManquants.All(l => l != TypeLivrable.CharteProjetSignee))
            {
                blocages.Add("Livrable manquant : Charte projet signée");
            }

            if (projet.CharteProjet?.SignatureSponsor != true)
            {
                blocages.Add("Signature manquante : Sponsor / Directeur Métier sur la charte signée");
            }

            if (projet.CharteProjet?.SignatureChefProjet != true)
            {
                blocages.Add("Signature manquante : Chef de Projet sur la charte signée");
            }

            if (!projet.CharteValideeParDM)
            {
                blocages.Add("Validation manquante : Directeur Métier");
            }

            if (!projet.CharteValideeParDSI)
            {
                blocages.Add("Validation manquante : DSI / RSIT délégué");
            }

            return blocages;
        }

        private static string BuildAnalyseBlockingAlertHtml(IEnumerable<string> blocages)
        {
            var items = string.Join(string.Empty, blocages.Select(item => $"<li>{System.Net.WebUtility.HtmlEncode(item)}</li>"));
            return "Blocage automatique : impossible de passer en phase Planification &amp; Validation tant que les éléments suivants ne sont pas complétés :" +
                   $"<ul class=\"mb-0 mt-2\">{items}</ul>";
        }

        private static string GetLivrableDisplayName(TypeLivrable type)
        {
            return type switch
            {
                TypeLivrable.CahierCharges => "Cahier des charges",
                TypeLivrable.CahierAnalyseTechnique => "Cahier d'analyse technique",
                TypeLivrable.CharteProjet => "Charte projet",
                TypeLivrable.CharteProjetSignee => "Charte projet signée",
                TypeLivrable.NoteCadrage => "Note de cadrage",
                TypeLivrable.Wbs => "WBS",
                TypeLivrable.PlanningDetaille => "Planning détaillé",
                TypeLivrable.MatriceRaci => "Matrice RACI",
                TypeLivrable.SchemaCommunication => "Schéma de communication",
                TypeLivrable.BudgetPrevisionnel => "Budget prévisionnel",
                TypeLivrable.PvKickOff => "PV de kick-off",
                TypeLivrable.CahierTests => "Cahier de tests",
                TypeLivrable.FeuilleAnomalies => "Feuille d'anomalies",
                TypeLivrable.PvRecette => "PV de recette",
                TypeLivrable.RapportHypercare => "Rapport hypercare",
                TypeLivrable.DossierMep => "Dossier MEP",
                TypeLivrable.PvMep => "PV MEP",
                TypeLivrable.RapportCloture => "Rapport de clôture",
                TypeLivrable.PvCloture => "PV de clôture",
                TypeLivrable.DossierExploitation => "Dossier d'exploitation",
                TypeLivrable.CompteRenduReunion => "Compte-rendu de réunion",
                _ => type.ToString()
            };
        }

        private static void NormalizeCharteProjetForPersistence(CharteProjet charte)
        {
            charte.NomProjet = NormalizeRequiredText(charte.NomProjet);
            charte.NumeroProjet = NormalizeOptionalText(charte.NumeroProjet);
            charte.ObjectifProjet = NormalizeRequiredText(charte.ObjectifProjet);
            charte.AssuranceQualite = NormalizeRequiredText(charte.AssuranceQualite);
            charte.Perimetre = NormalizeRequiredText(charte.Perimetre);
            charte.ContraintesInitiales = NormalizeRequiredText(charte.ContraintesInitiales);
            charte.RisquesInitiaux = NormalizeRequiredText(charte.RisquesInitiaux);
            charte.Sponsors = NormalizeRequiredText(charte.Sponsors);
            charte.EmailChefProjet = NormalizeOptionalText(charte.EmailChefProjet);
            charte.CodeDocument = NormalizeRequiredText(charte.CodeDocument);
            charte.TypeDocument = string.IsNullOrWhiteSpace(charte.TypeDocument) ? "Charte de projet" : charte.TypeDocument.Trim();
            charte.Departement = string.IsNullOrWhiteSpace(charte.Departement) ? "SYSTEME D'INFORMATION" : charte.Departement.Trim();
            charte.DescriptionRevision = NormalizeOptionalText(charte.DescriptionRevision);
            charte.RedigePar = NormalizeOptionalText(charte.RedigePar);
            charte.VerifiePar = NormalizeOptionalText(charte.VerifiePar);
            charte.ApprouvePar = NormalizeOptionalText(charte.ApprouvePar);
        }

        private static string NormalizeRequiredText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        // GET: Écran de validation de charte pour DM et DSI
        [Authorize]
        public async Task<IActionResult> ValidationsProjet(int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var canPortfolioAccess = await HasPortfolioGovernanceAccessAsync();
            var hasDmScope = await CurrentUserHasPermissionAsync("Projet", "ValidationsProjet");
            var canValidateAsDsi = await CanValidateCharteAsDsiAsync(userId);

            if (!hasDmScope && !canValidateAsDsi && !canPortfolioAccess)
            {
                return Forbid();
            }
            
            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Where(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification && 
                           (!p.CharteValideeParDM || !p.CharteValideeParDSI));
            
            if (hasDmScope && !canValidateAsDsi && !canPortfolioAccess)
            {
                var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
                if (currentUserDirectionId.HasValue)
                {
                    query = query.Where(p => p.SponsorId == userId || p.DirectionId == currentUserDirectionId.Value);
                }
                else
                {
                    query = query.Where(p => p.SponsorId == userId);
                }
            }
            
            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            
            query = query.OrderByDescending(p => p.DateCreation);
            
            var pagedResult = await query.ToPagedResultAsync(page, pageSize);
            
            ViewBag.PageNumber = pagedResult.PageNumber;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.PageSize = pagedResult.PageSize;
            ViewBag.CanValidateAsDsi = canValidateAsDsi;
            
            var projets = pagedResult.Items;
            
            return View(projets);
        }

        // POST: Valider Charte par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
        public async Task<IActionResult> ValiderCharteDM(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Sponsor)
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (projet == null)
                return NotFound();
            
            var userId = User.GetUserIdOrThrow();

            if (!await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();
            
            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La charte ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            if (!HasCompleteSignedCharte(projet))
            {
                TempData["Error"] = "La charte signée complète doit être déposée avant la validation DM.";
                return RedirectToAction(nameof(ValidationsProjet));
            }
            
            projet.CharteValideeParDM = true;
            projet.DateCharteValideeParDM = DateTime.Now;
            projet.CharteValideeParDMId = userId;
            projet.CommentaireRefusCharteDM = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            
            // Si DM et DSI ont validé, la charte est complètement validée
            if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
            {
                projet.CharteValidee = true;
                projet.DateCharteValidee = DateTime.Now;
            }
            
            await _db.SaveChangesAsync();
            
            await _auditService.LogActionAsync("VALIDATION_CHARTE_DM", "Projet", projet.Id);
            
            TempData["Success"] = "Charte validée par le Directeur Métier.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // POST: Rejeter Charte par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
        public async Task<IActionResult> RejeterCharteDM(Guid id, string commentaire)
        {
            var projet = await _db.Projets
                .Include(p => p.Sponsor)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (projet == null)
                return NotFound();
            
            var userId = User.GetUserIdOrThrow();

            if (!await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();
            
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la charte.";
                return RedirectToAction(nameof(ValidationsProjet));
            }
            
            projet.CharteValideeParDM = false;
            projet.DateCharteValideeParDM = null;
            projet.CharteValideeParDMId = null;
            projet.CommentaireRefusCharteDM = commentaire.Trim();
            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            
            await _db.SaveChangesAsync();
            
            await _auditService.LogActionAsync("REJET_CHARTE_DM", "Projet", projet.Id,
                new { Commentaire = commentaire });
            
            TempData["Success"] = "Charte rejetée par le Directeur Métier.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // POST: Valider Charte par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        public async Task<IActionResult> ValiderCharteDSI(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (projet == null)
                return NotFound();
            
            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La charte ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(ValidationsProjet));
            }
            
            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            if (!projet.CharteValideeParDM)
            {
                TempData["Error"] = "La charte doit d'abord être validée par le Directeur Métier.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            if (!HasCompleteSignedCharte(projet))
            {
                TempData["Error"] = "La charte signée complète doit être déposée avant la validation DSI.";
                return RedirectToAction(nameof(ValidationsProjet));
            }
             
            projet.CharteValideeParDSI = true;
            projet.DateCharteValideeParDSI = DateTime.Now;
            projet.CharteValideeParDSIId = userId;
            projet.CommentaireRefusCharteDSI = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            
            // Si DM et DSI ont validé, la charte est complètement validée
            if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
            {
                projet.CharteValidee = true;
                projet.DateCharteValidee = DateTime.Now;
            }
            
            await _db.SaveChangesAsync();
            
            await _auditService.LogActionAsync("VALIDATION_CHARTE_DSI", "Projet", projet.Id);
            
            TempData["Success"] = "Charte validée par la DSI.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // POST: Rejeter Charte par DSI
        // POST: Rejeter Charte par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        public async Task<IActionResult> RejeterCharteDSI(Guid id, string commentaire)
        {
            var projet = await _db.Projets.FindAsync(id);
            
            if (projet == null)
                return NotFound();
            
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la charte.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }
             
            projet.CharteValideeParDSI = false;
            projet.DateCharteValideeParDSI = null;
            projet.CharteValideeParDSIId = null;
            projet.CommentaireRefusCharteDSI = commentaire.Trim();
            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            
            await _db.SaveChangesAsync();
            
            await _auditService.LogActionAsync("REJET_CHARTE_DSI", "Projet", projet.Id,
                new { Commentaire = commentaire });
            
            TempData["Success"] = "Charte rejetée par la DSI.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // ══════════════════════════════════════════════════════════════════════
        // UAT — Gestion des cas de test
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterCasTest(Guid projetId, string titre, string description,
            string resultAttendu, PrioriteAnomalie priorite, bool estObligatoire, Guid? campagneId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageUatAsync(projet)) return Forbid();

            if (string.IsNullOrWhiteSpace(titre))
            {
                TempData["Error"] = "Le titre du cas de test est obligatoire.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
            }

            var reference = await _uatValidation.GenererReferenceCasTestAsync(projet);

            var casTest = new CasTestProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                CampagneTestProjetId = campagneId,
                Reference = reference,
                Titre = titre,
                Description = description ?? string.Empty,
                ResultatAttendu = resultAttendu ?? string.Empty,
                Priorite = priorite,
                EstObligatoire = estObligatoire,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                DateCreation = DateTime.Now
            };

            _db.CasTestsProjets.Add(casTest);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Cas de test {reference} ajouté.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ExecuterCasTest(Guid projetId, Guid casTestId,
            StatutExecutionTest statut, string? commentaire, Guid? campagneId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            var casTest = await _db.CasTestsProjets.FindAsync(casTestId);
            if (casTest == null || casTest.ProjetId != projetId) return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanEditUat && !ui.CanValidateRecette && !ui.HasDsiGovernanceAccess)
            {
                return Forbid();
            }

            var execution = new ExecutionTestProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                CasTestProjetId = casTestId,
                CampagneTestProjetId = campagneId ?? casTest.CampagneTestProjetId,
                Statut = statut,
                Commentaire = commentaire ?? string.Empty,
                DateExecution = DateTime.Now,
                ExecuteParId = userId,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                DateCreation = DateTime.Now
            };

            _db.ExecutionsTestsProjets.Add(execution);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Résultat enregistré : {statut}.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterCampagneTest(Guid projetId, string nom, string? descriptionCampagne,
            Environnement environnement, DateTime dateLancement)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageUatAsync(projet)) return Forbid();

            if (string.IsNullOrWhiteSpace(nom))
            {
                TempData["Error"] = "Le nom de la campagne est obligatoire.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
            }

            var campagne = new CampagneTestProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Nom = nom,
                Description = descriptionCampagne ?? string.Empty,
                Environnement = environnement,
                Statut = StatutCampagneTest.Brouillon,
                DateLancement = dateLancement,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                DateCreation = DateTime.Now
            };

            _db.CampagnesTestsProjets.Add(campagne);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Campagne \"{nom}\" créée.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerCasTest(Guid projetId, Guid casTestId)
        {
            var casTest = await _db.CasTestsProjets.FindAsync(casTestId);
            if (casTest == null || casTest.ProjetId != projetId) return NotFound();

            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();
            if (!await CanManageUatAsync(projet)) return Forbid();

            casTest.EstSupprime = true;
            casTest.ModifiePar = _currentUserService.Matricule ?? "SYSTEM";
            casTest.DateModification = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Cas de test supprimé.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Collaboration Teams
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ConfigurerCollaboration(Guid projetId,
            ModeCollaborationProjet mode,
            string? nomEquipeTeams, string? teamId, string? teamUrl,
            string? nomCanalTeams, string? channelId, string? channelUrl,
            string? nomPlanPlanner, string? planId, string? planUrl, string? nomBucketPlanner, string? bucketId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageCollaborationAsync(projet)) return Forbid();

            var request = new CollaborationProjetConfigurationRequest
            {
                Mode = mode,
                NomEquipeTeams = nomEquipeTeams ?? $"{projet.CodeProjet} — {projet.Titre}",
                TeamId = teamId,
                TeamUrl = teamUrl,
                NomCanalTeams = nomCanalTeams ?? "Général",
                ChannelId = channelId,
                ChannelUrl = channelUrl,
                NomPlanPlanner = nomPlanPlanner ?? $"Planification {projet.CodeProjet}",
                PlanId = planId,
                PlanUrl = planUrl,
                NomBucketPlanner = nomBucketPlanner,
                BucketId = bucketId
            };

            try
            {
                await _collaboration.ConfigurerAsync(projetId, request, _currentUserService.Matricule);
                TempData["Success"] = "Collaboration configurée avec succès.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la configuration : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "collaboration" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SynchroniserCollaboration(Guid projetId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageCollaborationAsync(projet)) return Forbid();

            try
            {
                var result = await _collaboration.SynchroniserAsync(projetId, _currentUserService.Matricule);
                if (result.Success)
                    TempData["Success"] = $"Synchronisation effectuée : {result.NombreMembres} membres, {result.NombreTaches} tâches.";
                else
                    TempData["Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la synchronisation : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "collaboration" });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Dossier de Signature Électronique
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> InitialiserDossierSignature(Guid projetId,
            FournisseurSignatureElectronique fournisseur)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature) return Forbid();

            try
            {
                var result = await _electronicSignature.InitialiserCharteAsync(projetId, fournisseur, _currentUserService.Matricule);
                if (result != null)
                    TempData["Success"] = "Dossier de signature initialisé.";
                else
                    TempData["Error"] = "Impossible d'initialiser le dossier.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "planification" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnvoyerDossierSignature(Guid projetId, Guid dossierId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature) return Forbid();

            try
            {
                var result = await _electronicSignature.EnvoyerDossierAsync(dossierId, _currentUserService.Matricule);
                if (result.Success)
                    TempData["Success"] = "Dossier envoyé pour signature.";
                else
                    TempData["Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "planification" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DecisionSignataire(Guid projetId, Guid dossierId, Guid signataireId, bool approuver)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null) return NotFound();

            var dossier = await _db.DossiersSignatureProjets
                .Include(d => d.Signataires)
                .FirstOrDefaultAsync(d => d.Id == dossierId && d.ProjetId == projetId && !d.EstSupprime);
            if (dossier == null) return NotFound();

            var signataire = dossier.Signataires.FirstOrDefault(s => s.Id == signataireId);
            if (signataire == null) return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            var canManage = ui.CanManageDossierSignature;
            var canSponsorDecision = await CanValidateCharteAsDirecteurMetierAsync(projet, userId) &&
                                     projet.SponsorId == userId &&
                                     signataire.Role == RoleSignataireProjet.Sponsor;
            var isCurrentSignatory = signataire.UtilisateurId == userId;
            if (!canManage && !canSponsorDecision && !isCurrentSignatory)
            {
                return Forbid();
            }

            try
            {
                var result = await _electronicSignature.EnregistrerDecisionSignataireAsync(
                    dossierId, signataireId, approuver, _currentUserService.Matricule);

                TempData[result.Success ? "Success" : "Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "planification" });
        }
    }
}

