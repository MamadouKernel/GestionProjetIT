using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Services(string? recherche = null, Guid? directionId = null, string? statut = null, int page = 1, int pageSize = 20)
        {
            var vm = await _serviceService.GetListAsync(recherche, directionId, statut, page, pageSize);
            PopulateServiceViewBag(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(string Code, string Libelle, string DirectionId)
        {
            var input  = new CreateServiceInput(Code, Libelle, DirectionId, true);
            var result = await _serviceService.CreateAsync(input);
            return await HandleServiceResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateService(Guid id, string Code, string Libelle, string DirectionId)
        {
            var input  = new UpdateServiceInput(id, Code, Libelle, DirectionId, true);
            var result = await _serviceService.UpdateAsync(input);
            return await HandleServiceResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(Guid id)
        {
            var result = await _serviceService.DeleteAsync(id);
            return await HandleServiceResultAsync(result);
        }

        // ── Helpers HTTP (présentation uniquement) ────────────────────────────
        private async Task<IActionResult> HandleServiceResultAsync(OperationResult result)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.Succeeded)
            {
                TempData["Success"] = result.SuccessMessage;
                return RedirectToAction(nameof(Services));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            var vm = await _serviceService.GetListAsync(null, null, null, 1, 20);
            PopulateServiceViewBag(vm);
            return View("Services", vm);
        }

        private void PopulateServiceViewBag(ServicesListViewModel vm)
        {
            ViewBag.PageNumber          = vm.PageNumber;
            ViewBag.TotalPages          = vm.TotalPages;
            ViewBag.TotalCount          = vm.TotalCount;
            ViewBag.PageSize            = vm.PageSize;
            ViewBag.Recherche           = vm.Recherche;
            ViewBag.SelectedDirectionId = vm.SelectedDirectionId;
            ViewBag.Statut              = vm.Statut;
            ViewBag.Directions          = vm.Directions;
        }
    }
}
