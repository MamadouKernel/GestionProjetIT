using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Service de calcul automatique de l'indicateur RAG (Red/Amber/Green)
    /// pour le portefeuille de projets selon le PRD
    /// </summary>
    public interface IRAGCalculationService
    {
        /// <summary>
        /// Calcule l'indicateur RAG d'un projet basé sur :
        /// - Budget (écarts)
        /// - Planning (retards)
        /// - Risques (criticité)
        /// - Livrables (manquants)
        /// </summary>
        /// <param name="projet">Le projet à analyser</param>
        /// <returns>L'indicateur RAG calculé</returns>
        Task<IndicateurRAG> CalculerRAGAsync(Projet projet);

        /// <summary>
        /// Met à jour l'indicateur RAG de tous les projets actifs
        /// </summary>
        Task MettreAJourRAGTousProjetsAsync();
    }
}

