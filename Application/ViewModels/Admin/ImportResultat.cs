namespace GestionProjects.Application.ViewModels.Admin;

public class ImportResultat
{
    public int Ligne { get; set; }
    public string Matricule { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Statut { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
