using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Suivi des charges (prévisionnelles et réelles) par ressource et par période.
    /// </summary>
    public class ChargeProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        /// <summary>
        /// Ressource membre de l'équipe projet.
        /// </summary>
        public Guid RessourceId { get; set; }
        public Utilisateur Ressource { get; set; } = null!;

        /// <summary>
        /// Début de la semaine de saisie.
        /// </summary>
        public DateTime SemaineDebut { get; set; }

        /// <summary>
        /// Charge prévisionnelle en heures.
        /// </summary>
        public decimal ChargePrevisionnelle { get; set; }

        /// <summary>
        /// Charge réelle en heures.
        /// </summary>
        public decimal? ChargeReelle { get; set; }

        /// <summary>
        /// Date de saisie de la charge réelle.
        /// </summary>
        public DateTime? DateSaisieChargeReelle { get; set; }

        /// <summary>
        /// Utilisateur ayant saisi la charge réelle.
        /// </summary>
        public Guid? SaisieParId { get; set; }
        public Utilisateur? SaisiePar { get; set; }

        /// <summary>
        /// Commentaire libre sur l'écart ou la charge.
        /// </summary>
        public string? Commentaire { get; set; }

        /// <summary>
        /// Type d'activité.
        /// </summary>
        public string TypeActivite { get; set; } = string.Empty;

        /// <summary>
        /// Activité détaillée réalisée sur la semaine.
        /// </summary>
        public string Activite { get; set; } = string.Empty;

        /// <summary>
        /// Statut de validation hebdomadaire de la charge.
        /// </summary>
        public StatutValidationCharge StatutValidation { get; set; } = StatutValidationCharge.Brouillon;

        /// <summary>
        /// Date de soumission à validation.
        /// </summary>
        public DateTime? DateSoumissionValidation { get; set; }

        /// <summary>
        /// Date de validation, commentaire ou rejet.
        /// </summary>
        public DateTime? DateValidation { get; set; }

        /// <summary>
        /// Utilisateur ayant validé ou commenté la charge.
        /// </summary>
        public Guid? ValideeParId { get; set; }
        public Utilisateur? ValideePar { get; set; }

        /// <summary>
        /// Commentaire de validation ou de rejet.
        /// </summary>
        public string CommentaireValidation { get; set; } = string.Empty;
    }
}
