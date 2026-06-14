using GestionProjects.Application.Common.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class ProjetIndexViewModel
{
    public IEnumerable<Domain.Models.Projet> Projets { get; set; } = Enumerable.Empty<Domain.Models.Projet>();

    // Filtres (disponibles uniquement pour les gestionnaires de portefeuille)
    public IEnumerable<SelectOption> Directions { get; set; } = Enumerable.Empty<SelectOption>();
    public IEnumerable<SelectOption> ChefsProjet { get; set; } = Enumerable.Empty<SelectOption>();
    public IEnumerable<SelectOption> Phases { get; set; } = Enumerable.Empty<SelectOption>();
    public IEnumerable<SelectOption> Statuts { get; set; } = Enumerable.Empty<SelectOption>();
    public IEnumerable<SelectOption> Etats { get; set; } = Enumerable.Empty<SelectOption>();

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
