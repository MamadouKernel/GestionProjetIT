using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetCreateViewModel
    {
        public Domain.Models.DemandeProjet Demande { get; set; } = new();
        public IEnumerable<SelectListItem> Directions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DirecteursMetier { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> AutresSponsors { get; set; } = Enumerable.Empty<SelectListItem>();
        public bool HasMembresCodir { get; set; }
        public bool IsReadOnly { get; set; }
        public Guid? PreSelectedDirectionId { get; set; }
        public Guid? PreSelectedDirecteurMetierId { get; set; }
    }
}
