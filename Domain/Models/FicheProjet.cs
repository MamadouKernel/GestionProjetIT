using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Fiche Projet CIT - Version synthèse pour le suivi et la gouvernance
    /// </summary>
    public class FicheProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        // 1. Identification du projet (déjà dans Projet, mais on peut avoir des champs spécifiques)
        public string? TitreCourt { get; set; }
        public string? TitreLong { get; set; }

        // 2. Objectifs & description
        public string? ObjectifPrincipal { get; set; } // 3-5 lignes
        public string? ContexteProblemeAdresse { get; set; }
        public string? DescriptionSynthetique { get; set; }
        public string? ResultatsAttendus { get; set; }

        // 3. Périmètre
        public string? PerimetreInclus { get; set; } // Liste courte
        public string? PerimetreExclu { get; set; } // Liste courte

        // 4. Indicateurs clés
        public string? BeneficesAttendus { get; set; } // Opérationnels, financiers, qualité, conformité...
        public string? CriticiteUrgence { get; set; }
        public string? TypeProjet { get; set; } // SI / Infra / Applicatif / Process / Conformité...

        // 5. Planning synthétique (déjà dans Projet, mais on peut avoir des champs complémentaires)
        public string? ProchainJalon { get; set; }

        // 6. Principaux risques (déjà dans RisqueProjet, mais on peut avoir une synthèse)
        public string? SyntheseRisques { get; set; }

        // 7. Gouvernance et acteurs (déjà dans Projet et Membres, mais on peut avoir une synthèse)
        public string? EquipeProjet { get; set; } // Liste courte : Nom + Rôle
        public string? PartiesPrenantesCles { get; set; } // Ops, billing, support, fournisseurs...

        // 8. Livrables obligatoires - suivi rapide (déjà dans LivrableProjet, mais on peut avoir un suivi synthétique)
        public bool CharteProjetPresente { get; set; } = false;
        public bool WBSPlanningRACIBudgetPresent { get; set; } = false;
        public bool CRReunionsPresent { get; set; } = false;
        public bool CahierTestsPVRecettePVMEPPresent { get; set; } = false;
        public bool RapportLeconsApprisesPVCloturePresent { get; set; } = false;

        // 9. Budget (optionnel pour CIT)
        public decimal? BudgetPrevisionnel { get; set; }
        public decimal? BudgetConsomme { get; set; }
        public decimal? EcartsBudget { get; set; }
        
        /// <summary>
        /// Justification obligatoire des écarts budget si écart > seuil (10% par défaut)
        /// </summary>
        public string? JustificationEcartBudget { get; set; }
        
        /// <summary>
        /// Date de justification de l'écart
        /// </summary>
        public DateTime? DateJustificationEcart { get; set; }
        
        /// <summary>
        /// Utilisateur qui a justifié l'écart
        /// </summary>
        public Guid? JustificationParId { get; set; }
        public Utilisateur? JustificationPar { get; set; }

        // 10. Synthèse DSI
        public string? PointsForts { get; set; }
        public string? PointsVigilance { get; set; }
        public string? DecisionsAttendues { get; set; }
        public string? DemandesArbitrage { get; set; }

        // Informations de mise à jour
        public DateTime? DateDerniereMiseAJour { get; set; }
        public Guid? DerniereMiseAJourParId { get; set; }
        public Utilisateur? DerniereMiseAJourPar { get; set; }
    }
}

