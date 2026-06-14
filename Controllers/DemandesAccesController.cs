using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.ViewModels.DemandesAcces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class DemandesAccesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IPermissionService _permissionService;
        private readonly IDemandeAccesWorkflowService _demandeAccesWorkflow;

        public DemandesAccesController(
            ApplicationDbContext db,
            IPermissionService permissionService,
            IDemandeAccesWorkflowService demandeAccesWorkflow)
        {
            _db = db;
            _permissionService = permissionService;
            _demandeAccesWorkflow = demandeAccesWorkflow;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? recherche = null,
            StatutDemandeAcces? statut = null,
            Guid? focusId = null,
            int page = 1, int pageSize = 15)
        {
            if (!await CanManageAccessRequestsAsync())
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = _db.DemandesAccesAzureAd
                .Include(d => d.DirectionDetectee)
                .Include(d => d.TraitePar)
                .AsQueryable();

            if (focusId.HasValue)
            {
                query = query.Where(d => d.Id == focusId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(recherche))
            {
                query = query.Where(d =>
                    d.Nom.Contains(recherche) ||
                    d.Prenoms.Contains(recherche) ||
                    d.Email.Contains(recherche) ||
                    d.Matricule.Contains(recherche));
            }

            if (!focusId.HasValue && statut.HasValue)
            {
                query = query.Where(d => d.Statut == statut.Value);
            }

            query = query.OrderBy(d => d.Statut).ThenByDescending(d => d.DateCreation);

            var paged = await query.ToPagedResultAsync(page, pageSize);

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .Select(d => new SelectOption(d.Id.ToString(), d.Libelle, false, false))
                .ToListAsync();

            var vm = new DemandesAccesIndexViewModel
            {
                Items          = paged.Items,
                Directions     = directions,
                Recherche      = recherche,
                SelectedStatut = statut,
                FocusId        = focusId,
                TotalCount     = paged.TotalCount,
                PageNumber     = paged.PageNumber,
                TotalPages     = paged.TotalPages,
                PageSize       = paged.PageSize
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approuver(Guid id, Guid? directionId, string? commentaire, RoleUtilisateur role = RoleUtilisateur.Demandeur)
        {
            if (!await CanManageAccessRequestsAsync())
            {
                return Forbid();
            }

            var result = await _demandeAccesWorkflow.ApprouverAsync(
                new ApprouverDemandeAccesInput(id, directionId, commentaire, role, User.GetUserIdOrThrow()));
            return RedirectWithWorkflowResult(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rejeter(Guid id, string commentaire)
        {
            if (!await CanManageAccessRequestsAsync())
            {
                return Forbid();
            }

            var result = await _demandeAccesWorkflow.RejeterAsync(
                new RejeterDemandeAccesInput(id, commentaire, User.GetUserIdOrThrow()));
            return RedirectWithWorkflowResult(result);
        }

        private async Task<bool> CanManageAccessRequestsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("DemandesAcces", "Index");
        }

        private IActionResult RedirectWithWorkflowResult(DemandeAccesWorkflowResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                TempData["Error"] = result.ErrorMessage;
            }
            else if (!string.IsNullOrWhiteSpace(result.InfoMessage))
            {
                TempData["Info"] = result.InfoMessage;
            }
            else
            {
                TempData["Success"] = result.SuccessMessage;
            }

            return result.FocusId.HasValue
                ? RedirectToAction(nameof(Index), new { focusId = result.FocusId.Value })
                : RedirectToAction(nameof(Index));
        }
    }
}
