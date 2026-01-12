using GestionProjects.Application.Common.Constants;
using System.Security.Claims;

namespace GestionProjects.Application.Common.Extensions
{
    /// <summary>
    /// Extensions pour simplifier les vérifications de rôles
    /// </summary>
    public static class ClaimsPrincipalRoleExtensions
    {
        /// <summary>
        /// Vérifie si l'utilisateur a un accès complet (DSI, AdminIT, ResponsableSolutionsIT)
        /// </summary>
        public static bool HasFullAccess(this ClaimsPrincipal? principal)
        {
            if (principal == null)
                return false;

            return Roles.RolesAvecAccesComplet.Any(role => principal.IsInRole(role));
        }

        /// <summary>
        /// Vérifie si l'utilisateur a un rôle spécifique
        /// </summary>
        public static bool HasRole(this ClaimsPrincipal? principal, string role)
        {
            return principal?.IsInRole(role) ?? false;
        }

        /// <summary>
        /// Vérifie si l'utilisateur a l'un des rôles spécifiés
        /// </summary>
        public static bool HasAnyRole(this ClaimsPrincipal? principal, params string[] roles)
        {
            if (principal == null || roles == null || roles.Length == 0)
                return false;

            return roles.Any(role => principal.IsInRole(role));
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut créer des demandes
        /// </summary>
        public static bool CanCreateDemand(this ClaimsPrincipal? principal)
        {
            return principal.HasAnyRole(Roles.RolesPouvantCreerDemande);
        }
    }
}

