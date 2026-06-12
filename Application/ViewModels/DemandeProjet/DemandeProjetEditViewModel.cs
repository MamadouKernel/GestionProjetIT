using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetEditViewModel
    {
        public Domain.Models.DemandeProjet Demande { get; set; } = null!;
        public IEnumerable<SelectListItem> Directions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DirecteursMetier { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> AutresSponsors { get; set; } = Enumerable.Empty<SelectListItem>();
        public bool HasMembresCodir { get; set; }
    }
}
