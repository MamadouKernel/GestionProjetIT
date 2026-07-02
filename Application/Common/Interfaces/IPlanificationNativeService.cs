using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Persistance et génération de livrables pour la planification native d'un projet
    /// (planning Gantt, RACI, plan de communication, budget, PV de kick-off).
    /// Le contrôleur conserve l'autorisation, le chargement du projet et le rendu de vue ;
    /// ce service possède le parsing des payloads JSON, la synchronisation des résumés
    /// texte de la fiche projet, et la persistance des collections associées.
    /// </summary>
    public interface IPlanificationNativeService
    {
        Task<FicheProjet> GetOrCreateFicheProjetAsync(Guid projetId, Guid userId);

        List<PlanningTacheInputViewModel> ParsePlanningTaches(string? ganttPayload);
        List<RaciLigneInputViewModel> ParseRaciLignes(string? raciPayload);
        List<CommunicationLigneInputViewModel> ParseCommunicationLignes(string? communicationPayload);
        List<BudgetLigneInputViewModel> ParseBudgetLignes(string? budgetPayload);
        PvKickOffInputViewModel ParsePvKickOff(string? kickOffPayload);

        void SynchronizePlanningSummary(Projet projet, FicheProjet ficheProjet, IReadOnlyList<PlanningTacheInputViewModel> taskInputs);
        void SynchronizeRaciSummary(FicheProjet ficheProjet, IReadOnlyList<RaciLigneInputViewModel> lines);
        void SynchronizeCommunicationSummary(FicheProjet ficheProjet, IReadOnlyList<CommunicationLigneInputViewModel> lines);
        void SynchronizeBudgetSummary(FicheProjet ficheProjet, IReadOnlyList<BudgetLigneInputViewModel> lines);

        Task ReplacePlanningTasksAsync(Projet projet, Guid userId, IReadOnlyList<PlanningTacheInputViewModel> tasks);
        Task ReplaceRaciLinesAsync(Projet projet, IReadOnlyList<RaciLigneInputViewModel> lines);
        Task ReplaceCommunicationLinesAsync(Projet projet, IReadOnlyList<CommunicationLigneInputViewModel> lines);
        Task ReplaceBudgetLinesAsync(Projet projet, IReadOnlyList<BudgetLigneInputViewModel> lines);
        Task UpsertPvKickOffAsync(Projet projet, Guid userId, PvKickOffInputViewModel kickOff);

        /// <summary>Génère les livrables Excel natifs (planning, WBS, RACI, communication, budget, PV kick-off) manquants. Retourne les libellés générés/manquants.</summary>
        Task<(List<string> Generated, List<string> Missing)> GenerateLivrablesAsync(
            Projet projet,
            Guid userId,
            IReadOnlyList<PlanningTacheInputViewModel> planningTasks,
            IReadOnlyList<RaciLigneInputViewModel> raciLines,
            IReadOnlyList<CommunicationLigneInputViewModel> communicationLines,
            IReadOnlyList<BudgetLigneInputViewModel> budgetLines,
            PvKickOffInputViewModel kickOff);
    }
}
