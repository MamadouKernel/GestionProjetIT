using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class LigneBudgetPlanificationProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;
        public string Poste { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Montant { get; set; }
        public string Commentaire { get; set; } = string.Empty;
        public int Ordre { get; set; }
    }
}
