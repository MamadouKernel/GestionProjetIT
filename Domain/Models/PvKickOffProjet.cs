using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class PvKickOffProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;
        public DateTime? DateReunion { get; set; }
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
