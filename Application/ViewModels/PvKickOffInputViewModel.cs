namespace GestionProjects.Application.ViewModels
{
    public class PvKickOffInputViewModel
    {
        public System.DateTime? DateReunion { get; set; }
        public string Heure { get; set; } = string.Empty;
        public string Lieu { get; set; } = string.Empty;
        public string Animateur { get; set; } = string.Empty;
        public string Objectifs { get; set; } = string.Empty;
        public string Participants { get; set; } = string.Empty;
        public string OrdreDuJour { get; set; } = string.Empty;
        public string Decisions { get; set; } = string.Empty;
        public string Actions { get; set; } = string.Empty;
        public string Commentaires { get; set; } = string.Empty;
    }
}
