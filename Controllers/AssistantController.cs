using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Web.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    /// <summary>
    /// Assistant scripté (sans LLM externe) : contexte projet en temps réel et
    /// génération de brouillons de bilan. La partie navigation/FAQ est gérée côté
    /// client en réutilisant les catalogues d'aide existants (wwwroot/js/site.js).
    /// </summary>
    [Authorize]
    public class AssistantController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IAssistantService _assistantService;
        private readonly IPermissionService _permissionService;
        private readonly IProjetQueryService _projetQuery;

        public AssistantController(
            ApplicationDbContext db,
            IAssistantService assistantService,
            IPermissionService permissionService,
            IProjetQueryService projetQuery)
        {
            _db = db;
            _assistantService = assistantService;
            _permissionService = permissionService;
            _projetQuery = projetQuery;
        }

        [HttpGet]
        public async Task<IActionResult> ProchainesEtapes(Guid projetId)
        {
            if (!await PeutVoirLeProjetAsync(projetId))
                return Forbid();

            var userId = User.GetUserIdOrThrow();
            var resultat = await _assistantService.ObtenirProchainesEtapesAsync(projetId, userId);
            if (resultat == null)
                return NotFound();

            return Json(resultat);
        }

        [HttpGet]
        public async Task<IActionResult> BrouillonBilan(Guid projetId)
        {
            if (!await PeutGererLaClotureAsync(projetId))
                return Forbid();

            var userId = User.GetUserIdOrThrow();
            var resultat = await _assistantService.GenererBrouillonBilanAsync(projetId, userId);
            if (resultat == null)
                return NotFound();

            return Json(resultat);
        }

        [HttpGet]
        public async Task<IActionResult> BrouillonAnalyse(Guid projetId)
        {
            if (!await PeutGererLAnalyseAsync(projetId))
                return Forbid();

            var userId = User.GetUserIdOrThrow();
            var resultat = await _assistantService.GenererBrouillonAnalyseAsync(projetId, userId);
            if (resultat == null)
                return NotFound();

            return Json(resultat);
        }

        [HttpGet]
        public async Task<IActionResult> BrouillonExecution(Guid projetId)
        {
            if (!await PeutGererLExecutionAsync(projetId))
                return Forbid();

            var userId = User.GetUserIdOrThrow();
            var resultat = await _assistantService.GenererBrouillonExecutionAsync(projetId, userId);
            if (resultat == null)
                return NotFound();

            return Json(resultat);
        }

        private async Task<bool> PeutGererLAnalyseAsync(Guid projetId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return false;

            var ui = await ConstruireUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> PeutGererLExecutionAsync(Guid projetId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return false;

            var ui = await ConstruireUiAsync(projet);
            return ui.CanEditExecution || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> PeutVoirLeProjetAsync(Guid projetId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return false;

            var ui = await ConstruireUiAsync(projet);
            return ui.CanViewProject;
        }

        private async Task<bool> PeutGererLaClotureAsync(Guid projetId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return false;

            var ui = await ConstruireUiAsync(projet);
            return ui.CanEditCloture || ui.HasDsiGovernanceAccess;
        }

        private async Task<Application.ViewModels.ProjetUiPermissions> ConstruireUiAsync(Domain.Models.Projet projet)
        {
            var userId = User.GetUserIdOrThrow();
            var currentUserDirectionId = await _projetQuery.GetUserDirectionIdAsync(userId);
            var isDemandeurProject = projet.DemandeProjetId != Guid.Empty &&
                await _db.DemandesProjets.AnyAsync(d => d.Id == projet.DemandeProjetId && d.DemandeurId == userId);

            return await ProjetUiPermissionBuilder.BuildAsync(
                _permissionService,
                User,
                projet,
                isReadOnly: false,
                isDemandeurProject: isDemandeurProject,
                currentUserDirectionId: currentUserDirectionId);
        }
    }
}
