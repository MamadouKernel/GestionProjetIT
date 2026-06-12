using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Application.ViewModels.Projet;

public class ProjetIndexViewModel
{
    public IEnumerable<Domain.Models.Projet> Projets { get; set; } = Enumerable.Empty<Domain.Models.Projet>();

    // Filtres (disponibles uniquement pour les gestionnaires de portefeuille)
    public IEnumerable<SelectListItem> Directions { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> ChefsProjet { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Phases { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Statuts { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Etats { get; set; } = Enumerable.Empty<SelectListItem>();

    // Flags de vue
    public bool IsDemandeurOnlyView { get; set; }
    public bool IsDirecteurMetierProjectsView { get; set; }

    // Projets accessibles en lecture seule (pour DM sans être sponsor)
    public HashSet<Guid> ReadOnlyProjets { get; set; } = new();

    // Pagination
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 20;
}
