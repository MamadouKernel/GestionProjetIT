using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin;

public class ServicesListViewModel
{
    public List<Service> Services { get; set; } = new();
    public List<Direction> Directions { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? Recherche { get; set; }
    public Guid? SelectedDirectionId { get; set; }
    public string? Statut { get; set; }
}
