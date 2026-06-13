using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> ListeRoles(string? recherche = null, Guid? directionId = null, RoleUtilisateur? role = null, int page = 1, int pageSize = 20)
        {
            var vm = await _roleService.GetListAsync(recherche, directionId, role, page, pageSize);
            ViewBag.PageNumber = vm.PageNumber;
            ViewBag.TotalPages = vm.TotalPages;
            ViewBag.TotalCount = vm.TotalCount;
            ViewBag.PageSize   = vm.PageSize;
            return View(vm);
        }

        public async Task<IActionResult> GererRoles(Guid id)
        {
            var vm = await _roleService.GetUserForRolesAsync(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(Guid id, string Roles)
        {
            var result = await _roleService.UpdateRolesAsync(id, Roles);

            if (result.NotFound)
                return NotFound();

            if (result.NoRolesSelected)
            {
                TempData["Error"] = "Veuillez sélectionner au moins un rôle.";
                return RedirectToAction(nameof(GererRoles), new { id });
            }

            if (result.InfoMessage is not null)
                TempData["Info"] = result.InfoMessage;

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction(nameof(ListeRoles));
        }
    }
}
