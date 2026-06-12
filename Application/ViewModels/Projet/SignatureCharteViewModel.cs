using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class SignatureCharteViewModel
{
    public CharteProjet Charte { get; set; } = null!;
    public string RoleSignataire { get; set; } = "CP";
    public Guid ProjetId { get; set; }
    public string NomSignataire { get; set; } = string.Empty;
}
