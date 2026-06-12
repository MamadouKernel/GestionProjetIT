using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class CharteProjetPageViewModel
{
    public CharteProjet Charte { get; set; } = null!;
    public Domain.Models.Projet Projet { get; set; } = null!;
    public List<Utilisateur> Utilisateurs { get; set; } = new();
    public LivrableProjet? CharteSigneeLivrable { get; set; }
    public DossierSignatureProjet? DossierSignatureCharte { get; set; }
}
