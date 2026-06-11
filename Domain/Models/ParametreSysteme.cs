using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class ParametreSysteme : EntiteAudit
    {
        public Guid Id { get; set; }

        public string Cle { get; set; } = string.Empty;
        public string Valeur { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
