using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin;

public class DirectionsListViewModel
{
    public List<Direction> Directions { get; set; } = new();
    public List<Utilisateur> DSIs { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? Recherche { get; set; }
}
