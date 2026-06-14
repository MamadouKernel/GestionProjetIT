using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels
{
    public class AutorisationsViewModel
    {
        public List<RoleUtilisateur> Roles { get; set; } = new();
        public Dictionary<RoleUtilisateur, List<RolePermission>> PermissionsParRole { get; set; } = new();
        public List<VueInfo> VuesDisponibles { get; set; } = new();
    }
}
