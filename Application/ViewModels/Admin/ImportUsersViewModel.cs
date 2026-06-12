namespace GestionProjects.Application.ViewModels.Admin
{
    public class ImportUsersViewModel
    {
        public List<GestionProjects.Controllers.ImportResultat> Resultats { get; set; } = new();
        public List<string> Erreurs { get; set; } = new();
    }
}
