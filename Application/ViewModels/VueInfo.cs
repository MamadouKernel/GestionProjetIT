namespace GestionProjects.Application.ViewModels;

public class VueInfo
{
    public string Controleur { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string NomAffichage { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Categorie { get; set; } = string.Empty;
    public string? Icone { get; set; }
    public int Ordre { get; set; }
}
