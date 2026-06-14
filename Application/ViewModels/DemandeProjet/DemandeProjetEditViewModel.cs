using GestionProjects.Application.Common.Models;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetEditViewModel
    {
        public DemandeProjetFormModel Demande { get; set; } = null!;
        public IEnumerable<SelectOption> Directions { get; set; } = Enumerable.Empty<SelectOption>();
        public IEnumerable<SelectOption> DirecteursMetier { get; set; } = Enumerable.Empty<SelectOption>();
        public IEnumerable<SelectOption> AutresSponsors { get; set; } = Enumerable.Empty<SelectOption>();
        public bool HasMembresCodir { get; set; }
    }
}
