using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Directions(string? recherche = null, string? statut = null, int page = 1, int pageSize = 20)
        {
            var vm = await _directionService.GetListAsync(recherche, statut, page, pageSize);
            PopulateDirectionViewBag(vm);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetDirectionCode(Guid id)
        {
            var code = await _directionService.GetCodeAsync(id);
            return code is null ? NotFound() : Json(new { code });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDirection(string Code, string Libelle, string? DSIId)
        {
            var input  = new CreateDirectionInput(Code, Libelle, DSIId, ReadEstActive());
            var result = await _directionService.CreateAsync(input);
            return await HandleDirectionResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDirection(Guid id, string Code, string Libelle, string? DSIId)
        {
            var input  = new UpdateDirectionInput(id, Code, Libelle, DSIId, ReadEstActive());
            var result = await _directionService.UpdateAsync(input);
            return await HandleDirectionResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDirection(Guid id)
        {
            var result = await _directionService.DeleteAsync(id);
            return await HandleDirectionResultAsync(result);
        }

        // ── Helpers HTTP (présentation uniquement) ────────────────────────────
        private bool ReadEstActive()
        {
            if (!Request.Form.ContainsKey("EstActive")) return true;
            var val = Request.Form["EstActive"].FirstOrDefault(v => v != "false");
            return val is "true" or "True";
        }

        private async Task<IActionResult> HandleDirectionResultAsync(OperationResult result)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.Succeeded)
            {
                TempData["Success"] = result.SuccessMessage;
                return RedirectToAction(nameof(Directions));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            var vm = await _directionService.GetListAsync(null, null, 1, 20);
            PopulateDirectionViewBag(vm);
            return View("Directions", vm);
        }

        private void PopulateDirectionViewBag(DirectionsListViewModel vm)
        {
            ViewBag.PageNumber = vm.PageNumber;
            ViewBag.TotalPages = vm.TotalPages;
            ViewBag.TotalCount = vm.TotalCount;
            ViewBag.PageSize   = vm.PageSize;
            ViewBag.Recherche  = vm.Recherche;
            ViewBag.Statut     = vm.Statut;
            ViewBag.DSIs       = vm.DSIs;
        }
    }
}
