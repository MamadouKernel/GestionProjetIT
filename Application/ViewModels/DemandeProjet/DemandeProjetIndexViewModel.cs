using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetIndexViewModel
    {
        public IEnumerable<Domain.Models.DemandeProjet> Demandes { get; set; } = Enumerable.Empty<Domain.Models.DemandeProjet>();
        public IEnumerable<SelectListItem> Directions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Demandeurs { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DirecteursMetier { get; set; } = Enumerable.Empty<SelectListItem>();

        // Pagination
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
    }
}
