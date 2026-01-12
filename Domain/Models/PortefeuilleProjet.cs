using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Représente le portefeuille de projets DSI avec ses objectifs stratégiques, avantages et risques globaux
    /// </summary>
    public class PortefeuilleProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public string Nom { get; set; } = "Portefeuille de Projet DSI";
        public string? Description { get; set; }

        // Objectif Stratégique Global
        public string ObjectifStrategiqueGlobal { get; set; } = string.Empty;

        // Avantages Attendus du Portefeuille (stocké en JSON ou texte)
        public string AvantagesAttendus { get; set; } = string.Empty; // Liste séparée par des retours à la ligne

        // Risques et Mitigations Globaux (stocké en JSON ou texte)
        public string RisquesEtMitigations { get; set; } = string.Empty; // Format: "Risque: Mitigation" séparés par des retours à la ligne

        // Indicateur si c'est le portefeuille actif
        public bool EstActif { get; set; } = true;
    }
}

