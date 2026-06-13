using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Delegations(
            string? tab = "dsi",
            string? rechercheDsi = null,
            string? rechercheChef = null,
            int pageDsi = 1, int pageChef = 1,
            int pageSize = 15)
        {
            var userId       = User.GetUserIdOrThrow();
            var hasFullScope = await HasFullAdminScopeAsync();

            var vm = await _delegationService.GetPageAsync(
                userId, hasFullScope, tab, rechercheDsi, rechercheChef, pageDsi, pageChef, pageSize);

            PopulateDelegationsViewBag(vm);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetDelegation(Guid id)
        {
            var result = await _delegationService.GetDsiAsync(id, User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationDetails(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelegation(string DSIId, string DelegueId, DateTime DateDebut, DateTime DateFin)
        {
            var input = new CreateDelegationDsiInput(DSIId, DelegueId, DateDebut, DateFin,
                ReadEstActive(), User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationResult(await _delegationService.CreateDsiAsync(input), "dsi");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDelegation(Guid id, string DSIId, string DelegueId, DateTime DateDebut, DateTime DateFin)
        {
            var input = new UpdateDelegationDsiInput(id, DSIId, DelegueId, DateDebut, DateFin,
                ReadEstActive(), User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationResult(await _delegationService.UpdateDsiAsync(input), "dsi");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelegation(Guid id)
        {
            var result = await _delegationService.DeleteDsiAsync(id, User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationResult(result, "dsi");
        }

        public async Task<IActionResult> DelegationsChefProjet()
        {
            var vm = await _delegationService.GetChefProjetPageAsync(User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetDelegationChefProjet(Guid id)
        {
            var result = await _delegationService.GetChefAsync(id, User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationDetails(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelegationChefProjet(string ProjetId, string DelegantId, string DelegueId, DateTime DateDebut)
        {
            var input = new CreateDelegationChefInput(ProjetId, DelegantId, DelegueId, DateDebut,
                ReadEstActive(), User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationResult(await _delegationService.CreateChefAsync(input), "chefprojet");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDelegationChefProjet(Guid id, string ProjetId, string DelegantId, string DelegueId, DateTime DateDebut)
        {
            var input = new UpdateDelegationChefInput(id, ProjetId, DelegantId, DelegueId, DateDebut,
                ReadEstActive(), User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationResult(await _delegationService.UpdateChefAsync(input), "chefprojet");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelegationChefProjet(Guid id)
        {
            var result = await _delegationService.DeleteChefAsync(id, User.GetUserIdOrThrow(), await HasFullAdminScopeAsync());
            return MapDelegationResult(result, "chefprojet");
        }

        // ── Helpers HTTP (présentation uniquement) ────────────────────────────
        private IActionResult MapDelegationDetails(DelegationDetailsResult result)
        {
            if (result.IsNotFound)  return NotFound();
            if (result.IsForbidden) return Forbid();
            return Json(result.Data);
        }

        private IActionResult MapDelegationResult(WorkflowResult result, string tab)
        {
            if (result.IsNotFound)  return NotFound();
            if (result.IsForbidden) return Forbid();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Delegations), new { tab });
        }

        private void PopulateDelegationsViewBag(DelegationsPageViewModel vm)
        {
            ViewBag.PageNumberDsi = vm.PageNumberDsi;
            ViewBag.TotalPagesDsi = vm.TotalPagesDsi;
            ViewBag.TotalCountDsi = vm.TotalCountDsi;
            ViewBag.PageSizeDsi   = vm.PageSizeDsi;
            ViewBag.RechercheDsi  = vm.RechercheDsi;
            ViewBag.DSIs          = vm.DSIs;
            ViewBag.DeleguesDSI   = vm.DeleguesDSI;
            ViewBag.PageNumberChef = vm.PageNumberChef;
            ViewBag.TotalPagesChef = vm.TotalPagesChef;
            ViewBag.TotalCountChef = vm.TotalCountChef;
            ViewBag.PageSizeChef   = vm.PageSizeChef;
            ViewBag.RechercheChef  = vm.RechercheChef;
            ViewBag.Projets        = vm.Projets;
            ViewBag.Delegants      = vm.Delegants;
            ViewBag.DeleguesChefProjet = vm.DeleguesChefProjet;
            ViewBag.CurrentUserId  = vm.CurrentUserId;
            ViewBag.CanAdminDelegations = vm.CanAdminDelegations;
            ViewBag.ActiveTab      = vm.ActiveTab;
        }
    }
}
