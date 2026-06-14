using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Application.ViewModels.DemandesAcces
{
    public class DemandesAccesIndexViewModel
    {
        public List<DemandeAccesAzureAd> Items { get; set; } = new();
        public IEnumerable<SelectListItem> Directions { get; set; } = Enumerable.Empty<SelectListItem>();
        public string? Recherche { get; set; }
        public StatutDemandeAcces? SelectedStatut { get; set; }
        public Guid? FocusId { get; set; }
        public int TotalCount { get; set; }

        // Pagination
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
    }
}
