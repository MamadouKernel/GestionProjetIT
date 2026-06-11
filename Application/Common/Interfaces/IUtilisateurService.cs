using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Service métier pour la gestion des utilisateurs et de leurs rôles.
    /// Extrait de l'AdminController pour centraliser la logique et éviter la duplication.
    /// </summary>
    public interface IUtilisateurService
    {
        /// <summary>
        /// Parse une chaîne de rôles séparés par virgule et applique la règle d'exclusivité AdminIT.
        /// Si AdminIT est présent, seul AdminIT est retourné.
        /// Si la liste est vide, Demandeur est assigné par défaut.
        /// </summary>
        List<RoleUtilisateur> ParseSelectedRoles(string? roles);

        /// <summary>
        /// Synchronise les rôles d'un utilisateur : active les nouveaux, désactive les retirés.
        /// L'utilisateur doit être chargé avec Include(u => u.UtilisateurRoles).
        /// </summary>
        Task SynchronizeUserRolesAsync(Utilisateur user, IEnumerable<RoleUtilisateur> selectedRoles);

        /// <summary>
        /// Crée un nouvel utilisateur avec ses rôles. Retourne l'utilisateur créé.
        /// Lève ArgumentException si le matricule ou l'email existe déjà.
        /// </summary>
        Task<Utilisateur> CreateUserAsync(
            string matricule,
            string nom,
            string prenoms,
            string email,
            string motDePasse,
            Guid? directionId,
            IEnumerable<RoleUtilisateur> roles,
            bool peutCreerDemandeProjet = true,
            ProfilRessource? profilRessource = null,
            decimal capaciteHebdomadaire = 40);

        /// <summary>
        /// Met à jour un utilisateur existant. Retourne false si l'utilisateur n'est pas trouvé.
        /// </summary>
        Task<bool> UpdateUserAsync(
            Guid id,
            string matricule,
            string nom,
            string prenoms,
            string email,
            Guid? directionId,
            IEnumerable<RoleUtilisateur> roles,
            string? nouveauMotDePasse = null,
            bool peutCreerDemandeProjet = true,
            ProfilRessource? profilRessource = null,
            decimal? capaciteHebdomadaire = null);

        /// <summary>
        /// Vérifie si un matricule est déjà utilisé (hors l'utilisateur avec l'id exclu).
        /// </summary>
        Task<bool> MatriculeExisteAsync(string matricule, Guid? excludeUserId = null);

        /// <summary>
        /// Vérifie si un email est déjà utilisé (hors l'utilisateur avec l'id exclu).
        /// </summary>
        Task<bool> EmailExisteAsync(string email, Guid? excludeUserId = null);
    }
}
