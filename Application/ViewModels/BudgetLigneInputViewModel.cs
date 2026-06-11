namespace GestionProjects.Application.ViewModels
{
    public class BudgetLigneInputViewModel
    {
        public string Poste { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Montant { get; set; }
        public string Commentaire { get; set; } = string.Empty;
        public int Ordre { get; set; }
    }
}
