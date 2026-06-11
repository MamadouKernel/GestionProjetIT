namespace GestionProjects.Application.ViewModels
{
    public class RaciLigneInputViewModel
    {
        public string CodeActivite { get; set; } = string.Empty;
        public string Activite { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Approbateur { get; set; } = string.Empty;
        public string Consulte { get; set; } = string.Empty;
        public string Informe { get; set; } = string.Empty;
        public int Ordre { get; set; }
    }
}
