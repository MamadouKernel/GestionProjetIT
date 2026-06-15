using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Bénéfice attendu d'un projet, défini pendant le cadrage puis évalué lors de la
    /// revue post-implémentation (réalisation des bénéfices). Ferme la boucle de la valeur :
    /// un projet ne se juge pas seulement « livré » mais « a-t-il produit la valeur promise ».
    /// </summary>
    public class BeneficeProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public string Libelle { get; set; } = string.Empty;
        public string Indicateur { get; set; } = string.Empty;
        public string ValeurCible { get; set; } = string.Empty;
        public DateTime? DateCibleRealisation { get; set; }

        public StatutBenefice Statut { get; set; } = StatutBenefice.Attendu;

        // Revue post-implémentation
        public string? ValeurRealisee { get; set; }
        public DateTime? DateRevue { get; set; }
        public string? CommentaireRevue { get; set; }
    }
}
