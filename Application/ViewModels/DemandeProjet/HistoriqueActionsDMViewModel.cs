using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class HistoriqueActionsDMViewModel
    {
        public IEnumerable<AuditLog> Logs { get; set; } = Enumerable.Empty<AuditLog>();
        public Dictionary<Guid, Domain.Models.DemandeProjet> Demandes { get; set; } = new();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int Total { get; set; }
    }
}
