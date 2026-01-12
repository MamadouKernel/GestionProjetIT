using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class Service : EntiteAudit
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Libelle { get; set; }
        public bool EstActive { get; set; } = true;
        
        public Guid DirectionId { get; set; }
        public Direction Direction { get; set; }
    }
}

