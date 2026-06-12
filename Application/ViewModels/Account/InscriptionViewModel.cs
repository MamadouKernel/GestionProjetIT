namespace GestionProjects.Application.ViewModels.Account
{
    public class DirectionSelectItem
    {
        public Guid Id { get; set; }
        public string Libelle { get; set; } = string.Empty;
    }

    public class InscriptionViewModel
    {
        public List<DirectionSelectItem> Directions { get; set; } = new();
    }
}
