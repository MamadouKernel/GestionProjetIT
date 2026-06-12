using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin
{
    public class GererRolesViewModel
    {
        public Utilisateur User { get; set; } = null!;
        public List<RoleUtilisateur> AllRoles { get; set; } = new();
    }
}
