using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class TachePlanningProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public string CodeWbs { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Dependances { get; set; } = string.Empty;
        public string Commentaire { get; set; } = string.Empty;

        public DateTime DateDebutPrevue { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public int Avancement { get; set; }
        public int Ordre { get; set; }
        public bool EstJalon { get; set; }
    }
}
