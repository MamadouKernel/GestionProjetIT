using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GestionProjects.Web.Security
{
    public class PermissionMatrixAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<PermissionMatrixAuthorizationFilter> _logger;

        public PermissionMatrixAuthorizationFilter(
            IPermissionService permissionService,
            ILogger<PermissionMatrixAuthorizationFilter> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                return;
            }

            if (context.Filters.Any(f => f is IAllowAnonymousFilter) ||
                context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            {
                return;
            }

            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            if (!PermissionCatalog.IsManagedAction(controller, action))
            {
                return;
            }

            var allowed = await _permissionService.CurrentUserHasPermissionAsync(controller!, action!);
            if (allowed)
            {
                return;
            }

            _logger.LogWarning(
                "Acces refuse par la matrice de permissions pour {User} sur {Controller}.{Action}",
                user.Identity?.Name,
                controller,
                action);

            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
