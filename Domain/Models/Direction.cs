using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class Direction : EntiteAudit
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public bool EstActive { get; set; } = true;
        
        // DSI responsable de cette direction
        public Guid? DSIId { get; set; }
        public Utilisateur? DSI { get; set; }
        
        // Collection des services de cette direction
        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}
