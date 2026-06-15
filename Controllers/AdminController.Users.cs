using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        public async Task<IActionResult> Users(string? recherche = null, Guid? directionId = null, RoleUtilisateur? role = null, int page = 1, int pageSize = 5)
        {
            var vm = await _userService.GetListAsync(recherche, directionId, role, page, pageSize);
            PopulateUserViewBag(vm);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var details = await _userService.GetDetailsAsync(id);
            return details is null ? NotFound() : Json(details);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(string Matricule, string Nom, string Prenoms, string Email, string? DirectionId, string motDePasse, string confirmMotDePasse, string? Roles = null, bool PeutCreerDemandeProjet = true)
        {
            var input = new CreateUserInput(Matricule, Nom, Prenoms, Email, DirectionId, motDePasse, confirmMotDePasse, Roles, PeutCreerDemandeProjet);
            var result = await _userService.CreateAsync(input);
            return await HandleUserResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(Guid id, string Matricule, string Nom, string Prenoms, string Email, string? DirectionId, string? nouveauMotDePasse, string? confirmNouveauMotDePasse, string? Roles = null, bool PeutCreerDemandeProjet = true, ProfilRessource? ProfilRessource = null, decimal? CapaciteHebdomadaire = null)
        {
            var input = new UpdateUserInput(id, Matricule, Nom, Prenoms, Email, DirectionId, nouveauMotDePasse, confirmNouveauMotDePasse, Roles, PeutCreerDemandeProjet, ProfilRessource, CapaciteHebdomadaire);
            var result = await _userService.UpdateAsync(input);
            return await HandleUserResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var result = await _userService.DeleteAsync(id);
            return await HandleUserResultAsync(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Guid id, string nouveauMotDePasse)
        {
            var result = await _userService.ResetPasswordAsync(id, nouveauMotDePasse);

            if (result.IsNotFound)
                return NotFound();

            if (result.Succeeded)
                TempData["Success"] = result.SuccessMessage;
            else
                TempData["Error"] = result.Errors.FirstOrDefault()?.Message;

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenvoyerLienActivation(Guid id)
        {
            var result = await _userService.RenvoyerLienActivationAsync(id);

            if (result.IsNotFound)
                return NotFound();

            if (result.Succeeded)
                TempData["Success"] = result.SuccessMessage;
            else
                TempData["Error"] = result.Errors.FirstOrDefault()?.Message ?? "Echec du renvoi du lien d'activation.";

            return RedirectToAction(nameof(Users));
        }

        // ── Helpers HTTP (présentation uniquement) ────────────────────────────
        private async Task<IActionResult> HandleUserResultAsync(OperationResult result)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.Succeeded)
            {
                TempData["Success"] = result.SuccessMessage;
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            var vm = await _userService.GetListAsync(null, null, null, 1, 5);
            PopulateUserViewBag(vm);
            return View("Users", vm);
        }

        private void PopulateUserViewBag(UsersListViewModel vm)
        {
            ViewBag.PageNumber = vm.PageNumber;
            ViewBag.TotalPages = vm.TotalPages;
            ViewBag.TotalCount = vm.TotalCount;
            ViewBag.PageSize   = vm.PageSize;
        }
    }
}
