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
    public class DemandeProjetController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public DemandeProjetController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        // GET: Mes demandes (pour Demandeur)
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> Index(Guid? directionId, Guid? demandeurId, Guid? directeurMetierId, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            IQueryable<DemandeProjet> query = _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier);
            
            // Pour Demandeur : toujours filtrer par DemandeurId
            if (userRole == "Demandeur")
            {
                query = query.Where(d => d.DemandeurId == userId);
            }
            // Pour DSI/AdminIT : par défaut, afficher uniquement leurs propres demandes
            else if (userRole == "DSI" || userRole == "AdminIT")
            {
                // Si aucun filtre n'est appliqué, afficher uniquement les demandes du DSI/AdminIT
                if (!directionId.HasValue && !demandeurId.HasValue && !directeurMetierId.HasValue)
                {
                    query = query.Where(d => d.DemandeurId == userId);
                }
                else
                {
                    // Appliquer les filtres optionnels
                    if (directionId.HasValue)
                    {
                        query = query.Where(d => d.DirectionId == directionId.Value);
                    }
                    
                    if (demandeurId.HasValue)
                    {
                        query = query.Where(d => d.DemandeurId == demandeurId.Value);
                    }
                    
                    if (directeurMetierId.HasValue)
                    {
                        query = query.Where(d => d.DirecteurMetierId == directeurMetierId.Value);
                    }
                }
            }
            
            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            
            query = query.OrderByDescending(d => d.DateSoumission);
            
            var pagedResult = await query.ToPagedResultAsync(page, pageSize);
            
            ViewBag.PageNumber = pagedResult.PageNumber;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.PageSize = pagedResult.PageSize;
            
            var demandes = pagedResult.Items;

            // Pour DSI/AdminIT, charger les données pour les filtres
            if (userRole == "DSI" || userRole == "AdminIT")
            {
                ViewBag.Directions = await _db.Directions
                    .Where(d => !d.EstSupprime && d.EstActive)
                    .OrderBy(d => d.Libelle)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Libelle,
                        Selected = directionId == d.Id
                    })
                    .ToListAsync();

                ViewBag.Demandeurs = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id.ToString(),
                        Text = $"{u.Nom} {u.Prenoms}",
                        Selected = demandeurId == u.Id
                    })
                    .ToListAsync();

                ViewBag.DirecteursMetier = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && 
                                                   (ur.Role == RoleUtilisateur.DirecteurMetier || 
                                                    ur.Role == RoleUtilisateur.DSI || 
                                                    ur.Role == RoleUtilisateur.AdminIT)))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id.ToString(),
                        Text = $"{u.Nom} {u.Prenoms}",
                        Selected = directeurMetierId == u.Id
                    })
                    .ToListAsync();

                ViewBag.SelectedDirectionId = directionId;
                ViewBag.SelectedDemandeurId = demandeurId;
                ViewBag.SelectedDirecteurMetierId = directeurMetierId;
            }

            return View(demandes);
        }

        // GET: Liste à valider (pour Directeur Métier)
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ListeValidationDM()
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            var query = _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                           d.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier ||
                           d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI);
            
            // Pour Directeur Métier : filtrer par DirecteurMetierId = user courant
            // Pour DSI/AdminIT : voir toutes les demandes
            if (userRole != "DSI" && userRole != "AdminIT")
            {
                query = query.Where(d => d.DirecteurMetierId == userId);
            }
            
            var demandes = await query
                .OrderByDescending(d => d.DateSoumission)
                .ToListAsync();

            return View(demandes);
        }

        // GET: Validation DSI (pour DSI)
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> ListeValidationDSI()
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier si l'utilisateur est un délégué actif
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && 
                              d.EstActive && 
                              d.DateDebut <= DateTime.Now && 
                              d.DateFin >= DateTime.Now && 
                              !d.EstSupprime);
            
            // Si l'utilisateur n'est ni DSI/AdminIT/ResponsableSolutionsIT ni délégué actif, retourner une liste vide
            if (userRole != "DSI" && userRole != "AdminIT" && userRole != "ResponsableSolutionsIT" && !isDelegue)
            {
                return View(new List<DemandeProjet>());
            }
            
            var demandes = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDSI)
                .OrderByDescending(d => d.DateSoumission)
                .ToListAsync();

            return View(demandes);
        }

        // GET: Historique des validations DSI (toutes les demandes traitées par le DSI)
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> HistoriqueValidationsDSI()
        {
            var demandes = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande == StatutDemande.ValideeParDSI ||
                           d.StatutDemande == StatutDemande.RejeteeParDSI ||
                           d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI ||
                           d.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                .OrderByDescending(d => d.DateValidationDSI ?? d.DateSoumission)
                .ToListAsync();

            return View(demandes);
        }

        // GET: Détails demande
        public async Task<IActionResult> Details(Guid id)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Include(d => d.Annexes)
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            // Pour la validation DSI, passer la liste des chefs de projet
            // Le ResponsableSolutionsIT est celui qui choisit le chef de projet
            if (User.IsInRole("DSI") || User.IsInRole("AdminIT") || User.IsInRole("ResponsableSolutionsIT"))
            {
                // Récupérer les chefs de projet (utilisateurs avec rôle ChefDeProjet)
                var chefsProjet = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToListAsync();

                // Récupérer les utilisateurs avec délégation active ChefProjet (DateFin null = délégation active jusqu'à clôture du projet)
                var delegationsActives = await _db.DelegationsChefProjet
                    .Include(d => d.Delegue)
                    .Where(d => !d.EstSupprime && 
                                d.EstActive && 
                                d.DateDebut <= DateTime.Now && 
                                d.DateFin == null) // DateFin null = délégation active jusqu'à clôture du projet
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

            return View(demande);
        }

        // GET: Créer nouvelle demande
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> Create()
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Pour Demandeur : auto-remplir DirectionId et DirecteurMetierId avec la direction de l'utilisateur
            Guid? preSelectedDirectionId = null;
            Guid? preSelectedDirecteurMetierId = null;
            bool isReadOnly = false;
            
            if (userRole == "Demandeur")
            {
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user?.DirectionId.HasValue == true)
                {
                    preSelectedDirectionId = user.DirectionId;
                    isReadOnly = true;
                    
                    // Récupérer le Directeur Métier de la direction
                    var directeurMetier = await _db.Utilisateurs
                        .Include(u => u.UtilisateurRoles)
                        .Where(u => !u.EstSupprime && 
                                   u.DirectionId == user.DirectionId &&
                                   u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                        .FirstOrDefaultAsync();
                    
                    if (directeurMetier != null)
                    {
                        preSelectedDirecteurMetierId = directeurMetier.Id;
                    }
                }
            }

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", preSelectedDirectionId);
            ViewBag.PreSelectedDirectionId = preSelectedDirectionId;
            ViewBag.PreSelectedDirecteurMetierId = preSelectedDirecteurMetierId;
            ViewBag.IsReadOnly = isReadOnly;

            var directeursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && 
                                               (ur.Role == RoleUtilisateur.DirecteurMetier || 
                                                ur.Role == RoleUtilisateur.DSI || 
                                                ur.Role == RoleUtilisateur.AdminIT)))
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

        // GET: Récupérer les directeurs métier d'une direction (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetDirecteursMetierByDirection(Guid? directionId)
        {
            if (!directionId.HasValue)
            {
                return Json(new List<object>());
            }

            // Récupérer les directeurs métier de la direction + DSI et AdminIT (qui peuvent être directeurs métier de n'importe quelle direction)
            var directeursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && 
                           u.UtilisateurRoles.Any(ur => !ur.EstSupprime && 
                           (ur.Role == RoleUtilisateur.DirecteurMetier || 
                            ur.Role == RoleUtilisateur.DSI || 
                            ur.Role == RoleUtilisateur.AdminIT)) &&
                           (u.DirectionId == directionId.Value || 
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.AdminIT)) ||
                            u.DirectionId == null))
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .Select(u => new
                {
                    id = u.Id,
                    nom = u.Nom,
                    prenoms = u.Prenoms,
                    text = $"{u.Nom} {u.Prenoms}" + (u.UtilisateurRoles.Any(ur => !ur.EstSupprime && (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.AdminIT)) ? " (DSI/AdminIT)" : "")
                })
                .ToListAsync();

            return Json(directeursMetier);
        }

        // POST: Créer nouvelle demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Create(DemandeProjet demande, IFormFile? cahierCharges, List<IFormFile>? annexes)
        {
            // Supprimer les erreurs de validation pour les champs système qui ne sont pas remplis par l'utilisateur
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
            
            // Supprimer les erreurs de validation automatiques pour les remplacer par nos messages en français
            ModelState.Remove(nameof(demande.DirectionId));
            ModelState.Remove(nameof(demande.DirecteurMetierId));
            ModelState.Remove(nameof(demande.Direction));
            ModelState.Remove(nameof(demande.DirecteurMetier));
            ModelState.Remove(nameof(demande.Titre));
            ModelState.Remove(nameof(demande.Description));
            ModelState.Remove(nameof(demande.Contexte));
            ModelState.Remove(nameof(demande.Objectifs));

            // Validation manuelle avec messages en français
            if (string.IsNullOrWhiteSpace(demande.Titre))
            {
                ModelState.AddModelError(nameof(demande.Titre), "Le titre du projet est requis.");
            }

            if (string.IsNullOrWhiteSpace(demande.Description))
            {
                ModelState.AddModelError(nameof(demande.Description), "La description est requise.");
            }

            if (string.IsNullOrWhiteSpace(demande.Contexte))
            {
                ModelState.AddModelError(nameof(demande.Contexte), "Le contexte est requis.");
            }

            if (string.IsNullOrWhiteSpace(demande.Objectifs))
            {
                ModelState.AddModelError(nameof(demande.Objectifs), "Les objectifs sont requis.");
            }

            // Vérifier DirectionId (nullable Guid)
            if (!demande.DirectionId.HasValue || demande.DirectionId.Value == Guid.Empty)
            {
                ModelState.AddModelError(nameof(demande.DirectionId), "La direction est requise.");
            }

            // Vérifier DirecteurMetierId (non-nullable Guid)
            if (demande.DirecteurMetierId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(demande.DirecteurMetierId), "Le directeur métier est requis.");
            }

            if (ModelState.IsValid)
            {
                var userId = User.GetUserIdOrThrow();
                
                demande.Id = Guid.NewGuid();
                demande.DemandeurId = userId;
                demande.StatutDemande = StatutDemande.Brouillon;
                demande.DateSoumission = DateTime.Now;
                demande.DateCreation = DateTime.Now;
                demande.CreePar = _currentUserService.Matricule;
                
                // Initialiser les propriétés string nullable avec string.Empty pour éviter les erreurs de base de données
                demande.Titre = demande.Titre ?? string.Empty;
                demande.Description = demande.Description ?? string.Empty;
                demande.Contexte = demande.Contexte ?? string.Empty;
                demande.Objectifs = demande.Objectifs ?? string.Empty;
                demande.AvantagesAttendus = demande.AvantagesAttendus ?? string.Empty;
                demande.Perimetre = demande.Perimetre ?? string.Empty;
                demande.CommentaireDirecteurMetier = demande.CommentaireDirecteurMetier ?? string.Empty;
                demande.CommentaireDSI = demande.CommentaireDSI ?? string.Empty;
                demande.CahierChargesPath = demande.CahierChargesPath ?? string.Empty;

                // Upload cahier des charges
                if (cahierCharges != null && cahierCharges.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    var maxSize = 10 * 1024 * 1024; // 10 MB
                    var savedPath = await _fileStorage.SaveFileAsync(
                        cahierCharges, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        demande.CahierChargesPath = savedPath;
                    }
                }
                // S'assurer que CahierChargesPath n'est jamais null (seulement si aucun fichier n'a été uploadé)
                if (string.IsNullOrEmpty(demande.CahierChargesPath))
                {
                    demande.CahierChargesPath = string.Empty;
                }

                _db.DemandesProjets.Add(demande);
                await _db.SaveChangesAsync();

                // Upload annexes
                if (annexes != null && annexes.Any())
                {
                    foreach (var annexe in annexes)
                    {
                        if (annexe.Length > 0)
                        {
                            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                            var maxSize = 10 * 1024 * 1024; // 10 MB
                            var path = await _fileStorage.SaveFileAsync(annexe, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);
                            
                            var doc = new DocumentJointDemande
                            {
                                Id = Guid.NewGuid(),
                                DemandeProjetId = demande.Id,
                                NomFichier = annexe.FileName,
                                CheminRelatif = path,
                                DateDepot = DateTime.Now,
                                DeposeParId = userId,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule
                            };

                            _db.DocumentsJointsDemandes.Add(doc);
                        }
                    }
                    await _db.SaveChangesAsync();
                }

                await _auditService.LogActionAsync("CREATION_DEMANDE", "DemandeProjet", demande.Id,
                    null,
                    new { Titre = demande.Titre, DirectionId = demande.DirectionId, DirecteurMetierId = demande.DirecteurMetierId });

                return RedirectToAction(nameof(Details), new { id = demande.Id });
            }

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", demande?.DirectionId);

            var directeursMetier = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && (u.Role == RoleUtilisateur.DirecteurMetier || 
                                               u.Role == RoleUtilisateur.DSI || 
                                               u.Role == RoleUtilisateur.AdminIT))
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

        // GET: Soumettre demande
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> Soumettre(Guid id)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .FirstOrDefaultAsync(d => d.Id == id);
            
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (demande.DemandeurId != userId)
                return Forbid();

            if (demande.StatutDemande != StatutDemande.Brouillon)
            {
                TempData["Error"] = "Cette demande ne peut plus être modifiée.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Vérifier les demandes similaires existantes
            var demandesSimilaires = await DetecterDemandesSimilairesAsync(demande);
            
            if (demandesSimilaires.Any())
            {
                ViewBag.DemandesSimilaires = demandesSimilaires;
                ViewBag.DemandeCourante = demande;
                return View("VerificationDoublons", demande);
            }

            // Créer automatiquement le portefeuille s'il n'existe pas
            var portefeuilleActif = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuilleActif == null)
            {
                portefeuilleActif = new PortefeuilleProjet
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
                _db.PortefeuillesProjets.Add(portefeuilleActif);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_PORTEFEUILLE", "PortefeuilleProjet", portefeuilleActif.Id,
                    null,
                    new { Nom = portefeuilleActif.Nom, EstActif = portefeuilleActif.EstActif });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
            demande.DateSoumission = DateTime.Now;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SOUMISSION_DEMANDE", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande });

            TempData["Success"] = "Demande soumise avec succès.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Confirmer soumission après vérification des doublons
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> ConfirmerSoumission(Guid id, bool confirmerMalgreDoublons = false)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (demande.DemandeurId != userId)
                return Forbid();

            if (demande.StatutDemande != StatutDemande.Brouillon)
            {
                TempData["Error"] = "Cette demande ne peut plus être modifiée.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!confirmerMalgreDoublons)
            {
                // Vérifier à nouveau les doublons
                var demandesSimilaires = await DetecterDemandesSimilairesAsync(demande);
                if (demandesSimilaires.Any())
                {
                    ViewBag.DemandesSimilaires = demandesSimilaires;
                    ViewBag.DemandeCourante = demande;
                    TempData["Warning"] = "Veuillez confirmer que vous souhaitez soumettre cette demande malgré l'existence de demandes similaires.";
                    return View("VerificationDoublons", demande);
                }
            }

            // Créer automatiquement le portefeuille s'il n'existe pas
            var portefeuilleActif = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuilleActif == null)
            {
                portefeuilleActif = new PortefeuilleProjet
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
                _db.PortefeuillesProjets.Add(portefeuilleActif);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_PORTEFEUILLE", "PortefeuilleProjet", portefeuilleActif.Id,
                    null,
                    new { Nom = portefeuilleActif.Nom, EstActif = portefeuilleActif.EstActif });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
            demande.DateSoumission = DateTime.Now;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SOUMISSION_DEMANDE", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut, ConfirmeMalgreDoublons = confirmerMalgreDoublons },
                new { StatutDemande = demande.StatutDemande });

            TempData["Success"] = "Demande soumise avec succès.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Méthode privée pour détecter les demandes similaires
        private async Task<List<DemandeSimilaireInfo>> DetecterDemandesSimilairesAsync(DemandeProjet demande)
        {
            var resultats = new List<DemandeSimilaireInfo>();

            if (string.IsNullOrWhiteSpace(demande.Titre))
                return resultats;

            // Normaliser le titre pour la comparaison (supprimer espaces, minuscules)
            var titreNormalise = NormaliserTexte(demande.Titre);

            // Rechercher les demandes avec un titre similaire (au moins 70% de similarité)
            var demandesExistantes = await _db.DemandesProjets
                .Include(d => d.Demandeur)
                .Include(d => d.Direction)
                .Include(d => d.Projet)
                    .ThenInclude(p => p != null ? p.ChefProjet : null)
                .Where(d => d.Id != demande.Id && !string.IsNullOrWhiteSpace(d.Titre))
                .ToListAsync();

            foreach (var demandeExistante in demandesExistantes)
            {
                var titreExistanteNormalise = NormaliserTexte(demandeExistante.Titre ?? string.Empty);
                
                // Calculer la similarité (simple comparaison de sous-chaînes)
                var similarite = CalculerSimilarite(titreNormalise, titreExistanteNormalise);
                
                if (similarite >= 0.7) // 70% de similarité minimum
                {
                    // Vérifier si un projet existe pour cette demande
                    var projetExistant = await _db.Projets
                        .Include(p => p.ChefProjet)
                        .FirstOrDefaultAsync(p => p.DemandeProjetId == demandeExistante.Id);

                    var info = new DemandeSimilaireInfo
                    {
                        DemandeId = demandeExistante.Id,
                        Titre = demandeExistante.Titre ?? string.Empty,
                        StatutDemande = demandeExistante.StatutDemande,
                        DateSoumission = demandeExistante.DateSoumission,
                        Demandeur = $"{demandeExistante.Demandeur?.Nom} {demandeExistante.Demandeur?.Prenoms}",
                        Direction = demandeExistante.Direction?.Libelle ?? "N/A",
                        CommentaireRejet = GetCommentaireRejet(demandeExistante),
                        ProjetExistant = projetExistant != null ? new ProjetExistantInfo
                        {
                            ProjetId = projetExistant.Id,
                            CodeProjet = projetExistant.CodeProjet,
                            Titre = projetExistant.Titre,
                            StatutProjet = projetExistant.StatutProjet,
                            PhaseActuelle = projetExistant.PhaseActuelle,
                            ChefProjet = projetExistant.ChefProjet != null 
                                ? $"{projetExistant.ChefProjet.Nom} {projetExistant.ChefProjet.Prenoms}" 
                                : "Non assigné"
                        } : null,
                        Similarite = similarite
                    };

                    resultats.Add(info);
                }
            }

            return resultats.OrderByDescending(r => r.Similarite).ToList();
        }

        private string NormaliserTexte(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte))
                return string.Empty;

            return texte.ToLowerInvariant()
                .Trim()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "");
        }

        private double CalculerSimilarite(string texte1, string texte2)
        {
            if (string.IsNullOrEmpty(texte1) || string.IsNullOrEmpty(texte2))
                return 0.0;

            // Algorithme simple de similarité basé sur la longueur de la sous-chaîne commune
            var longueurMin = Math.Min(texte1.Length, texte2.Length);
            var longueurMax = Math.Max(texte1.Length, texte2.Length);

            if (longueurMin == 0)
                return 0.0;

            // Compter les caractères communs
            var caracteresCommuns = 0;
            var minLength = Math.Min(texte1.Length, texte2.Length);
            
            for (int i = 0; i < minLength; i++)
            {
                if (i < texte1.Length && i < texte2.Length && texte1[i] == texte2[i])
                    caracteresCommuns++;
            }

            // Vérifier aussi si l'un contient l'autre
            if (texte1.Contains(texte2) || texte2.Contains(texte1))
                return 0.9;

            return (double)caracteresCommuns / longueurMax;
        }

        private string? GetCommentaireRejet(DemandeProjet demande)
        {
            if (demande.StatutDemande == StatutDemande.RejeteeParDirecteurMetier)
                return demande.CommentaireDirecteurMetier;
            
            if (demande.StatutDemande == StatutDemande.RejeteeParDSI)
                return demande.CommentaireDSI;

            if (demande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
                return demande.CommentaireDirecteurMetier;

            if (demande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                return demande.CommentaireDSI;

            return null;
        }

        // Classe pour stocker les informations sur les demandes similaires
        private class DemandeSimilaireInfo
        {
            public Guid DemandeId { get; set; }
            public string Titre { get; set; } = string.Empty;
            public StatutDemande StatutDemande { get; set; }
            public DateTime DateSoumission { get; set; }
            public string Demandeur { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public string? CommentaireRejet { get; set; }
            public ProjetExistantInfo? ProjetExistant { get; set; }
            public double Similarite { get; set; }
        }

        private class ProjetExistantInfo
        {
            public Guid ProjetId { get; set; }
            public string CodeProjet { get; set; } = string.Empty;
            public string Titre { get; set; } = string.Empty;
            public StatutProjet StatutProjet { get; set; }
            public PhaseProjet PhaseActuelle { get; set; }
            public string ChefProjet { get; set; } = string.Empty;
        }

        // GET: Éditer demande (pour correction)
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Annexes)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            // DSI et AdminIT peuvent modifier n'importe quelle demande
            if (demande.DemandeurId != userId && userRole != "DSI" && userRole != "AdminIT")
                return Forbid();

            if (demande.StatutDemande != StatutDemande.Brouillon &&
                demande.StatutDemande != StatutDemande.CorrectionDemandeeParDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDemandeurParDSI)
            {
                // DSI et AdminIT peuvent modifier même si le statut ne le permet pas normalement
                if (userRole != "DSI" && userRole != "AdminIT")
                {
                    TempData["Error"] = "Cette demande ne peut plus être modifiée.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", demande?.DirectionId);

            var directeursMetier = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && (u.Role == RoleUtilisateur.DirecteurMetier || 
                                               u.Role == RoleUtilisateur.DSI || 
                                               u.Role == RoleUtilisateur.AdminIT))
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
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> Edit(
            Guid id,
            string Titre,
            string Description,
            string Contexte,
            string Objectifs,
            string AvantagesAttendus,
            string? Perimetre,
            int Urgence,
            int Criticite,
            DateTime? DateMiseEnOeuvreSouhaitee,
            string? DirectionId,
            string DirecteurMetierId,
            IFormFile? cahierCharges,
            List<IFormFile>? annexes)
        {
            var existingDemande = await _db.DemandesProjets
                .Include(d => d.Annexes)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (existingDemande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (existingDemande.DemandeurId != userId)
                return Forbid();

            // Supprimer les erreurs de validation automatiques pour les remplacer par nos messages en français
            ModelState.Remove("Projet");
            ModelState.Remove("Demandeur");
            ModelState.Remove("DemandeurId");
            ModelState.Remove("CommentaireDSI");
            ModelState.Remove("CommentaireDirecteurMetier");
            ModelState.Remove("CahierChargesPath");
            ModelState.Remove("StatutDemande");
            ModelState.Remove("DateSoumission");
            ModelState.Remove("DateValidationDM");
            ModelState.Remove("DateValidationDSI");
            ModelState.Remove("CreePar");
            ModelState.Remove("DateCreation");
            ModelState.Remove("ModifiePar");
            ModelState.Remove("DateModification");
            ModelState.Remove("DirectionId");
            ModelState.Remove("DirecteurMetierId");
            ModelState.Remove("Direction");
            ModelState.Remove("DirecteurMetier");
            ModelState.Remove("Titre");
            ModelState.Remove("Description");
            ModelState.Remove("Contexte");
            ModelState.Remove("Objectifs");
            ModelState.Remove("AvantagesAttendus");

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(Titre))
            {
                ModelState.AddModelError("Titre", "Le titre est requis.");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                ModelState.AddModelError("Description", "La description est requise.");
            }

            if (string.IsNullOrWhiteSpace(Contexte))
            {
                ModelState.AddModelError("Contexte", "Le contexte est requis.");
            }

            if (string.IsNullOrWhiteSpace(Objectifs))
            {
                ModelState.AddModelError("Objectifs", "Les objectifs sont requis.");
            }

            // Parser les GUIDs pour validation et utilisation
            Guid? directionGuid = null;
            Guid directeurMetierGuid = Guid.Empty;

            if (string.IsNullOrWhiteSpace(DirectionId) || !Guid.TryParse(DirectionId, out var parsedDirectionGuid))
            {
                ModelState.AddModelError("DirectionId", "La direction est requise.");
            }
            else
            {
                directionGuid = parsedDirectionGuid;
            }

            if (string.IsNullOrWhiteSpace(DirecteurMetierId) || !Guid.TryParse(DirecteurMetierId, out directeurMetierGuid))
            {
                ModelState.AddModelError("DirecteurMetierId", "Le directeur métier est requis.");
            }

            if (ModelState.IsValid)
            {
                // Mise à jour des propriétés
                existingDemande.Titre = Titre.Trim();
                existingDemande.Description = Description?.Trim() ?? string.Empty;
                existingDemande.Contexte = Contexte?.Trim() ?? string.Empty;
                existingDemande.Objectifs = Objectifs?.Trim() ?? string.Empty;
                existingDemande.AvantagesAttendus = AvantagesAttendus?.Trim() ?? string.Empty;
                existingDemande.Perimetre = Perimetre?.Trim() ?? string.Empty;
                existingDemande.Urgence = (UrgenceProjet)Urgence;
                existingDemande.Criticite = (CriticiteProjet)Criticite;
                existingDemande.DateMiseEnOeuvreSouhaitee = DateMiseEnOeuvreSouhaitee;

                // Gérer DirectionId
                existingDemande.DirectionId = directionGuid;

                // Gérer DirecteurMetierId
                existingDemande.DirecteurMetierId = directeurMetierGuid;

                // Si en correction, repasser au bon statut selon l'origine de la correction
                if (existingDemande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
                {
                    existingDemande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
                }
                else if (existingDemande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                {
                    existingDemande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
                }

                // Upload nouveau cahier des charges si fourni
                if (cahierCharges != null && cahierCharges.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    var maxSize = 10 * 1024 * 1024; // 10 MB
                    if (!string.IsNullOrEmpty(existingDemande.CahierChargesPath))
                        await _fileStorage.DeleteFileAsync(existingDemande.CahierChargesPath);

                    existingDemande.CahierChargesPath = await _fileStorage.SaveFileAsync(
                        cahierCharges, "demandes", existingDemande.Id.ToString(), allowedExtensions, maxSize);
                }

                // Upload nouvelles annexes
                if (annexes != null && annexes.Any())
                {
                    foreach (var annexe in annexes)
                    {
                        if (annexe.Length > 0)
                        {
                            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                            var maxSize = 10 * 1024 * 1024; // 10 MB
                            var path = await _fileStorage.SaveFileAsync(annexe, "demandes", existingDemande.Id.ToString(), allowedExtensions, maxSize);
                            
                            var doc = new DocumentJointDemande
                            {
                                Id = Guid.NewGuid(),
                                DemandeProjetId = existingDemande.Id,
                                NomFichier = annexe.FileName,
                                CheminRelatif = path,
                                DateDepot = DateTime.Now,
                                DeposeParId = userId,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule
                            };

                            _db.DocumentsJointsDemandes.Add(doc);
                        }
                    }
                }

                // Mise à jour des champs d'audit
                existingDemande.DateModification = DateTime.Now;
                existingDemande.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                // Logger selon le contexte : correction ou simple modification
                var ancienStatut = existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier 
                    ? StatutDemande.CorrectionDemandeeParDirecteurMetier 
                    : (existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDSI 
                        ? StatutDemande.RetourneeAuDemandeurParDSI 
                        : existingDemande.StatutDemande);

                if (existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                    existingDemande.StatutDemande == StatutDemande.EnAttenteValidationDSI)
                {
                    await _auditService.LogActionAsync("CORRECTION_DEMANDE", "DemandeProjet", existingDemande.Id,
                        new { AncienStatut = ancienStatut },
                        new { NouveauStatut = existingDemande.StatutDemande });
                }
                else
                {
                    await _auditService.LogActionAsync("MODIFICATION_DEMANDE", "DemandeProjet", existingDemande.Id);
                }

                TempData["Success"] = "Demande modifiée avec succès.";
                return RedirectToAction(nameof(Details), new { id = existingDemande.Id });
            }

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.Directions = new SelectList(directions, "Id", "Libelle", existingDemande?.DirectionId);

            var directeursMetier = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && (u.Role == RoleUtilisateur.DirecteurMetier || 
                                               u.Role == RoleUtilisateur.DSI || 
                                               u.Role == RoleUtilisateur.AdminIT))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.DirecteursMetier = directeursMetier.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Nom} {u.Prenoms}",
                Selected = existingDemande?.DirecteurMetierId == u.Id
            });

            return View(existingDemande);
        }

        // POST: Valider par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> ValiderDM(Guid id, string? commentaire, 
            string? titre, string? description, string? objectifs, string? avantagesAttendus)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le Directeur Métier assigné (DirecteurMetierId = user courant)
            if (userRole != "DSI" && userRole != "AdminIT")
            {
                if (demande.DirecteurMetierId != userId)
                    return Forbid();
            }

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            
            // Permettre au DM de modifier certains champs avant validation
            // OK pour titre, description, objectifs, avantages
            // PAS de modification de Direction ou Demandeur
            if (!string.IsNullOrWhiteSpace(titre))
            {
                demande.Titre = titre.Trim();
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                demande.Description = description.Trim();
            }
            if (!string.IsNullOrWhiteSpace(objectifs))
            {
                demande.Objectifs = objectifs.Trim();
            }
            if (avantagesAttendus != null)
            {
                demande.AvantagesAttendus = avantagesAttendus.Trim();
            }
            
            demande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
            demande.DateValidationDM = DateTime.Now;
            demande.CommentaireDirecteurMetier = commentaire ?? string.Empty;
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_DM", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            TempData["Success"] = "Demande validée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Rejeter par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> RejeterDM(Guid id, string commentaire)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le Directeur Métier assigné (DirecteurMetierId = user courant)
            if (userRole != "DSI" && userRole != "AdminIT")
            {
                if (demande.DirecteurMetierId != userId)
                    return Forbid();
            }

            // Le commentaire est obligatoire pour le rejet
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                ModelState.AddModelError("commentaire", "Le motif du rejet est obligatoire.");
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
            demande.StatutDemande = StatutDemande.RejeteeParDirecteurMetier;
            demande.CommentaireDirecteurMetier = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_DM", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            TempData["Success"] = "Demande rejetée.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Demander correction par Directeur Métier
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,DSI,AdminIT")]
        public async Task<IActionResult> DemanderCorrectionDM(Guid id, string commentaire)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier que l'utilisateur est le Directeur Métier assigné (DirecteurMetierId = user courant)
            if (userRole != "DSI" && userRole != "AdminIT")
            {
                if (demande.DirecteurMetierId != userId)
                    return Forbid();
            }

            // Le commentaire est obligatoire pour demander une correction
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                ModelState.AddModelError("commentaire", "Le commentaire est obligatoire pour demander une correction.");
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
            demande.StatutDemande = StatutDemande.CorrectionDemandeeParDirecteurMetier;
            demande.CommentaireDirecteurMetier = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CORRECTION_DM", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            TempData["Success"] = "Correction demandée au demandeur.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Valider par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> ValiderDSI(Guid id, string? commentaire, Guid? chefProjetId)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier si l'utilisateur est un délégué actif
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && 
                              d.EstActive && 
                              d.DateDebut <= DateTime.Now && 
                              d.DateFin >= DateTime.Now && 
                              !d.EstSupprime);
            
            // Vérifier les permissions : doit être DSI, AdminIT, ResponsableSolutionsIT ou délégué actif
            if (userRole != "DSI" && userRole != "AdminIT" && userRole != "ResponsableSolutionsIT" && !isDelegue)
            {
                TempData["Error"] = "Vous n'avez pas l'autorisation de valider cette demande.";
                return RedirectToAction(nameof(Details), new { id });
            }
            
            var demande = await _db.DemandesProjets
                .Include(d => d.Direction)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation DSI.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande = StatutDemande.ValideeParDSI;
            demande.DateValidationDSI = DateTime.Now;
            demande.CommentaireDSI = commentaire ?? string.Empty;
            await _db.SaveChangesAsync();

            // Récupérer le portefeuille actif ou le créer s'il n'existe pas
            var portefeuilleActif = await _db.PortefeuillesProjets
                .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

            if (portefeuilleActif == null)
            {
                // Créer un portefeuille par défaut si aucun n'existe
                portefeuilleActif = new PortefeuilleProjet
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
                _db.PortefeuillesProjets.Add(portefeuilleActif);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_PORTEFEUILLE", "PortefeuilleProjet", portefeuilleActif.Id,
                    null,
                    new { Nom = portefeuilleActif.Nom, EstActif = portefeuilleActif.EstActif });
            }

            // Création automatique du Projet
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                CodeProjet = $"PROJ-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                Titre = demande.Titre ?? string.Empty,
                Objectif = demande.Objectifs, // Copier l'objectif de la demande
                PortefeuilleProjetId = portefeuilleActif.Id, // Assigner au portefeuille actif (garanti d'exister maintenant)
                DemandeProjetId = demande.Id,
                DirectionId = demande.DirectionId,
                SponsorId = demande.DirecteurMetierId,
                ChefProjetId = chefProjetId,
                StatutProjet = StatutProjet.NonDemarre, // Projet en analyse (pas encore démarré)
                PhaseActuelle = PhaseProjet.AnalyseClarification, // Phase Analyse
                EtatProjet = EtatProjet.Vert,
                PourcentageAvancement = 0,
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? string.Empty
            };

            _db.Projets.Add(projet);
            await _db.SaveChangesAsync();

            // Différencier les logs selon si c'est fait par un délégué
            string actionLog = (isDelegue || userRole == "ResponsableSolutionsIT") ? "VALIDATION_DSI_PAR_DELEGUE" : "VALIDATION_DSI";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            await _auditService.LogActionAsync("CREATION_PROJET", "Projet", projet.Id,
                null,
                new { CodeProjet = projet.CodeProjet, Titre = projet.Titre, StatutProjet = projet.StatutProjet, PhaseActuelle = projet.PhaseActuelle });

            TempData["Success"] = $"Demande validée et projet {projet.CodeProjet} créé automatiquement.";
            return RedirectToAction("Details", "Projet", new { id = projet.Id });
        }

        // POST: Rejeter par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> RejeterDSI(Guid id, string? commentaire)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier si l'utilisateur est un délégué actif
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && 
                              d.EstActive && 
                              d.DateDebut <= DateTime.Now && 
                              d.DateFin >= DateTime.Now && 
                              !d.EstSupprime);
            
            // Vérifier les permissions : doit être DSI, AdminIT, ResponsableSolutionsIT ou délégué actif
            if (userRole != "DSI" && userRole != "AdminIT" && userRole != "ResponsableSolutionsIT" && !isDelegue)
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

            // Le commentaire est obligatoire pour le rejet
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                ModelState.AddModelError("commentaire", "Le commentaire est obligatoire pour rejeter la demande.");
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la demande.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande = StatutDemande.RejeteeParDSI;
            demande.CommentaireDSI = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            // Différencier les logs selon si c'est fait par un délégué
            string actionLog = (isDelegue || userRole == "ResponsableSolutionsIT") ? "REJET_DSI_PAR_DELEGUE" : "REJET_DSI";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            TempData["Success"] = "Demande rejetée par la DSI.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Renvoyer au demandeur par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> RenvoyerAuDemandeurDSI(Guid id, string? commentaire)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier si l'utilisateur est un délégué actif
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && 
                              d.EstActive && 
                              d.DateDebut <= DateTime.Now && 
                              d.DateFin >= DateTime.Now && 
                              !d.EstSupprime);
            
            // Vérifier les permissions : doit être DSI, AdminIT, ResponsableSolutionsIT ou délégué actif
            if (userRole != "DSI" && userRole != "AdminIT" && userRole != "ResponsableSolutionsIT" && !isDelegue)
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

            // Le commentaire est obligatoire pour renvoyer au demandeur
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                ModelState.AddModelError("commentaire", "Le commentaire est obligatoire pour renvoyer la demande au demandeur.");
                TempData["Error"] = "Le commentaire est obligatoire pour renvoyer la demande au demandeur.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande = StatutDemande.RetourneeAuDemandeurParDSI;
            demande.CommentaireDSI = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            // Différencier les logs selon si c'est fait par un délégué
            string actionLog = (isDelegue || userRole == "ResponsableSolutionsIT") ? "CORRECTION_DSI_DEMANDEUR_PAR_DELEGUE" : "CORRECTION_DSI_DEMANDEUR";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            TempData["Success"] = "Demande renvoyée au demandeur pour correction.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Renvoyer au Directeur Métier par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
        public async Task<IActionResult> RenvoyerAuDMDSI(Guid id, string commentaire)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();
            
            // Vérifier si l'utilisateur est un délégué actif
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId && 
                              d.EstActive && 
                              d.DateDebut <= DateTime.Now && 
                              d.DateFin >= DateTime.Now && 
                              !d.EstSupprime);
            
            // Vérifier les permissions : doit être DSI, AdminIT, ResponsableSolutionsIT ou délégué actif
            if (userRole != "DSI" && userRole != "AdminIT" && userRole != "ResponsableSolutionsIT" && !isDelegue)
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

            // Le commentaire est obligatoire pour renvoyer au DM
            if (string.IsNullOrWhiteSpace(commentaire))
            {
                ModelState.AddModelError("commentaire", "Le commentaire est obligatoire pour renvoyer la demande au Directeur Métier.");
                TempData["Error"] = "Le commentaire est obligatoire pour renvoyer la demande au Directeur Métier.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande = StatutDemande.RetourneeAuDirecteurMetierParDSI;
            demande.CommentaireDSI = commentaire.Trim();
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            // Différencier les logs selon si c'est fait par un délégué
            string actionLog = (isDelegue || userRole == "ResponsableSolutionsIT") ? "CORRECTION_DSI_DM_PAR_DELEGUE" : "CORRECTION_DSI_DM";
            await _auditService.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande, Commentaire = commentaire });

            TempData["Success"] = "Demande renvoyée au Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Ajouter des documents complémentaires à une demande soumise
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> AjouterDocumentsComplementaires(Guid id, List<IFormFile>? documents)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Annexes)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Vérifier que l'utilisateur est le demandeur ou DSI/AdminIT
            if (demande.DemandeurId != userId && userRole != "DSI" && userRole != "AdminIT")
                return Forbid();

            // Permettre l'ajout de documents si la demande est en attente de compléments ou en cours de traitement
            if (demande.StatutDemande != StatutDemande.CorrectionDemandeeParDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDemandeurParDSI &&
                demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
                demande.StatutDemande != StatutDemande.EnAttenteValidationDSI &&
                userRole != "DSI" && userRole != "AdminIT")
            {
                TempData["Error"] = "Vous ne pouvez ajouter des documents que lorsque la demande est en attente de compléments ou en cours de traitement.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (documents != null && documents.Any())
            {
                var documentsAjoutes = 0;
                foreach (var document in documents)
                {
                    if (document.Length > 0)
                    {
                        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                        var maxSize = 10 * 1024 * 1024; // 10 MB
                        var path = await _fileStorage.SaveFileAsync(document, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);
                        
                        var doc = new DocumentJointDemande
                        {
                            Id = Guid.NewGuid(),
                            DemandeProjetId = demande.Id,
                            NomFichier = document.FileName,
                            CheminRelatif = path,
                            DateDepot = DateTime.Now,
                            DeposeParId = userId,
                            DateCreation = DateTime.Now,
                            CreePar = _currentUserService.Matricule
                        };

                        _db.DocumentsJointsDemandes.Add(doc);
                        documentsAjoutes++;
                    }
                }

                if (documentsAjoutes > 0)
                {
                    // Si la demande était en attente de compléments, la remettre en validation
                    if (demande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
                    {
                        demande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
                        await _auditService.LogActionAsync("COMPLEMENT_INFORMATION_AJOUTE", "DemandeProjet", demande.Id,
                            new { AncienStatut = StatutDemande.CorrectionDemandeeParDirecteurMetier, NouveauStatut = demande.StatutDemande, NombreDocuments = documentsAjoutes });
                    }
                    else if (demande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                    {
                        demande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
                        await _auditService.LogActionAsync("COMPLEMENT_INFORMATION_AJOUTE", "DemandeProjet", demande.Id,
                            new { AncienStatut = StatutDemande.RetourneeAuDemandeurParDSI, NouveauStatut = demande.StatutDemande, NombreDocuments = documentsAjoutes });
                    }
                    else
                    {
                        await _auditService.LogActionAsync("AJOUT_DOCUMENTS_COMPLEMENTAIRES", "DemandeProjet", demande.Id,
                            new { NombreDocuments = documentsAjoutes });
                    }
                    
                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"{documentsAjoutes} document(s) complémentaire(s) ajouté(s) avec succès. La demande a été remise en validation.";
                }
                else
                {
                    TempData["Error"] = "Aucun document valide n'a été ajouté.";
                }
            }
            else
            {
                TempData["Error"] = "Veuillez sélectionner au moins un document.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Dupliquer/Relancer une demande refusée
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Demandeur,DSI,AdminIT")]
        public async Task<IActionResult> DupliquerDemande(Guid id)
        {
            var demandeOriginale = await _db.DemandesProjets
                .Include(d => d.Annexes)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demandeOriginale == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Vérifier que l'utilisateur est le demandeur ou DSI/AdminIT
            if (demandeOriginale.DemandeurId != userId && userRole != "DSI" && userRole != "AdminIT")
                return Forbid();

            // Vérifier que la demande est refusée
            if (demandeOriginale.StatutDemande != StatutDemande.RejeteeParDirecteurMetier &&
                demandeOriginale.StatutDemande != StatutDemande.RejeteeParDSI)
            {
                TempData["Error"] = "Seules les demandes refusées peuvent être dupliquées.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Créer une nouvelle demande basée sur l'originale
            var nouvelleDemande = new DemandeProjet
            {
                Id = Guid.NewGuid(),
                DemandeurId = demandeOriginale.DemandeurId,
                DirectionId = demandeOriginale.DirectionId,
                DirecteurMetierId = demandeOriginale.DirecteurMetierId,
                Titre = demandeOriginale.Titre,
                Description = demandeOriginale.Description,
                Contexte = demandeOriginale.Contexte,
                Objectifs = demandeOriginale.Objectifs,
                AvantagesAttendus = demandeOriginale.AvantagesAttendus,
                Perimetre = demandeOriginale.Perimetre,
                Urgence = demandeOriginale.Urgence,
                Criticite = demandeOriginale.Criticite,
                DateMiseEnOeuvreSouhaitee = demandeOriginale.DateMiseEnOeuvreSouhaitee,
                StatutDemande = StatutDemande.Brouillon,
                DateSoumission = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule,
                CahierChargesPath = string.Empty,
                CommentaireDirecteurMetier = string.Empty,
                CommentaireDSI = string.Empty
            };

            _db.DemandesProjets.Add(nouvelleDemande);
            await _db.SaveChangesAsync();

            // Dupliquer les documents joints
            if (demandeOriginale.Annexes != null && demandeOriginale.Annexes.Any())
            {
                foreach (var annexe in demandeOriginale.Annexes)
                {
                    var nouvelleAnnexe = new DocumentJointDemande
                    {
                        Id = Guid.NewGuid(),
                        DemandeProjetId = nouvelleDemande.Id,
                        NomFichier = annexe.NomFichier,
                        CheminRelatif = annexe.CheminRelatif, // Réutiliser le même fichier
                        DateDepot = DateTime.Now,
                        DeposeParId = userId,
                        DateCreation = DateTime.Now,
                        CreePar = _currentUserService.Matricule
                    };
                    _db.DocumentsJointsDemandes.Add(nouvelleAnnexe);
                }
                await _db.SaveChangesAsync();
            }

            await _auditService.LogActionAsync("RELANCE_DEMANDE_PROJET", "DemandeProjet", nouvelleDemande.Id,
                new { DemandeOriginaleId = demandeOriginale.Id, StatutOriginal = demandeOriginale.StatutDemande });

            TempData["Success"] = "Demande dupliquée avec succès. Vous pouvez maintenant la modifier et la soumettre.";
            return RedirectToAction(nameof(Details), new { id = nouvelleDemande.Id });
        }
    }
}

