using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
        /// <summary>Liste des demandes de création de compte — DM voit les siennes, DSI/AdminIT/Responsable voit tout.</summary>
        public async Task<IActionResult> DemandesCreationCompte()
        {
            var userId            = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();
            var canValidateAsDsi  = await CanValidateAccountRequestAsDsiAsync();
            var canValidateAsDm   = await CanValidateAccountRequestAsDmAsync();

            Guid? restrictToDmId;
            if (hasFullAdminScope || canValidateAsDsi)
                restrictToDmId = null;
            else if (canValidateAsDm)
                restrictToDmId = userId;
            else
                return Forbid();

            return View(await _demandeCompteService.GetListAsync(restrictToDmId));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ValiderDemandeCreationCompteDM(Guid id, string? commentaire)
        {
            var hasFullScope = await HasFullAdminScopeAsync();
            if (!hasFullScope && !await CanValidateAccountRequestAsDmAsync())
                return Forbid();

            var result = await _demandeCompteService.ValiderDmAsync(
                id, commentaire, User.GetUserIdOrThrow(), hasFullScope, NomActeurCourant());
            return MapWorkflowResult(result);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RefuserDemandeCreationCompteDM(Guid id, string? commentaire)
        {
            var hasFullScope = await HasFullAdminScopeAsync();
            if (!hasFullScope && !await CanValidateAccountRequestAsDmAsync())
                return Forbid();

            var result = await _demandeCompteService.RefuserDmAsync(
                id, commentaire, User.GetUserIdOrThrow(), hasFullScope, NomActeurCourant());
            return MapWorkflowResult(result);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ValiderDemandeCreationCompteDSI(Guid id, string? commentaire, RoleUtilisateur role = RoleUtilisateur.Demandeur)
        {
            if (!await HasFullAdminScopeAsync() && !await CanValidateAccountRequestAsDsiAsync())
                return Forbid();

            var result = await _demandeCompteService.ValiderDsiAsync(id, commentaire, role);
            return MapWorkflowResult(result);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RefuserDemandeCreationCompteDSI(Guid id, string? commentaire)
        {
            if (!await HasFullAdminScopeAsync() && !await CanValidateAccountRequestAsDsiAsync())
                return Forbid();

            var result = await _demandeCompteService.RefuserDsiAsync(id, commentaire, NomActeurCourant());
            return MapWorkflowResult(result);
        }

        // ── Helpers HTTP (présentation uniquement) ────────────────────────────
        private string NomActeurCourant() =>
            $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();

        private IActionResult MapWorkflowResult(WorkflowResult result)
        {
            if (result.IsNotFound)  return NotFound();
            if (result.IsForbidden) return Forbid();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(DemandesCreationCompte));
        }
    }
}
