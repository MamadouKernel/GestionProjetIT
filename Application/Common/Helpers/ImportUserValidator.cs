using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Helpers
{
    /// <summary>
    /// Règles de validation métier pour l'import d'utilisateurs depuis Excel.
    /// Extraites du contrôleur pour être testables indépendamment.
    /// </summary>
    public static class ImportUserValidator
    {
        /// <summary>
        /// Valide les champs obligatoires d'une ligne d'import.
        /// Retourne un message d'erreur ou null si valide.
        /// </summary>
        public static string? ValidateRow(
            string matricule,
            string nom,
            string prenoms,
            string email)
        {
            if (string.IsNullOrWhiteSpace(matricule))
                return "Le matricule est requis.";
            if (string.IsNullOrWhiteSpace(nom))
                return "Le nom est requis.";
            if (string.IsNullOrWhiteSpace(prenoms))
                return "Les prénoms sont requis.";
            if (string.IsNullOrWhiteSpace(email))
                return "L'email est requis.";
            if (!ValidationHelper.IsValidEmail(email))
                return "Format d'email invalide.";

            return null;
        }

        /// <summary>
        /// Détermine le statut d'une ligne doublon selon le flag ignorerDoublons.
        /// </summary>
        public static string ResoudreSatutDoublon(bool ignorerDoublons)
            => ignorerDoublons ? "Ignoré" : "Erreur";

        /// <summary>
        /// Parse la chaîne de rôles CSV et retourne les rôles valides.
        /// Les rôles inconnus sont ignorés. Si aucun rôle valide, retourne Demandeur.
        /// </summary>
        public static (List<RoleUtilisateur> Roles, List<string> RolesInvalides) ParseRoles(string? rolesStr)
        {
            var roles = new List<RoleUtilisateur>();
            var rolesInvalides = new List<string>();

            if (!string.IsNullOrWhiteSpace(rolesStr))
            {
                foreach (var roleStr in rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var roleTrimmed = roleStr.Trim();
                    if (Enum.TryParse<RoleUtilisateur>(roleTrimmed, true, out var roleEnum))
                        roles.Add(roleEnum);
                    else
                        rolesInvalides.Add(roleTrimmed);
                }
            }

            if (!roles.Any())
                roles.Add(RoleUtilisateur.Demandeur);

            return (roles, rolesInvalides);
        }
    }
}
