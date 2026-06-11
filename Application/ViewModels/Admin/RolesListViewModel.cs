using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin;

public class RolesListViewModel
{
    public List<Utilisateur> Users { get; set; } = new();
    public List<Direction> Directions { get; set; } = new();
    public List<RoleUtilisateur> AllRoles { get; set; } = new();
    public Dictionary<RoleUtilisateur, int> RoleCounts { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? Recherche { get; set; }
    public Guid? SelectedDirectionId { get; set; }
    public RoleUtilisateur? SelectedRole { get; set; }
}
