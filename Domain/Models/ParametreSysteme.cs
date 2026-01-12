using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class ParametreSysteme : EntiteAudit
    {
        public Guid Id { get; set; }

        public string Cle { get; set; }   
        public string Valeur { get; set; }
        public string Description { get; set; }
    }
}
