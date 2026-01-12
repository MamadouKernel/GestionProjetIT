using System.Security.Claims;

namespace GestionProjects.Application.Common.Extensions
{
    /// <summary>
    /// Extensions pour ClaimsPrincipal pour améliorer la sécurité et la validation
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Récupère l'ID utilisateur de manière sécurisée avec validation
        /// </summary>
        /// <param name="principal">Le ClaimsPrincipal</param>
        /// <returns>Le Guid de l'utilisateur ou null si invalide</returns>
        public static Guid? GetUserId(this ClaimsPrincipal? principal)
        {
            if (principal == null)
                return null;

            var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return null;

            if (Guid.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }

        /// <summary>
        /// Récupère l'ID utilisateur de manière sécurisée avec validation, lance une exception si invalide
        /// </summary>
        /// <param name="principal">Le ClaimsPrincipal</param>
        /// <returns>Le Guid de l'utilisateur</returns>
        /// <exception cref="UnauthorizedAccessException">Si l'utilisateur n'est pas authentifié ou l'ID est invalide</exception>
        public static Guid GetUserIdOrThrow(this ClaimsPrincipal? principal)
        {
            var userId = GetUserId(principal);
            if (!userId.HasValue)
                throw new UnauthorizedAccessException("Utilisateur non authentifié ou ID invalide.");

            return userId.Value;
        }

        /// <summary>
        /// Récupère le rôle de l'utilisateur
        /// </summary>
        public static string? GetUserRole(this ClaimsPrincipal? principal)
        {
            return principal?.FindFirstValue(ClaimTypes.Role);
        }
    }
}

