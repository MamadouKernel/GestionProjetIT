using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class FicheProjetPageViewModel
{
    public FicheProjet Fiche { get; set; } = null!;
    public Domain.Models.Projet Projet { get; set; } = null!;
}
