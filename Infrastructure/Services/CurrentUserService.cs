using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GestionProjects.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Retourne le login de l'utilisateur connecté (HttpContext.User.Identity.Name)
        /// Sinon retourne "SYSTEM".
        /// </summary>
        public string Matricule
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    // On essaie Identity.Name
                    if (!string.IsNullOrWhiteSpace(user.Identity.Name))
                        return user.Identity.Name;

                    // Sinon on essaie le claim NameIdentifier (rare mais possible)
                    var claim = user.FindFirst(ClaimTypes.NameIdentifier);
                    if (claim != null)
                        return claim.Value;
                }

                // Pas de contexte utilisateur
                return "SYSTEM";
            }
        }

    }
}
