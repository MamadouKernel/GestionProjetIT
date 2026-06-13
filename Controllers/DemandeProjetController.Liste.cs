using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.DemandeProjet;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // GET: Mes demandes (pour Demandeur)
        [Authorize]
        public async Task<IActionResult> Index(Guid? directionId, Guid? demandeurId, Guid? directeurMetierId, int page = 1, int pageSize = 20)
        {
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Index"))
                return Forbid();

            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();

            var vm = await _demandeQueryService.GetIndexAsync(
                userId, canManageDemandes, directionId, demandeurId, directeurMetierId, page, pageSize);

            return View(vm);
        }

        // GET: Liste à valider (pour Directeur Métier)
        [Authorize]
        public async Task<IActionResult> ListeValidationDM(string? recherche = null, int page = 1, int pageSize = 20)
        {
            if (!await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDM"))
                return Forbid();

            var userId = User.GetUserIdOrThrow();
            var hasAdminScope = await HasAdminScopeAsync();

            var paged = await _demandeQueryService.GetListeValidationDMAsync(userId, hasAdminScope, recherche, page, pageSize);
            SetPaginationViewBag(paged);
            ViewBag.Recherche = recherche;

            return View(paged.Items);
        }

        // GET: Validation DSI (pour DSI)
        [Authorize]
        public async Task<IActionResult> ListeValidationDSI(string? recherche = null, Guid? directionId = null, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await IsActiveDsiDelegateAsync(userId);
            if (!await CanHandleDsiValidationAsync(isDelegue))
                return Forbid();

            var result = await _demandeQueryService.GetListeValidationDSIAsync(recherche, directionId, page, pageSize);
            SetPaginationViewBag(result.Paged);
            ViewBag.Recherche           = recherche;
            ViewBag.SelectedDirectionId = directionId;
            ViewBag.Directions          = result.Directions;

            return View(result.Paged.Items);
        }

        // GET: Historique des validations DSI
        [Authorize]
        public async Task<IActionResult> HistoriqueValidationsDSI(string? recherche = null, int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var isDelegue = await IsActiveDsiDelegateAsync(userId);
            if (!await CanHandleDsiValidationAsync(isDelegue))
                return Forbid();

            var paged = await _demandeQueryService.GetHistoriqueValidationsDSIAsync(recherche, page, pageSize);
            SetPaginationViewBag(paged);
            ViewBag.Recherche = recherche;

            return View(paged.Items);
        }

        // GET: Détails demande
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = User.GetUserIdOrThrow();
            var demande = await _demandeQueryService.GetForDetailsAsync(id);
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
                chefsProjetList = await _demandeQueryService.GetChefsProjetDisponiblesAsync();
                ViewBag.ChefsProjet = chefsProjetList;
            }

            return View(new DemandeProjetDetailsViewModel
            {
                Demande             = demande,
                CanActAsDemandeur   = canActAsDemandeur,
                CanEditAsDemandeur  = canEditAsDemandeur,
                CanSubmitDemande    = canSubmitDemande,
                CanAddDocuments     = canAddDocuments,
                CanDuplicateDemande = canDuplicateDemande,
                CanActAsDm          = canActAsDm,
                CanActAsDsi         = canActAsDsi,
                BackLinkAction      = backLinkAction,
                ChefsProjet         = chefsProjetList
            });
        }

        private void SetPaginationViewBag<T>(PagedResult<T> paged)
        {
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
        }
    }
}
