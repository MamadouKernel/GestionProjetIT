using System.Security.Claims;

namespace GestionProjects.Application.Common.Extensions
{
    /// <summary>
    /// Extensions pour ClaimsPrincipal avec validation minimale et explicite.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        public static Guid? GetUserId(this ClaimsPrincipal? principal)
        {
            if (principal == null)
            {
                return null;
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                return null;
            }

            return Guid.TryParse(userIdClaim, out var userId)
                ? userId
                : null;
        }

        public static Guid GetUserIdOrThrow(this ClaimsPrincipal? principal)
        {
            var userId = GetUserId(principal);
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("Utilisateur non authentifié ou ID invalide.");
            }

            return userId.Value;
        }
    }
}
