using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        private readonly INotificationService _notificationService;
        private readonly ILivrableValidationService _livrableValidationService;
        private readonly IRAGCalculationService _ragCalculationService;
        private readonly ICacheService _cacheService;

        public ProjetController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IAuditService auditService,
            IPdfService pdfService,
            IExcelService excelService,
            IWordService wordService,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            ILivrableValidationService livrableValidationService,
            IRAGCalculationService ragCalculationService,
            ICacheService cacheService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _auditService = auditService;
            _pdfService = pdfService;
            _excelService = excelService;
            _wordService = wordService;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
            _livrableValidationService = livrableValidationService;
            _ragCalculationService = ragCalculationService;
            _cacheService = cacheService;
        }

        // GET: Liste des projets
        public async Task<IActionResult> Index(Guid? directionId, Guid? chefProjetId, PhaseProjet? phase, StatutProjet? statut, EtatProjet? etat, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();

            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet);

            // Filtrage selon les rôles (DSI, AdminIT et ResponsableSolutionsIT voient tout)
            if (!User.IsInRole("DSI") && !User.IsInRole("AdminIT") && !User.IsInRole("ResponsableSolutionsIT"))
            {
                var isChefProjet = User.IsInRole("ChefDeProjet");
                var isDirecteurMetier = User.IsInRole("DirecteurMetier");
                
                if (isChefProjet && isDirecteurMetier)
                {
                    // L'utilisateur a les deux rôles : voir les projets où il est chef OU sponsor/direction
                    var user = await _db.Utilisateurs
                        .Include(u => u.Direction)
                        .FirstOrDefaultAsync(u => u.Id == userId);
                    
                    if (user?.DirectionId.HasValue == true)
                    {
                        query = query.Where(p => p.ChefProjetId == userId || p.SponsorId == userId || p.DirectionId == user.DirectionId);
                    }
                    else
                    {
                        query = query.Where(p => p.ChefProjetId == userId || p.SponsorId == userId);
                    }
                }
                else if (isChefProjet)
                {
                    query = query.Where(p => p.ChefProjetId == userId);
                }
                else if (isDirecteurMetier)
                {
                    // Récupérer la direction du Directeur Métier
                    var user = await _db.Utilisateurs
                        .Include(u => u.Direction)
                        .FirstOrDefaultAsync(u => u.Id == userId);
                    
                    if (user?.DirectionId.HasValue == true)
                    {
                        // Voir les projets où il est sponsor OU les projets de sa direction
                        query = query.Where(p => p.SponsorId == userId || p.DirectionId == user.DirectionId);
                    }
                    else
                    {
                        // Si pas de direction, voir uniquement ses projets
                        query = query.Where(p => p.SponsorId == userId);
                    }
                }
                else
                {
                    // Si aucun rôle ne correspond, ne rien afficher
                    query = query.Where(p => false);
                }
            }

            // Filtres pour DSI/AdminIT/ResponsableSolutionsIT
            if (User.IsInRole("DSI") || User.IsInRole("AdminIT") || User.IsInRole("ResponsableSolutionsIT"))
            {
                if (directionId.HasValue)
                {
                    query = query.Where(p => p.DirectionId == directionId.Value);
                }
                
                if (chefProjetId.HasValue)
                {
                    query = query.Where(p => p.ChefProjetId == chefProjetId.Value);
                }
                
                if (phase.HasValue)
                {
                    query = query.Where(p => p.PhaseActuelle == phase.Value);
                }
                
                if (statut.HasValue)
                {
                    query = query.Where(p => p.StatutProjet == statut.Value);
                }
                
                if (etat.HasValue)
                {
                    query = query.Where(p => p.EtatProjet == etat.Value);
                }
            }

            // Pour DSI/AdminIT/ResponsableSolutionsIT, charger les données pour les filtres (avec cache)
            if (User.IsInRole("DSI") || User.IsInRole("AdminIT") || User.IsInRole("ResponsableSolutionsIT"))
            {
                // Utiliser le cache pour Directions
                var directionsCached = await _cacheService.GetOrSetAsync(
                    "directions_active",
                    async () => await _db.Directions
                    .Where(d => !d.EstSupprime && d.EstActive)
                    .OrderBy(d => d.Libelle)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Libelle,
                            Selected = false
                        })
                        .ToListAsync(),
                    TimeSpan.FromMinutes(30));

                // Marquer la sélection
                ViewBag.Directions = directionsCached?.Select(d => new SelectListItem
                {
                    Value = d.Value,
                    Text = d.Text,
                    Selected = directionId.HasValue && d.Value == directionId.Value.ToString()
                }).ToList() ?? new List<SelectListItem>();

                // Utiliser le cache pour Chefs de Projet
                var chefsCached = await _cacheService.GetOrSetAsync(
                    "chefs_projet",
                    async () => await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id.ToString(),
                        Text = $"{u.Nom} {u.Prenoms}",
                            Selected = false
                        })
                        .ToListAsync(),
                    TimeSpan.FromMinutes(15));

                // Marquer la sélection
                ViewBag.ChefsProjet = chefsCached?.Select(c => new SelectListItem
                {
                    Value = c.Value,
                    Text = c.Text,
                    Selected = chefProjetId.HasValue && c.Value == chefProjetId.Value.ToString()
                }).ToList() ?? new List<SelectListItem>();

                ViewBag.Phases = Enum.GetValues(typeof(PhaseProjet))
                    .Cast<PhaseProjet>()
                    .Select(p => new SelectListItem
                    {
                        Value = ((int)p).ToString(),
                        Text = p.ToString(),
                        Selected = phase == p
                    })
                    .ToList();

                ViewBag.Statuts = Enum.GetValues(typeof(StatutProjet))
                    .Cast<StatutProjet>()
                    .Select(s => new SelectListItem
                    {
                        Value = ((int)s).ToString(),
                        Text = s.ToString(),
                        Selected = statut == s
                    })
                    .ToList();

                ViewBag.Etats = Enum.GetValues(typeof(EtatProjet))
                    .Cast<EtatProjet>()
                    .Select(e => new SelectListItem
                    {
                        Value = ((int)e).ToString(),
                        Text = e.ToString(),
                        Selected = false
                    })
                    .ToList();

                ViewBag.SelectedDirectionId = directionId;
                ViewBag.SelectedChefProjetId = chefProjetId;
                ViewBag.SelectedPhase = phase;
                ViewBag.SelectedStatut = statut;
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
            ViewBag.PageSize = pagedResult.PageSize;

            var projets = pagedResult.Items;

            // Pour DSI/AdminIT/ResponsableSolutionsIT, charger les données pour les filtres (avec cache)
            if (User.IsInRole("DSI") || User.IsInRole("AdminIT") || User.IsInRole("ResponsableSolutionsIT"))
            {
                // Utiliser le cache pour Directions
                var directionsCached = await _cacheService.GetOrSetAsync(
                    "directions_active",
                    async () => await _db.Directions
                    .Where(d => !d.EstSupprime && d.EstActive)
                    .OrderBy(d => d.Libelle)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Libelle,
                            Selected = false
                        })
                        .ToListAsync(),
                    TimeSpan.FromMinutes(30));

                // Marquer la sélection
                ViewBag.Directions = directionsCached?.Select(d => new SelectListItem
                {
                    Value = d.Value,
                    Text = d.Text,
                    Selected = directionId.HasValue && d.Value == directionId.Value.ToString()
                }).ToList() ?? new List<SelectListItem>();

                // Utiliser le cache pour Chefs de Projet
                var chefsCached = await _cacheService.GetOrSetAsync(
                    "chefs_projet",
                    async () => await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id.ToString(),
                        Text = $"{u.Nom} {u.Prenoms}",
                            Selected = false
                        })
                        .ToListAsync(),
                    TimeSpan.FromMinutes(15));

                // Marquer la sélection
                ViewBag.ChefsProjet = chefsCached?.Select(c => new SelectListItem
                {
                    Value = c.Value,
                    Text = c.Text,
                    Selected = chefProjetId.HasValue && c.Value == chefProjetId.Value.ToString()
                }).ToList() ?? new List<SelectListItem>();

                ViewBag.Phases = Enum.GetValues(typeof(PhaseProjet))
                    .Cast<PhaseProjet>()
                    .Select(p => new SelectListItem
                    {
                        Value = ((int)p).ToString(),
                        Text = p.ToString(),
                        Selected = phase == p
                    })
                    .ToList();

                ViewBag.Statuts = Enum.GetValues(typeof(StatutProjet))
                    .Cast<StatutProjet>()
                    .Select(s => new SelectListItem
                    {
                        Value = ((int)s).ToString(),
                        Text = s.ToString(),
                        Selected = statut == s
                    })
                    .ToList();

                ViewBag.SelectedDirectionId = directionId;
                ViewBag.SelectedChefProjetId = chefProjetId;
                ViewBag.SelectedPhase = phase;
                ViewBag.SelectedStatut = statut;
            }

            // Pour le Directeur Métier, indiquer quels projets sont en readonly
            if (User.IsInRole("DirecteurMetier"))
            {
                ViewBag.ReadOnlyProjets = projets
                    .Where(p => p.SponsorId != userId)
                    .Select(p => p.Id)
                    .ToHashSet();
            }

            return View(projets);
        }

        // GET: Historique des projets pour Directeur Métier
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> HistoriqueDM()
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.HistoriquePhases)
                    .ThenInclude(h => h.ModifieParUtilisateur);
            
            // Si ce n'est pas DSI/AdminIT/ResponsableSolutionsIT, filtrer par SponsorId ou DirectionId
            if (!User.IsInRole("DSI") && !User.IsInRole("AdminIT") && !User.IsInRole("ResponsableSolutionsIT"))
            {
                if (User.IsInRole("DirecteurMetier"))
                {
                    // Récupérer la direction du Directeur Métier
                    var user = await _db.Utilisateurs
                        .Include(u => u.Direction)
                        .FirstOrDefaultAsync(u => u.Id == userId);
                    
                    if (user?.DirectionId.HasValue == true)
                    {
                        // Voir les projets où il est sponsor OU les projets de sa direction
                        query = query.Where(p => p.SponsorId == userId || p.DirectionId == user.DirectionId);
                    }
                    else
                    {
                        // Si pas de direction, voir uniquement ses projets
                        query = query.Where(p => p.SponsorId == userId);
                    }
                }
                else
                {
                    query = query.Where(p => p.SponsorId == userId);
                }
            }
            
            // Récupérer tous les projets
            var projets = await query
                .OrderByDescending(p => p.DateCreation)
                .ToListAsync();

            // Optimisation : Charger toutes les données en une seule fois pour éviter N+1
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
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> Portefeuille()
        {
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
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> UpdatePortefeuille(Guid id, string ObjectifStrategiqueGlobal, string AvantagesAttendus, string RisquesEtMitigations)
        {
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
            var userRole = User.GetUserRole();
            
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
                .Include(p => p.HistoriquePhases)
                    .ThenInclude(h => h.ModifieParUtilisateur)
                .Include(p => p.DemandesCloture)
                .Include(p => p.DernierCommentaireTechniquePar)
                .Include(p => p.CharteValideeParDMUtilisateur)
                .Include(p => p.CharteValideeParDSIUtilisateur)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            // Vérifier les permissions
            if (User.IsInRole("DirecteurMetier"))
            {
                // Récupérer la direction du Directeur Métier
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                // Peut voir si : il est sponsor OU le projet est de sa direction
                bool canView = projet.SponsorId == userId;
                if (!canView && user?.DirectionId.HasValue == true)
                {
                    canView = projet.DirectionId == user.DirectionId;
                }
                
                if (!canView)
                {
                    return Forbid();
                }
                
                // Indiquer si le projet est en readonly (où il n'est pas sponsor)
                ViewBag.IsReadOnly = projet.SponsorId != userId;
            }
            else if (User.IsInRole("ChefDeProjet"))
            {
                // Chef de Projet ne peut voir que les projets où il est chef de projet
                if (projet.ChefProjetId != userId)
                {
                    return Forbid();
                }
            }
            else if (User.IsInRole("ResponsableSolutionsIT"))
            {
                // ResponsableSolutionsIT peut voir tous les projets (chef de projet par défaut sur tous les projets)
                // Pas de restriction
            }
            // DSI et AdminIT peuvent voir tous les projets (pas de restriction)

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

            // Pour DSI/AdminIT/ResponsableSolutionsIT : charger la liste des chefs de projet disponibles pour modification
            if (User.IsInRole("DSI") || User.IsInRole("AdminIT") || User.IsInRole("ResponsableSolutionsIT"))
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
                    .Select(d => d.Delegue)
                    .Where(u => u != null && !u.EstSupprime)
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

            // Pour ChefProjet : vérifier si c'est le premier accès et logger la prise en charge
            if (User.IsInRole("ChefDeProjet") && projet.ChefProjetId == userId)
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
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> UpdateChefProjet(Guid id, Guid? chefProjetId)
        {
            var projet = await _db.Projets
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> ValiderPhaseAnalyse(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            if (!CanActAsChefProjet(projet, userId, userRole))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La phase Analyse ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Vérifier que la charte est validée par DM et DSI
            if (!projet.CharteValideeParDM || !projet.CharteValideeParDSI)
            {
                TempData["Error"] = "La charte projet doit être validée par le Directeur Métier et la DSI avant de pouvoir valider la phase Analyse.";
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }

            // ⛔ VALIDATION DES LIVRABLES OBLIGATOIRES (PRD - Blocage automatique)
            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.PlanificationValidation);
            
            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> CharteProjet(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

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

            return View(charte);
        }

        // POST: Sauvegarder la charte de projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> SauvegarderCharteProjet(Guid id, CharteProjet charte, 
            List<JalonCharte>? Jalons = null, List<PartiePrenanteCharte>? PartiesPrenantes = null)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

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

            await _auditService.LogActionAsync("SAUVEGARDE_CHARTE_PROJET", "CharteProjet", charteExistante?.Id ?? charte.Id);

            TempData["Success"] = "Charte de projet sauvegardée avec succès.";
            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        // POST: Générer Word Charte (version complète)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> GenererCharteCompletWord(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                    .ThenInclude(c => c.Demandeur)
                .Include(p => p.CharteProjet)
                    .ThenInclude(c => c.ChefProjet)
                .Include(p => p.CharteProjet)
                    .ThenInclude(c => c.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                .Include(p => p.CharteProjet)
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
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
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> GenererPortefeuilleExcel()
        {
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

        // POST: Générer PDF Charte
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
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

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            try
            {
                var pdfBytes = await _pdfService.GenerateCharteProjetPdfAsync(projet);

                // Sauvegarder le PDF comme livrable
                var fileName = $"CharteProjet_{projet.CodeProjet}_{DateTime.Now:yyyyMMdd}.pdf";
                var filePath = Path.Combine("projets", projet.CodeProjet, "analyse", fileName);
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", filePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
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

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du PDF: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }
        }

        // GET: Afficher/Éditer la fiche projet CIT
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT,DirecteurMetier")]
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> SauvegarderFicheProjet(Guid id, FicheProjet fiche)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ficheExistante = await _db.FicheProjets
                .FirstOrDefaultAsync(f => f.ProjetId == id && !f.EstSupprime);

            var userId = User.GetUserIdOrThrow();

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
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ValiderPlanifDM(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.SponsorId != userId)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.PlanificationValidation)
            {
                TempData["Error"] = "La planification ne peut être validée qu'en phase Planification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PLANIF_DM", "Projet", projet.Id);

            TempData["Success"] = "Planification validée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // POST: Valider Planification par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> ValiderPlanifDSI(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

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
                ModifieParId = User.GetUserIdOrThrow(),
                Commentaire = "Passage en phase Exécution après validation planification DSI",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> PretUAT(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.ExecutionSuivi)
            {
                TempData["Error"] = "Le projet doit être en phase Exécution.";
                return RedirectToAction(nameof(Details), new { id });
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
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ValiderRecette(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.SponsorId != userId)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.UatMep)
            {
                TempData["Error"] = "Le projet doit être en phase UAT & MEP.";
                return RedirectToAction(nameof(Details), new { id });
            }

            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;
            projet.RecetteValideeParId = userId;
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> FinUAT(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
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

            // ⛔ VALIDATION DES LIVRABLES OBLIGATOIRES (PRD - Blocage automatique)
            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.ClotureLeconsApprises);
            
            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> DemanderCloture(Guid id, string? commentaire, DateTime? dateSouhaiteeCloture)
        {
            var projet = await _db.Projets
                .Include(p => p.DemandeProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Seul le Chef de Projet ou ResponsableSolutionsIT peut initier la demande de clôture
            // Il doit vérifier que les livrables, bilans et leçons apprises sont complétés
            if (!CanActAsChefProjet(projet, userId, userRole))
            {
                TempData["Error"] = "Seul le Chef de Projet ou le Responsable Solutions IT peut initier une demande de clôture.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Vérifier que le projet est en phase UAT/MEP et que la recette est validée
            if (projet.PhaseActuelle != PhaseProjet.UatMep || !projet.RecetteValidee)
            {
                TempData["Error"] = "Le projet doit avoir validé la recette avant de demander la clôture.";
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

            if (string.IsNullOrWhiteSpace(projet.BilanCloture) || string.IsNullOrWhiteSpace(projet.LeconsApprises))
            {
                TempData["Error"] = "Veuillez renseigner le bilan de clôture et les leçons apprises avant de soumettre la demande.";
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
                CommentaireDemandeur = commentaire ?? string.Empty, // Commentaire du Chef de Projet
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public IActionResult UploadLivrableForm(Guid projetId, PhaseProjet phase)
        {
            ViewBag.ProjetId = projetId;
            ViewBag.Phase = phase;
            ViewBag.TypesLivrables = Enum.GetValues<TypeLivrable>();
            return PartialView("_UploadLivrableModal");
        }

        // POST: Upload Livrable
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> UploadLivrable(Guid projetId, PhaseProjet phase, TypeLivrable typeLivrable, IFormFile fichier, string? commentaire, string? version)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

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
                Commentaire = commentaire,
                Version = version,
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> AjouterRisque(Guid projetId, RisqueProjet risque)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            if (ModelState.IsValid)
            {
                risque.Id = Guid.NewGuid();
                risque.ProjetId = projetId;
                risque.DateCreationRisque = DateTime.Now;
                risque.Statut = StatutRisque.Identifie;
                risque.DateCreation = DateTime.Now;
                risque.CreePar = _currentUserService.Matricule;

                _db.RisquesProjets.Add(risque);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("AJOUT_RISQUE", "RisqueProjet", risque.Id);

                TempData["Success"] = "Risque ajouté.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "synthese" });
            }

            TempData["Error"] = "Erreur lors de l'ajout du risque.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "synthese" });
        }

        // POST: Mettre à jour un risque
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> UpdateRisque(Guid id, Guid risqueId, string? description, ProbabiliteRisque? probabilite, ImpactRisque? impact, StatutRisque? statut, string? planMitigation, string? responsable, DateTime? echeance)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
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
                risque.PlanMitigation = planMitigation;

            if (!string.IsNullOrWhiteSpace(responsable))
                risque.Responsable = responsable;

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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> AjouterAnomalie(Guid projetId, AnomalieProjet anomalie)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            if (ModelState.IsValid)
            {
                anomalie.Id = Guid.NewGuid();
                anomalie.ProjetId = projetId;
                anomalie.Reference = $"ANOM-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
                anomalie.DateCreationAnomalie = DateTime.Now;
                anomalie.Statut = StatutAnomalie.Ouverte;
                anomalie.RapporteePar = User.FindFirstValue("Nom") + " " + User.FindFirstValue("Prenoms");
                anomalie.DateCreation = DateTime.Now;
                anomalie.CreePar = _currentUserService.Matricule;

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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> UpdateAvancement(Guid id, int pourcentageAvancement, EtatProjet? etatProjet)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            if (pourcentageAvancement < 0 || pourcentageAvancement > 100)
            {
                TempData["Error"] = "Le pourcentage doit être entre 0 et 100.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienPourcentage = projet.PourcentageAvancement;
            projet.PourcentageAvancement = pourcentageAvancement;

            // Mise à jour de l'état
            if (etatProjet.HasValue)
            {
                projet.EtatProjet = etatProjet.Value;
            }
            else
            {
                // Mise à jour automatique selon l'avancement si non fourni
                if (pourcentageAvancement < 50)
                    projet.EtatProjet = EtatProjet.Vert;
                else if (pourcentageAvancement < 80)
                    projet.EtatProjet = EtatProjet.Orange;
                else
                    projet.EtatProjet = EtatProjet.Rouge;
            }

            // Calcul automatique du RAG
            projet.IndicateurRAG = await _ragCalculationService.CalculerRAGAsync(projet);
            projet.DateDernierCalculRAG = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_AVANCEMENT", "Projet", projet.Id,
                new { PourcentageAvancement = ancienPourcentage },
                new { PourcentageAvancement = pourcentageAvancement, IndicateurRAG = projet.IndicateurRAG });

            TempData["Success"] = "Avancement mis à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // GET: Gestion des charges du projet
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> Charges(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Membres)
                .Include(p => p.Charges)
                    .ThenInclude(c => c.Ressource)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId && !User.IsInRole("DSI") && !User.IsInRole("AdminIT"))
                return Forbid();

            // Récupérer les utilisateurs qui travaillent sur ce projet
            // (Chef de projet + membres identifiés par email si possible)
            var ressources = new List<Utilisateur>();
            
            // Ajouter le chef de projet s'il existe
            if (projet.ChefProjetId.HasValue)
            {
                var chefProjet = await _db.Utilisateurs.FindAsync(projet.ChefProjetId.Value);
                if (chefProjet != null && !chefProjet.EstSupprime)
                    ressources.Add(chefProjet);
            }

            // Ajouter les utilisateurs identifiés par email depuis les membres du projet
            var emailsMembres = projet.Membres
                .Where(m => !m.EstSupprime && !string.IsNullOrEmpty(m.Email))
                .Select(m => m.Email)
                .Distinct()
                .ToList();

            if (emailsMembres.Any())
            {
                var utilisateursParEmail = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime && emailsMembres.Contains(u.Email))
                    .ToListAsync();
                
                foreach (var user in utilisateursParEmail)
                {
                    if (!ressources.Any(r => r.Id == user.Id))
                        ressources.Add(user);
                }
            }

            // Calculer les semaines à afficher (4 semaines à venir + 2 semaines passées)
            var semaines = new List<DateTime>();
            var aujourdhui = DateTime.Now;
            var debutSemaine = aujourdhui.AddDays(-(int)aujourdhui.DayOfWeek); // Lundi de cette semaine

            // 2 semaines passées
            for (int i = -2; i < 0; i++)
            {
                semaines.Add(debutSemaine.AddDays(i * 7));
            }
            // 4 semaines à venir
            for (int i = 0; i < 4; i++)
            {
                semaines.Add(debutSemaine.AddDays(i * 7));
            }

            ViewBag.Semaines = semaines;
            ViewBag.Ressources = ressources;
            ViewBag.Projet = projet;

            return View(projet);
        }

        // POST: Saisir charge réelle
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> SaisirCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut, decimal chargeReelle, string? commentaire)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId && !User.IsInRole("DSI") && !User.IsInRole("AdminIT"))
                return Forbid();

            // Normaliser la date au lundi de la semaine
            var lundiSemaine = semaineDebut.AddDays(-(int)semaineDebut.DayOfWeek);

            // Chercher une charge existante ou en créer une nouvelle
            var charge = await _db.ChargesProjets
                .FirstOrDefaultAsync(c => c.ProjetId == projetId && 
                                         c.RessourceId == ressourceId && 
                                         c.SemaineDebut.Date == lundiSemaine.Date);

            if (charge == null)
            {
                // Créer une nouvelle charge
                charge = new ChargeProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    RessourceId = ressourceId,
                    SemaineDebut = lundiSemaine,
                    ChargePrevisionnelle = 0, // À définir séparément
                    ChargeReelle = chargeReelle,
                    DateSaisieChargeReelle = DateTime.Now,
                    SaisieParId = userId,
                    Commentaire = commentaire,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };
                _db.ChargesProjets.Add(charge);
            }
            else
            {
                // Mettre à jour la charge existante
                charge.ChargeReelle = chargeReelle;
                charge.DateSaisieChargeReelle = DateTime.Now;
                charge.SaisieParId = userId;
                charge.Commentaire = commentaire;
                charge.DateModification = DateTime.Now;
                charge.ModifiePar = _currentUserService.Matricule;
            }

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SAISIE_CHARGE", "ChargeProjet", charge.Id,
                new { ProjetId = projetId, RessourceId = ressourceId, Semaine = lundiSemaine },
                new { ChargeReelle = chargeReelle });

            TempData["Success"] = "Charge réelle enregistrée avec succès.";
            return RedirectToAction(nameof(Charges), new { id = projetId });
        }

        // POST: Mettre à jour Bilan
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> UpdateBilan(Guid id, string? bilanCloture, string? leconsApprises)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            projet.BilanCloture = bilanCloture;
            projet.LeconsApprises = leconsApprises;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_BILAN", "Projet", projet.Id);

            TempData["Success"] = "Bilan et leçons apprises mis à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }

        // POST: Ajouter Membre
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> AjouterMembre(Guid projetId, Guid utilisateurId, string roleDansProjet)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
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
                DirectionLibelle = utilisateur.Direction?.Libelle,
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> RetirerMembre(Guid id, Guid membreId)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
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
        [Authorize(Roles = "ChefDeProjet,DSI,AdminIT")]
        public async Task<IActionResult> UpdateLivrable(Guid id, Guid livrableId, string? commentaire, string? version)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (projet.ChefProjetId != userId)
                return Forbid();

            var livrable = await _db.LivrablesProjets.FindAsync(livrableId);
            if (livrable == null || livrable.ProjetId != id)
                return NotFound();

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

        // POST: Valider Clôture - Demandeur
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> ValiderClotureDemandeur(Guid demandeClotureId)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                    .ThenInclude(p => p.DemandeProjet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (demande.Projet.DemandeProjet.DemandeurId != userId)
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

        // POST: Valider Clôture - Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ValiderClotureDM(Guid demandeClotureId)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le sponsor OU un Directeur Métier de la direction du projet
            var peutValider = false;
            if (demande.Projet.SponsorId == userId || userRole == "DSI" || userRole == "AdminIT")
            {
                peutValider = true;
            }
            else if (User.IsInRole("DirecteurMetier"))
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user?.DirectionId.HasValue == true && demande.Projet.DirectionId == user.DirectionId)
                {
                    peutValider = true;
                }
            }
            
            if (!peutValider)
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
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> RejeterClotureDM(Guid demandeClotureId, string commentaire)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le sponsor OU un Directeur Métier de la direction du projet
            var peutRejeter = false;
            if (demande.Projet.SponsorId == userId || userRole == "DSI" || userRole == "AdminIT")
            {
                peutRejeter = true;
            }
            else if (User.IsInRole("DirecteurMetier"))
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user?.DirectionId.HasValue == true && demande.Projet.DirectionId == user.DirectionId)
                {
                    peutRejeter = true;
                }
            }
            
            if (!peutRejeter)
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
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> ListeValidationClotureDSI()
        {
            var demandes = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                    .ThenInclude(p => p.Direction)
                .Include(d => d.Projet)
                    .ThenInclude(p => p.DemandeProjet)
                        .ThenInclude(dp => dp.Demandeur)
                .Include(d => d.DemandePar)
                .Where(d => !d.EstTerminee &&
                           d.StatutValidationDemandeur == StatutValidationCloture.Validee &&
                           d.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
                           d.StatutValidationDSI == StatutValidationCloture.EnAttente)
                .OrderByDescending(d => d.DateDemande)
                .ToListAsync();

            return View(demandes);
        }

        // POST: Valider Clôture - DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> ValiderClotureDSI(Guid demandeClotureId)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

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
        // Validation : (Demandeur OU DM) + DSI
        private async Task VerifierClotureComplete(DemandeClotureProjet demande)
        {
            // La clôture est complète si :
            // - (Demandeur validé OU DM validé) ET DSI validé
            var validationMetier = demande.StatutValidationDemandeur == StatutValidationCloture.Validee ||
                                   demande.StatutValidationDirecteurMetier == StatutValidationCloture.Validee;
            
            if (validationMetier &&
                demande.StatutValidationDSI == StatutValidationCloture.Validee &&
                !demande.EstTerminee)
            {
                demande.EstTerminee = true;
                demande.DateClotureFinale = DateTime.Now;

                var projet = demande.Projet;
                projet.StatutProjet = StatutProjet.Cloture;
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
        [Authorize(Roles = "ResponsableSolutionsIT,DSI,AdminIT")]
        public async Task<IActionResult> AjouterCommentaireTechnique(Guid id, string commentaireTechnique)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Seul ResponsableSolutionsIT peut ajouter/modifier (DSI peut voir mais pas modifier via cette action)
            if (!User.IsInRole("ResponsableSolutionsIT"))
            {
                return Forbid();
            }

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
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> RejeterClotureDSI(Guid demandeClotureId, string commentaire)
        {
            var demande = await _db.DemandesClotureProjets
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

            if (demande == null)
                return NotFound();

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
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> ForcerStatutProjet(Guid id, string actionType, string commentaire)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

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
        private bool CanActAsChefProjet(Projet projet, Guid userId, string userRole)
        {
            // ResponsableSolutionsIT est chef de projet par défaut sur tous les projets
            if (User.IsInRole("ResponsableSolutionsIT"))
                return true;
            
            // DSI et AdminIT peuvent agir comme chef de projet
            if (userRole == "DSI" || userRole == "AdminIT")
                return true;
            
            // Chef de Projet assigné au projet
            if (User.IsInRole("ChefDeProjet") && projet.ChefProjetId == userId)
                return true;
            
            return false;
        }

        // GET: Écran de validation de charte pour DM et DSI
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ValidationsProjet(int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Where(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification && 
                           (!p.CharteValideeParDM || !p.CharteValideeParDSI));
            
            // Filtrage selon le rôle
            if (User.IsInRole("DirecteurMetier"))
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user?.DirectionId.HasValue == true)
                {
                    // Voir les projets de sa direction où il est sponsor OU les projets de sa direction
                    query = query.Where(p => p.SponsorId == userId || p.DirectionId == user.DirectionId);
                }
                else
                {
                    query = query.Where(p => p.SponsorId == userId);
                }
            }
            // DSI et AdminIT voient tous les projets en attente de validation
            
            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            
            query = query.OrderByDescending(p => p.DateCreation);
            
            var pagedResult = await query.ToPagedResultAsync(page, pageSize);
            
            ViewBag.PageNumber = pagedResult.PageNumber;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.PageSize = pagedResult.PageSize;
            
            var projets = pagedResult.Items;
            
            return View(projets);
        }

        // POST: Valider Charte par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ValiderCharteDM(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Sponsor)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (projet == null)
                return NotFound();
            
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le sponsor OU un Directeur Métier de la direction
            bool canValidate = false;
            if (projet.SponsorId == userId || userRole == "DSI" || userRole == "AdminIT")
            {
                canValidate = true;
            }
            else if (User.IsInRole("DirecteurMetier"))
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user?.DirectionId.HasValue == true && projet.DirectionId == user.DirectionId)
                {
                    canValidate = true;
                }
            }
            
            if (!canValidate)
                return Forbid();
            
            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La charte ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(ValidationsProjet));
            }
            
            projet.CharteValideeParDM = true;
            projet.DateCharteValideeParDM = DateTime.Now;
            projet.CharteValideeParDMId = userId;
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
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> RejeterCharteDM(Guid id, string commentaire)
        {
            var projet = await _db.Projets
                .Include(p => p.Sponsor)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (projet == null)
                return NotFound();
            
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le sponsor OU un Directeur Métier de la direction
            bool canReject = false;
            if (projet.SponsorId == userId || userRole == "DSI" || userRole == "AdminIT")
            {
                canReject = true;
            }
            else if (User.IsInRole("DirecteurMetier"))
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user?.DirectionId.HasValue == true && projet.DirectionId == user.DirectionId)
                {
                    canReject = true;
                }
            }
            
            if (!canReject)
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
        [Authorize(Roles = "DSI,AdminIT")]
        public async Task<IActionResult> ValiderCharteDSI(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            
            if (projet == null)
                return NotFound();
            
            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La charte ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(ValidationsProjet));
            }
            
            var userId = User.GetUserIdOrThrow();
            
            projet.CharteValideeParDSI = true;
            projet.DateCharteValideeParDSI = DateTime.Now;
            projet.CharteValideeParDSIId = userId;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT")]
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
    }
}

