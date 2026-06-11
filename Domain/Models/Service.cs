using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class Service : EntiteAudit
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public bool EstActive { get; set; } = true;
        
        public Guid DirectionId { get; set; }
        public Direction Direction { get; set; } = null!;
    }
}

