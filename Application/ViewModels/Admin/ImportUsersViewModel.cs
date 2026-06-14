namespace GestionProjects.Application.ViewModels.Admin
{
    public class ImportUsersViewModel
    {
        public List<ImportResultat> Resultats { get; set; } = new();
        public List<string> Erreurs { get; set; } = new();
    }
}
