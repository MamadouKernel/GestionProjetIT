namespace GestionProjects.Application.ViewModels
{
    public class PlanningTacheInputViewModel
    {
        public string CodeWbs { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Dependances { get; set; } = string.Empty;
        public string Commentaire { get; set; } = string.Empty;
        public DateTime? DateDebutPrevue { get; set; }
        public DateTime? DateFinPrevue { get; set; }
        public int Avancement { get; set; }
        public int Ordre { get; set; }
        public bool EstJalon { get; set; }
    }
}
