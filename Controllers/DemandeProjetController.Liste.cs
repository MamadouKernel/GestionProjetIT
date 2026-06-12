using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.DemandeProjet;
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
        // GET: Mes demandes (pour Demandeur)
        [Authorize]
        public async Task<IActionResult> Index(Guid? directionId, Guid? demandeurId, Guid? directeurMetierId, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Index"))
                return Forbid();

            IQueryable<DemandeProjet> query = _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier);

            if (!canManageDemandes)
            {
                query = query.Where(d => d.DemandeurId == userId);
            }
            else
            {
                if (!directionId.HasValue && !demandeurId.HasValue && !directeurMetierId.HasValue)
                {
                    query = query.Where(d => d.DemandeurId == userId);
                }
                else
                {
                    if (directionId.HasValue)
                        query = query.Where(d => d.DirectionId == directionId.Value);

                    if (demandeurId.HasValue)
                        query = query.Where(d => d.DemandeurId == demandeurId.Value);

                    if (directeurMetierId.HasValue)
                        query = query.Where(d => d.DirecteurMetierId == directeurMetierId.Value);
                }
            }

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            query    = query.OrderByDescending(d => d.DateSoumission);

            var pagedResult = await query.ToPagedResultAsync(page, pageSize);

            var vm = new DemandeProjetIndexViewModel
            {
                Demandes   = pagedResult.Items,
                PageNumber = pagedResult.PageNumber,
                TotalPages = pagedResult.TotalPages,
                TotalCount = pagedResult.TotalCount,
                PageSize   = pagedResult.PageSize
            };

            if (canManageDemandes)
            {
                vm.Directions = await _db.Directions
                    .Where(d => !d.EstSupprime && d.EstActive)
                    .OrderBy(d => d.Libelle)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Libelle,
                        Selected = directionId == d.Id
                    })
                    .ToListAsync();

                vm.Demandeurs = await _db.Utilisateurs
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

                vm.DirecteursMetier = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id.ToString(),
                        Text = $"{u.Nom} {u.Prenoms}",
                        Selected = directeurMetierId == u.Id
                    })
                    .ToListAsync();
            }

            return View(vm);
        }

        // GET: Liste à valider (pour Directeur Métier)
        [Authorize]
        public async Task<IActionResult> ListeValidationDM(string? recherche = null, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDM"))
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                            d.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier ||
                            d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI);

            if (!await HasAdminScopeAsync())
                query = query.Where(d => d.DirecteurMetierId == userId);

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => (d.Titre != null && d.Titre.Contains(recherche)) ||
                                         d.Demandeur.Nom.Contains(recherche) ||
                                         d.Demandeur.Prenoms.Contains(recherche));

            query = query.OrderByDescending(d => d.DateSoumission);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            return View(paged.Items);
        }

        // GET: Validation DSI (pour DSI)
        [Authorize]
        public async Task<IActionResult> ListeValidationDSI(string? recherche = null, Guid? directionId = null, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await _db.DelegationsValidationDSI
                .AnyAsync(d => d.DelegueId == userId &&
                               d.EstActive &&
                               d.DateDebut <= DateTime.Now &&
                               d.DateFin >= DateTime.Now &&
                               !d.EstSupprime);

            if (!await CanHandleDsiValidationAsync(isDelegue))
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDSI);

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => (d.Titre != null && d.Titre.Contains(recherche)) ||
                                         d.Demandeur.Nom.Contains(recherche) ||
                                         d.Demandeur.Prenoms.Contains(recherche));

            if (directionId.HasValue)
                query = query.Where(d => d.DirectionId == directionId.Value);

            query = query.OrderByDescending(d => d.DateSoumission);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber          = paged.PageNumber;
            ViewBag.TotalPages          = paged.TotalPages;
            ViewBag.TotalCount          = paged.TotalCount;
            ViewBag.PageSize            = paged.PageSize;
            ViewBag.Recherche           = recherche;
            ViewBag.SelectedDirectionId = directionId;
            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .Select(d => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = d.Id.ToString(), Text = d.Libelle, Selected = directionId == d.Id
                }).ToListAsync();

            return View(paged.Items);
        }

        // GET: Historique des validations DSI
        [Authorize]
        public async Task<IActionResult> HistoriqueValidationsDSI(string? recherche = null, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await IsActiveDsiDelegateAsync(userId);
            if (!await CanHandleDsiValidationAsync(isDelegue))
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Where(d => d.StatutDemande == StatutDemande.ValideeParDSI ||
                            d.StatutDemande == StatutDemande.RejeteeParDSI ||
                            d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI ||
                            d.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI);

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => (d.Titre != null && d.Titre.Contains(recherche)) ||
                                         d.Demandeur.Nom.Contains(recherche) ||
                                         d.Demandeur.Prenoms.Contains(recherche));

            query = query.OrderByDescending(d => d.DateValidationDSI ?? d.DateSoumission);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            return View(paged.Items);
        }

        // GET: Détails demande
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            var demande = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .Include(d => d.DirecteurMetier)
                .Include(d => d.Annexes)
                .Include(d => d.Projet)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            if (!await CanAccessDemandeDetailsAsync(demande, userId))
                return Forbid();

            var canManageDemandes   = await CanManageDemandesBackofficeAsync();
            var canActAsDemandeur   = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Details") && demande.DemandeurId == userId;
            var canEditAsDemandeur  = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Edit") && demande.DemandeurId == userId;
            var canSubmitDemande    = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Soumettre") && demande.DemandeurId == userId;
            var canAddDocuments     = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "AjouterDocumentsComplementaires") && demande.DemandeurId == userId;
            var canDuplicateDemande = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "DupliquerDemande") && demande.DemandeurId == userId;
            var canActAsDm          = await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ValiderDM") && demande.DirecteurMetierId == userId;
            var isDsiDelegue        = await IsActiveDsiDelegateAsync(userId);
            var canActAsDsi         = await CanHandleDsiValidationAsync(isDsiDelegue);

            var backLinkAction = canManageDemandes
                ? nameof(ListeValidationDSI)
                : canActAsDm
                    ? nameof(ListeValidationDM)
                    : canActAsDemandeur
                        ? nameof(Index)
                        : string.Empty;

            var chefsProjetList = new List<Utilisateur>();
            if (canManageDemandes)
            {
                var chefsProjet = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToListAsync();

                var delegationsActives = await _db.DelegationsChefProjet
                    .Include(d => d.Delegue)
                    .Where(d => !d.EstSupprime && d.EstActive && d.DateDebut <= DateTime.Now && d.DateFin == null)
                    .Select(d => d.Delegue!)
                    .Where(u => !u.EstSupprime)
                    .ToListAsync();

                chefsProjetList = chefsProjet
                    .Union(delegationsActives)
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToList();

                ViewBag.ChefsProjet = chefsProjetList;
            }

            return View(new DemandeProjetDetailsViewModel
            {
                Demande            = demande,
                CanActAsDemandeur  = canActAsDemandeur,
                CanEditAsDemandeur = canEditAsDemandeur,
                CanSubmitDemande   = canSubmitDemande,
                CanAddDocuments    = canAddDocuments,
                CanDuplicateDemande = canDuplicateDemande,
                CanActAsDm         = canActAsDm,
                CanActAsDsi        = canActAsDsi,
                BackLinkAction     = backLinkAction,
                ChefsProjet        = chefsProjetList
            });
        }
    }
}
