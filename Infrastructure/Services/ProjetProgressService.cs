using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class ProjetProgressService : IProjetProgressService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public ProjetProgressService(ApplicationDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        public async Task EnsureDataLoadedAsync(Projet projet)
        {
            var entry = _db.Entry(projet);

            if (!entry.Collection(p => p.Livrables).IsLoaded)
                await entry.Collection(p => p.Livrables).LoadAsync();

            if (!entry.Collection(p => p.TachesPlanning).IsLoaded)
                await entry.Collection(p => p.TachesPlanning).LoadAsync();

            if (!entry.Collection(p => p.LignesRaci).IsLoaded)
                await entry.Collection(p => p.LignesRaci).LoadAsync();

            if (!entry.Collection(p => p.LignesCommunication).IsLoaded)
                await entry.Collection(p => p.LignesCommunication).LoadAsync();

            if (!entry.Collection(p => p.LignesBudgetPlanification).IsLoaded)
                await entry.Collection(p => p.LignesBudgetPlanification).LoadAsync();

            if (!entry.Collection(p => p.DemandesCloture).IsLoaded)
                await entry.Collection(p => p.DemandesCloture).LoadAsync();

            if (!entry.Reference(p => p.PvKickOff).IsLoaded)
                await entry.Reference(p => p.PvKickOff).LoadAsync();

            if (!entry.Reference(p => p.CharteProjet).IsLoaded)
                await entry.Reference(p => p.CharteProjet).LoadAsync();

            if (!entry.Reference(p => p.FicheProjet).IsLoaded)
                await entry.Reference(p => p.FicheProjet).LoadAsync();
        }

        public async Task RecalculateAsync(Projet projet, bool persistChanges = false)
        {
            await EnsureDataLoadedAsync(projet);

            var automaticProgress = ComputeAutomaticProgress(projet);
            if (projet.PourcentageAvancement == automaticProgress)
            {
                return;
            }

            projet.PourcentageAvancement = automaticProgress;
            projet.DateModification = DateTime.UtcNow;
            projet.ModifiePar = _currentUserService.Matricule;

            if (persistChanges)
            {
                await _db.SaveChangesAsync();
            }
        }

        private static int ComputeAutomaticProgress(Projet projet)
        {
            if (projet.StatutProjet == StatutProjet.NonDemarre)
            {
                return 0;
            }

            if (projet.StatutProjet == StatutProjet.Cloture)
            {
                return 100;
            }

            if (projet.StatutProjet == StatutProjet.Annule)
            {
                return Math.Clamp(projet.PourcentageAvancement, 0, 99);
            }

            var livrables = projet.Livrables?
                .Where(l => !l.EstSupprime)
                .Select(l => l.TypeLivrable)
                .ToHashSet() ?? new HashSet<TypeLivrable>();

            var fiche = projet.FicheProjet;

            return projet.PhaseActuelle switch
            {
                PhaseProjet.AnalyseClarification => ComputeProgressBand(
                    0,
                    20,
                    CountCompletedChecks(
                        livrables.Contains(TypeLivrable.CharteProjet),
                        HasCompleteSignedCharte(projet),
                        projet.CharteValideeParDM,
                        projet.CharteValideeParDSI),
                    4),
                PhaseProjet.PlanificationValidation => ComputeProgressBand(
                    20,
                    40,
                    CountCompletedChecks(
                        livrables.Contains(TypeLivrable.PlanningDetaille),
                        livrables.Contains(TypeLivrable.Wbs),
                        livrables.Contains(TypeLivrable.MatriceRaci),
                        livrables.Contains(TypeLivrable.SchemaCommunication),
                        livrables.Contains(TypeLivrable.BudgetPrevisionnel),
                        livrables.Contains(TypeLivrable.PvKickOff),
                        projet.PlanningValideParDM),
                    7),
                PhaseProjet.ExecutionSuivi => ComputeExecutionProgress(projet),
                PhaseProjet.UatMep => ComputeProgressBand(
                    80,
                    95,
                    CountCompletedChecks(
                        fiche?.DateDebutRecette.HasValue == true &&
                        fiche.DateFinRecette.HasValue &&
                        !string.IsNullOrWhiteSpace(fiche.UtilisateursTesteurs) &&
                        !string.IsNullOrWhiteSpace(fiche.PerimetreTeste),
                        fiche?.DateMepPrevue.HasValue == true &&
                        !string.IsNullOrWhiteSpace(fiche.PlanMep) &&
                        !string.IsNullOrWhiteSpace(fiche.PlanRollback),
                        livrables.Contains(TypeLivrable.PvRecette),
                        projet.RecetteValidee,
                        projet.MepEffectuee,
                        fiche?.HypercareTermine == true),
                    6),
                PhaseProjet.ClotureLeconsApprises => ComputeProgressBand(
                    95,
                    99,
                    CountCompletedChecks(
                        HasClosureSummary(projet),
                        HasRunTransferReady(fiche),
                        GetActiveClosureRequest(projet) != null,
                        GetActiveClosureRequest(projet)?.StatutValidationDemandeur == StatutValidationCloture.Validee,
                        GetActiveClosureRequest(projet)?.StatutValidationDirecteurMetier == StatutValidationCloture.Validee,
                        GetActiveClosureRequest(projet)?.StatutValidationDSI == StatutValidationCloture.Validee),
                    6),
                _ => projet.PourcentageAvancement
            };
        }

        private static int ComputeExecutionProgress(Projet projet)
        {
            var activeTasks = projet.TachesPlanning?
                .Where(t => !t.EstSupprime)
                .ToList() ?? new List<TachePlanningProjet>();

            if (activeTasks.Count > 0)
            {
                var averageTaskProgress = activeTasks.Average(t => Math.Clamp(t.Avancement, 0, 100));
                return ComputeProgressBand(40, 80, averageTaskProgress, 100d);
            }

            var fiche = projet.FicheProjet;
            var completed = CountCompletedChecks(
                fiche?.DateDebutReelleExecution.HasValue == true,
                !string.IsNullOrWhiteSpace(fiche?.ActionsRealiseesExecution),
                !string.IsNullOrWhiteSpace(fiche?.ActionsAVenirExecution),
                !string.IsNullOrWhiteSpace(fiche?.SyntheseChargesExecution));

            return ComputeProgressBand(40, 80, completed, 4);
        }

        private static int CountCompletedChecks(params bool[] checks)
        {
            return checks.Count(check => check);
        }

        private static int ComputeProgressBand(int start, int end, int completed, int total)
        {
            if (total <= 0)
            {
                return start;
            }

            return ComputeProgressBand(start, end, completed, (double)total);
        }

        private static int ComputeProgressBand(int start, int end, double completed, double total)
        {
            if (total <= 0)
            {
                return start;
            }

            var ratio = Math.Clamp(completed / total, 0d, 1d);
            return Math.Clamp(start + (int)Math.Round((end - start) * ratio), start, end);
        }

        private static DemandeClotureProjet? GetActiveClosureRequest(Projet projet)
        {
            return projet.DemandesCloture?
                .Where(d => !d.EstSupprime)
                .OrderByDescending(d => d.DateDemande)
                .FirstOrDefault(d => !d.EstTerminee);
        }

        private static bool HasRunTransferReady(FicheProjet? fiche)
        {
            return fiche?.TransfertRunDocumentation == true &&
                   fiche.TransfertRunAcces &&
                   fiche.TransfertRunSupportInforme &&
                   fiche.TransfertRunExploitationPrete;
        }

        private static bool HasClosureSummary(Projet projet)
        {
            return !string.IsNullOrWhiteSpace(projet.BilanPerimetre) &&
                   !string.IsNullOrWhiteSpace(projet.BilanPlanning) &&
                   !string.IsNullOrWhiteSpace(projet.BilanBudget) &&
                   !string.IsNullOrWhiteSpace(projet.BilanReussites) &&
                   !string.IsNullOrWhiteSpace(projet.BilanDifficultes);
        }

        private static bool HasCompleteSignedCharte(Projet projet)
        {
            var hasSignedLivrable = projet.Livrables.Any(l =>
                !l.EstSupprime &&
                l.TypeLivrable == TypeLivrable.CharteProjetSignee);

            return hasSignedLivrable &&
                   projet.CharteProjet?.SignatureSponsor == true &&
                   projet.CharteProjet?.SignatureChefProjet == true;
        }
    }
}
