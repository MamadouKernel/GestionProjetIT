namespace GestionProjects.Application.ViewModels
{
    public class CommunicationLigneInputViewModel
    {
        public string Instance { get; set; } = string.Empty;
        public string Objectif { get; set; } = string.Empty;
        public string Frequence { get; set; } = string.Empty;
        public string Canal { get; set; } = string.Empty;
        public string Participants { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public bool EstCopil { get; set; }
        public int Ordre { get; set; }
    }
}
