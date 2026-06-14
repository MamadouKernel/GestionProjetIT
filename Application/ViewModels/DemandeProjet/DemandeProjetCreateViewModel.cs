using GestionProjects.Application.Common.Models;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetCreateViewModel
    {
        public DemandeProjetFormModel Demande { get; set; } = new();
        public IEnumerable<SelectOption> Directions { get; set; } = Enumerable.Empty<SelectOption>();
        public IEnumerable<SelectOption> DirecteursMetier { get; set; } = Enumerable.Empty<SelectOption>();
        public IEnumerable<SelectOption> AutresSponsors { get; set; } = Enumerable.Empty<SelectOption>();
        public bool HasMembresCodir { get; set; }
        public bool IsReadOnly { get; set; }
        public Guid? PreSelectedDirectionId { get; set; }
        public Guid? PreSelectedDirecteurMetierId { get; set; }
    }
}
