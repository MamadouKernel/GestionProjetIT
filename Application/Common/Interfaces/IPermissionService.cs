using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces
{
    public interface IPermissionService
    {
        /// <summary>
        /// Vérifie si l'utilisateur a la permission d'accéder à une action
        /// </summary>
        Task<bool> HasPermissionAsync(RoleUtilisateur role, string controleur, string action);
        
        /// <summary>
        /// Vérifie si l'utilisateur actuel a la permission d'accéder à une action
        /// </summary>
        Task<bool> CurrentUserHasPermissionAsync(string controleur, string action);
        
        /// <summary>
        /// Récupère toutes les permissions actives pour un rôle
        /// </summary>
        Task<List<(string Controleur, string Action)>> GetActivePermissionsAsync(RoleUtilisateur role);
        
        /// <summary>
        /// Récupère toutes les permissions actives pour l'utilisateur actuel
        /// </summary>
        Task<List<(string Controleur, string Action)>> GetCurrentUserActivePermissionsAsync();
    }
}
