using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class PortefeuilleViewModel
{
    public IEnumerable<Domain.Models.Projet> Projets { get; set; } = Enumerable.Empty<Domain.Models.Projet>();
    public PortefeuilleProjet Portefeuille { get; set; } = new();
    public IEnumerable<GestionProjects.Domain.Models.DemandeProjet> DemandesEnCours { get; set; } = Enumerable.Empty<GestionProjects.Domain.Models.DemandeProjet>();
}
