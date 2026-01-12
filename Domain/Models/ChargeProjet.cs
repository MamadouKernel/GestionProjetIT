using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Suivi des charges (prévisionnelles et réelles) par ressource et par période
    /// Selon le PRD : saisie hebdomadaire des charges réelles
    /// </summary>
    public class ChargeProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        /// <summary>
        /// Ressource (membre de l'équipe projet)
        /// </summary>
        public Guid RessourceId { get; set; }
        public Utilisateur Ressource { get; set; }

        /// <summary>
        /// Semaine concernée (début de semaine)
        /// </summary>
        public DateTime SemaineDebut { get; set; }

        /// <summary>
        /// Charge prévisionnelle en heures pour cette semaine
        /// </summary>
        public decimal ChargePrevisionnelle { get; set; }

        /// <summary>
        /// Charge réelle en heures pour cette semaine (saisie hebdomadaire)
        /// </summary>
        public decimal? ChargeReelle { get; set; }

        /// <summary>
        /// Date de saisie de la charge réelle
        /// </summary>
        public DateTime? DateSaisieChargeReelle { get; set; }

        /// <summary>
        /// Utilisateur qui a saisi la charge réelle
        /// </summary>
        public Guid? SaisieParId { get; set; }
        public Utilisateur? SaisiePar { get; set; }

        /// <summary>
        /// Commentaire sur la charge (justification d'écart, etc.)
        /// </summary>
        public string? Commentaire { get; set; }
    }
}

