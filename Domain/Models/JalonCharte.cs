using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Jalon de planification prévisionnelle dans la charte
    /// </summary>
    public class JalonCharte : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid CharteProjetId { get; set; }
        public CharteProjet CharteProjet { get; set; }

        public string Nom { get; set; } = string.Empty; // Ex: "Cahier de charge validé"
        public string Description { get; set; } = string.Empty;
        public string CriteresApprobation { get; set; } = string.Empty;
        public DateTime? DatePrevisionnelle { get; set; }
        public int Ordre { get; set; } // Pour l'ordre d'affichage
    }
}

