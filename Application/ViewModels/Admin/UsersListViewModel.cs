using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin;

public class UsersListViewModel
{
    public List<Utilisateur> Users { get; set; } = new();
    public List<Direction> Directions { get; set; } = new();
    /// <summary>
    /// Ids des directions ayant au moins un Directeur Métier rattaché et actif.
    /// Permet a la modale Creer/Editer un utilisateur d'afficher visuellement les
    /// directions sans DM sans pour autant les bloquer (l'AdminIT peut justement
    /// vouloir y affecter le premier DM).
    /// </summary>
    public HashSet<Guid> DirectionsAvecDm { get; set; } = new();
    public List<RoleUtilisateur> AllRoles { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? Recherche { get; set; }
    public Guid? SelectedDirectionId { get; set; }
    public RoleUtilisateur? SelectedRole { get; set; }
}
