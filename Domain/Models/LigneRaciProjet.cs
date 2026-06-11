using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class LigneRaciProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;
        public string CodeActivite { get; set; } = string.Empty;
        public string Activite { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Approbateur { get; set; } = string.Empty;
        public string Consulte { get; set; } = string.Empty;
        public string Informe { get; set; } = string.Empty;
        public int Ordre { get; set; }
    }
}
