using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels
{
    public class AutorisationsViewModel
    {
        public List<RoleUtilisateur> Roles { get; set; } = new();
        public Dictionary<RoleUtilisateur, List<RolePermission>> PermissionsParRole { get; set; } = new();
        public List<VueInfo> VuesDisponibles { get; set; } = new();
        /// <summary>Nombre d'entrées en base qui seront supprimées par « Initialiser les permissions » (permission renommée/retirée du catalogue).</summary>
        public int EntreesObsoletesCount { get; set; }
    }
}
