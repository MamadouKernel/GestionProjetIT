using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Table de gestion des permissions par rôle
    /// Permet de contrôler l'accès aux vues/actions par rôle
    /// </summary>
    public class RolePermission : EntiteAudit
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Rôle concerné
        /// </summary>
        public RoleUtilisateur Role { get; set; }
        
        /// <summary>
        /// Contrôleur (ex: "DemandeProjet", "Projet", "Admin")
        /// </summary>
        public string Controleur { get; set; } = string.Empty;
        
        /// <summary>
        /// Action (ex: "Index", "Create", "Details")
        /// </summary>
        public string Action { get; set; } = string.Empty;
        
        /// <summary>
        /// Nom d'affichage de la permission
        /// </summary>
        public string NomAffichage { get; set; } = string.Empty;
        
        /// <summary>
        /// Description de la permission
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Catégorie pour regrouper les permissions (ex: "Demandes", "Projets", "Administration")
        /// </summary>
        public string Categorie { get; set; } = string.Empty;
        
        /// <summary>
        /// Icône Bootstrap Icons (ex: "bi-list-ul", "bi-folder")
        /// </summary>
        public string? Icone { get; set; }
        
        /// <summary>
        /// Ordre d'affichage dans la catégorie
        /// </summary>
        public int Ordre { get; set; }
        
        /// <summary>
        /// Indique si la permission est active
        /// </summary>
        public bool EstActif { get; set; } = true;
    }
}
