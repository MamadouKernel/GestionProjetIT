using System.Security.Claims;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Models;

namespace GestionProjects.Web.Ui
{
    public static class ProjetUiPermissionBuilder
    {
        public static async Task<ProjetUiPermissions> BuildAsync(
            IPermissionService permissionService,
            ClaimsPrincipal user,
            Projet? projet,
            bool isReadOnly = false,
            bool isDemandeurProject = false,
            Guid? currentUserDirectionId = null)
        {
            var permissions = await permissionService.GetCurrentUserActivePermissionsAsync();
            var keys = new HashSet<string>(
                permissions.Select(p => $"{p.Controleur}::{p.Action}"),
                StringComparer.OrdinalIgnoreCase);

            var userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : Guid.Empty;

            var chefProjetId = projet?.ChefProjetId;
            var sponsorId = projet?.SponsorId;

            return new ProjetUiPermissions
            {
                UserId = userId,
                CurrentUserDirectionId = currentUserDirectionId,
                IsReadOnly = isReadOnly,
                IsDemandeurProject = isDemandeurProject,
                IsAssignedChefProjet = userId != Guid.Empty && chefProjetId.HasValue && chefProjetId.Value == userId,
                IsProjectSponsor = userId != Guid.Empty && sponsorId.HasValue && sponsorId.Value == userId,
                IsProjectInUserDirection = currentUserDirectionId.HasValue && projet?.DirectionId == currentUserDirectionId.Value,
                ActivePermissionKeys = keys
            };
        }
    }
}
