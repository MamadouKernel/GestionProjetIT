using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class AutorisationsController : Controller
    {
        private readonly IAutorisationMatrixService _autorisationMatrix;
        private readonly ILogger<AutorisationsController> _logger;
        private readonly IPermissionService _permissionService;

        public AutorisationsController(
            IAutorisationMatrixService autorisationMatrix,
            ILogger<AutorisationsController> logger,
            IPermissionService permissionService)
        {
            _autorisationMatrix = autorisationMatrix;
            _logger = logger;
            _permissionService = permissionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                if (!await CanManagePermissionsAsync())
                {
                    return Forbid();
                }

                var viewModel = await _autorisationMatrix.BuildIndexAsync();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement des autorisations");
                TempData["Error"] = "Erreur lors du chargement des autorisations.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePermission(
            RoleUtilisateur role,
            string controleur,
            string action,
            bool estActif)
        {
            try
            {
                if (!await CanManagePermissionsAsync())
                {
                    return Json(new { success = false, message = "Acces refuse." });
                }

                var result = await _autorisationMatrix.UpdatePermissionAsync(
                    new UpdateRolePermissionInput(role, controleur, action, estActif));

                return Json(new
                {
                    success = result.Succeeded,
                    message = result.Succeeded
                        ? result.SuccessMessage
                        : result.Errors.FirstOrDefault()?.Message ?? "Erreur lors de la mise a jour"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise a jour de la permission");
                return Json(new { success = false, message = "Erreur lors de la mise a jour" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitialiserPermissions()
        {
            try
            {
                if (!await CanManagePermissionsAsync())
                {
                    return Forbid();
                }

                var result = await _autorisationMatrix.InitialiserPermissionsAsync();
                if (result.Succeeded)
                {
                    TempData["Success"] = result.SuccessMessage;
                }
                else
                {
                    TempData["Error"] = result.Errors.FirstOrDefault()?.Message
                        ?? "Erreur lors de l'initialisation des permissions.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'initialisation des permissions");
                TempData["Error"] = "Erreur lors de l'initialisation des permissions.";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<bool> CanManagePermissionsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Autorisations", "Index");
        }
    }
}
