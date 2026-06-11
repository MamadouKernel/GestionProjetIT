using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class DelegationValidationDSI : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid DSIId { get; set; }
        public Utilisateur? DSI { get; set; }

        public Guid DelegueId { get; set; }
        public Utilisateur? Delegue { get; set; }

        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }

        public bool EstActive { get; set; }
    }
}
