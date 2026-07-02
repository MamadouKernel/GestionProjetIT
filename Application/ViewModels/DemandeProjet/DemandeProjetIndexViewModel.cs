using GestionProjects.Application.Common.Models;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetIndexViewModel
    {
        public IEnumerable<Domain.Models.DemandeProjet> Demandes { get; set; } = Enumerable.Empty<Domain.Models.DemandeProjet>();
        public IEnumerable<SelectOption> Directions { get; set; } = Enumerable.Empty<SelectOption>();
        public IEnumerable<SelectOption> Demandeurs { get; set; } = Enumerable.Empty<SelectOption>();
        public IEnumerable<SelectOption> DirecteursMetier { get; set; } = Enumerable.Empty<SelectOption>();

        // Corbeille (réservé AdminIT)
        public bool CanGererCorbeille { get; set; }
        public bool AfficherSupprimees { get; set; }

        // Pagination
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
    }
}
