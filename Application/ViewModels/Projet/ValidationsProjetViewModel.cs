using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class ValidationsProjetViewModel
{
    public IEnumerable<Domain.Models.Projet> Projets { get; set; } = Enumerable.Empty<Domain.Models.Projet>();
    public bool CanValidateAsDsi { get; set; }

    // Pagination
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 20;
}
