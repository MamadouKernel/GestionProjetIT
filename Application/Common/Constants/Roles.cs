namespace GestionProjects.Application.Common.Constants
{
    /// <summary>
    /// Constantes pour les rôles utilisateurs - évite les magic strings
    /// </summary>
    public static class Roles
    {
        public const string Demandeur = "Demandeur";
        public const string DirecteurMetier = "DirecteurMetier";
        public const string DSI = "DSI";
        public const string AdminIT = "AdminIT";
        public const string ChefDeProjet = "ChefDeProjet";
        public const string ResponsableSolutionsIT = "ResponsableSolutionsIT";

        /// <summary>
        /// Rôles ayant accès à tous les projets
        /// </summary>
        public static readonly string[] RolesAvecAccesComplet = 
        {
            DSI,
            AdminIT,
            ResponsableSolutionsIT
        };

        /// <summary>
        /// Rôles pouvant créer des demandes
        /// </summary>
        public static readonly string[] RolesPouvantCreerDemande = 
        {
            Demandeur,
            DSI,
            AdminIT
        };
    }
}

