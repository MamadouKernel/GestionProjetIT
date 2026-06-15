using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Avenant projet : demande de changement maîtrisée (périmètre / budget / délai)
    /// après que la charte et la planification ont été validées (baseline posée).
    /// Cycle : soumission -> validation Métier (DM/sponsor) -> validation DSI qui
    /// applique le changement au projet. Trace l'ancien et le nouveau pour l'audit.
    /// </summary>
    public class AvenantProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        /// <summary>Numéro séquentiel lisible par projet (AV-01, AV-02, …).</summary>
        public int Numero { get; set; }

        public TypeAvenant Type { get; set; }

        public string Titre { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;

        // Impact périmètre (descriptif)
        public string? DescriptionPerimetre { get; set; }

        // Impact budget (la valeur courante est captée à la soumission)
        public decimal? AncienBudget { get; set; }
        public decimal? NouveauBudget { get; set; }

        // Impact délai
        public DateTime? AncienneDateFinPrevue { get; set; }
        public DateTime? NouvelleDateFinPrevue { get; set; }

        public StatutAvenant Statut { get; set; } = StatutAvenant.EnAttenteValidationDM;

        // Initiateur
        public Guid DemandeParId { get; set; }
        public Utilisateur DemandePar { get; set; } = null!;
        public DateTime DateDemande { get; set; }

        // Validation Métier (Directeur Métier / sponsor)
        public Guid? ValideParDMId { get; set; }
        public Utilisateur? ValideParDMUtilisateur { get; set; }
        public DateTime? DateValidationDM { get; set; }

        // Validation DSI (applique le changement)
        public Guid? ValideParDSIId { get; set; }
        public Utilisateur? ValideParDSIUtilisateur { get; set; }
        public DateTime? DateValidationDSI { get; set; }

        public string CommentaireRejet { get; set; } = string.Empty;
        public DateTime? DateApplication { get; set; }
    }
}
