using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Security;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Infrastructure.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GestionProjects.Controllers
{
    [Authorize]
    public partial class ProjetController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IAuditService _auditService;
        private readonly IPdfService _pdfService;
        private readonly IExcelService _excelService;
        private readonly IWordService _wordService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly ILivrableValidationService _livrableValidationService;
        private readonly IRAGCalculationService _ragCalculationService;
        private readonly ICacheService _cacheService;
        private readonly IUatValidationService _uatValidation;
        private readonly ICollaborationProjetService _collaboration;
        private readonly IElectronicSignatureService _electronicSignature;
        private readonly IProjetQueryService _projetQuery;
        private readonly IClotureProjetWorkflowService _clotureWorkflow;
        private readonly ICharteProjetWorkflowService _charteWorkflow;

        public ProjetController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IAuditService auditService,
            IPdfService pdfService,
            IExcelService excelService,
            IWordService wordService,
            ICurrentUserService currentUserService,
            IPermissionService permissionService,
            INotificationService notificationService,
            ILivrableValidationService livrableValidationService,
            IRAGCalculationService ragCalculationService,
            ICacheService cacheService,
            IUatValidationService uatValidation,
            ICollaborationProjetService collaboration,
            IElectronicSignatureService electronicSignature,
            IProjetQueryService projetQuery,
            IClotureProjetWorkflowService clotureWorkflow,
            ICharteProjetWorkflowService charteWorkflow)
        {
            _db = db;
            _fileStorage = fileStorage;
            _auditService = auditService;
            _pdfService = pdfService;
            _excelService = excelService;
            _wordService = wordService;
            _currentUserService = currentUserService;
            _permissionService = permissionService;
            _notificationService = notificationService;
            _livrableValidationService = livrableValidationService;
            _ragCalculationService = ragCalculationService;
            _cacheService = cacheService;
            _uatValidation = uatValidation;
            _collaboration = collaboration;
            _electronicSignature = electronicSignature;
            _projetQuery = projetQuery;
            _clotureWorkflow = clotureWorkflow;
            _charteWorkflow = charteWorkflow;
        }
        private Task<bool> CurrentUserHasPermissionAsync(string controleur, string action)
            => _permissionService.CurrentUserHasPermissionAsync(controleur, action);

        /// <summary>
        /// Délègue à IProjetQueryService. Conservé pour compatibilité interne — à migrer progressivement.
        /// </summary>
        private Task<Guid?> GetCurrentUserDirectionIdAsync(Guid userId)
            => _projetQuery.GetUserDirectionIdAsync(userId);

        private async Task<bool> HasAdminScopeAsync()
        {
            return await CurrentUserHasPermissionAsync("Admin", "Users");
        }

        private async Task<bool> HasPortfolioGovernanceAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "Portefeuille");
        }

        private async Task<bool> HasChefProjetWorkflowAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "UpdateProgress") ||
                   await CurrentUserHasPermissionAsync("Projet", "ValiderAnalyse") ||
                   await CurrentUserHasPermissionAsync("Projet", "EditPlanification");
        }

        private async Task<bool> HasDmWorkflowAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "ValidationsProjet") ||
                   await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM");
        }

        private async Task<bool> HasDemandeurProjectAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDemandeur");
        }

        private async Task<ProjetUiPermissions> BuildProjectUiAsync(Projet projet, bool isReadOnly = false)
        {
            var userId = User.GetUserIdOrThrow();
            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            var isDemandeurProject = projet.DemandeProjet?.DemandeurId == userId;

            return await ProjetUiPermissionBuilder.BuildAsync(
                _permissionService,
                User,
                projet,
                isReadOnly: isReadOnly,
                isDemandeurProject: isDemandeurProject,
                currentUserDirectionId: currentUserDirectionId);
        }

        private async Task<bool> CanManageProjectAsChefProjetOrAdminAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanViewProjectAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanViewProject;
        }

        private async Task<bool> CanManageAnalyseAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanEditFicheProjetAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCharte || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanManagePlanificationAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditPlanification;
        }

        private async Task<bool> CanManageExecutionAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditExecution;
        }

        private async Task<bool> CanManageUatAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditUat;
        }

        private async Task<bool> CanManageClotureAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCloture;
        }

        private async Task<bool> CanManageCollaborationAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCollaboration;
        }

        private async Task<bool> CanUpdateProjectProgressAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanUpdateProgress || ui.CanForceStatus;
        }

        private async Task<bool> CanManageProjectMembersAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanEditTechnicalCommentAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditTechnicalComment;
        }

        private async Task<bool> CanValidateClotureDmAsync(DemandeClotureProjet demande, Guid userId)
        {
            if (!await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM"))
            {
                return false;
            }

            if (demande.Projet.SponsorId == userId)
            {
                return true;
            }

            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            return currentUserDirectionId.HasValue && demande.Projet.DirectionId == currentUserDirectionId.Value;
        }

        private async Task<bool> CanValidateClotureDsiAsync(Guid userId)
        {
            return await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDSI") &&
                   await CanValidateCharteAsDsiAsync(userId);
        }

        private async Task<FicheProjet> GetOrCreateFicheProjetAsync(Guid projetId, Guid userId)
        {
            var fiche = await _db.FicheProjets.FirstOrDefaultAsync(f => f.ProjetId == projetId && !f.EstSupprime);
            if (fiche != null)
            {
                return fiche;
            }

            fiche = new FicheProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                DateDerniereMiseAJour = DateTime.Now,
                DerniereMiseAJourParId = userId
            };

            _db.FicheProjets.Add(fiche);
            return fiche;
        }

        private async Task EnsureProjectProgressDataLoadedAsync(Projet projet)
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

        private async Task RecalculateProjectProgressAsync(Projet projet, bool persistChanges = false)
        {
            await EnsureProjectProgressDataLoadedAsync(projet);

            var automaticProgress = ComputeAutomaticProgress(projet);
            if (projet.PourcentageAvancement == automaticProgress)
            {
                return;
            }

            projet.PourcentageAvancement = automaticProgress;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            if (persistChanges)
            {
                await _db.SaveChangesAsync();
            }
        }

        private static int ComputeAutomaticProgress(Projet projet)
        {
            if (projet.StatutProjet == StatutProjet.Cloture || projet.StatutProjet == StatutProjet.Annule)
            {
                return 100;
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

        private List<PlanningTacheInputViewModel> ParsePlanningTaches(string? ganttPayload)
        {
            if (string.IsNullOrWhiteSpace(ganttPayload))
            {
                return new List<PlanningTacheInputViewModel>();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tasks = JsonSerializer.Deserialize<List<PlanningTacheInputViewModel>>(ganttPayload, options)
                ?? new List<PlanningTacheInputViewModel>();

            var filtered = new List<PlanningTacheInputViewModel>();
            for (var index = 0; index < tasks.Count; index++)
            {
                var task = tasks[index];
                var isCompletelyEmpty = string.IsNullOrWhiteSpace(task.Libelle)
                    && string.IsNullOrWhiteSpace(task.CodeWbs)
                    && !task.DateDebutPrevue.HasValue
                    && !task.DateFinPrevue.HasValue
                    && string.IsNullOrWhiteSpace(task.Responsable)
                    && string.IsNullOrWhiteSpace(task.Dependances)
                    && string.IsNullOrWhiteSpace(task.Commentaire)
                    && task.Avancement == 0
                    && !task.EstJalon;

                if (isCompletelyEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(task.Libelle))
                    throw new InvalidOperationException($"La tâche #{index + 1} doit avoir un libellé.");

                if (!task.DateDebutPrevue.HasValue || !task.DateFinPrevue.HasValue)
                    throw new InvalidOperationException($"La tâche \"{task.Libelle}\" doit avoir une date de début et une date de fin.");

                if (task.DateFinPrevue.Value.Date < task.DateDebutPrevue.Value.Date)
                    throw new InvalidOperationException($"La tâche \"{task.Libelle}\" a une date de fin antérieure à sa date de début.");

                task.CodeWbs = string.IsNullOrWhiteSpace(task.CodeWbs) ? $"{filtered.Count + 1}" : task.CodeWbs.Trim();
                task.Libelle = task.Libelle.Trim();
                task.Responsable = task.Responsable?.Trim() ?? string.Empty;
                task.Dependances = task.Dependances?.Trim() ?? string.Empty;
                task.Commentaire = task.Commentaire?.Trim() ?? string.Empty;
                task.Avancement = Math.Clamp(task.Avancement, 0, 100);
                task.Ordre = filtered.Count;
                filtered.Add(task);
            }

            return filtered;
        }

        private async Task ReplacePlanningTasksAsync(Projet projet, Guid userId, IReadOnlyList<PlanningTacheInputViewModel> tasks)
        {
            var existingTasks = await _db.TachesPlanningProjets
                .Where(t => t.ProjetId == projet.Id && !t.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingTasks)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            if (tasks.Count == 0)
            {
                return;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var task in tasks)
            {
                _db.TachesPlanningProjets.Add(new TachePlanningProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    CodeWbs = task.CodeWbs,
                    Libelle = task.Libelle,
                    Responsable = task.Responsable,
                    Dependances = task.Dependances,
                    Commentaire = task.Commentaire,
                    DateDebutPrevue = task.DateDebutPrevue!.Value.Date,
                    DateFinPrevue = task.DateFinPrevue!.Value.Date,
                    Avancement = Math.Clamp(task.Avancement, 0, 100),
                    Ordre = task.Ordre,
                    EstJalon = task.EstJalon,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private static string FormatPlanningTaskSummary(TachePlanningProjet task)
        {
            var suffix = task.EstJalon ? " [jalon]" : string.Empty;
            return $"{task.CodeWbs} - {task.Libelle} ({task.DateDebutPrevue:dd/MM/yyyy} -> {task.DateFinPrevue:dd/MM/yyyy}){suffix}";
        }

        private void SynchronizePlanningSummary(Projet projet, FicheProjet ficheProjet, IReadOnlyList<PlanningTacheInputViewModel> taskInputs)
        {
            if (taskInputs.Count == 0)
            {
                return;
            }

            var taskEntities = taskInputs
                .Select(t => new TachePlanningProjet
                {
                    CodeWbs = t.CodeWbs,
                    Libelle = t.Libelle,
                    Responsable = t.Responsable,
                    Dependances = t.Dependances,
                    Commentaire = t.Commentaire,
                    DateDebutPrevue = t.DateDebutPrevue!.Value.Date,
                    DateFinPrevue = t.DateFinPrevue!.Value.Date,
                    Avancement = t.Avancement,
                    Ordre = t.Ordre,
                    EstJalon = t.EstJalon
                })
                .OrderBy(t => t.DateDebutPrevue)
                .ThenBy(t => t.Ordre)
                .ToList();

            var nextMilestone = taskEntities
                .Where(t => t.EstJalon && t.Avancement < 100)
                .OrderBy(t => t.DateDebutPrevue)
                .FirstOrDefault()
                ?? taskEntities
                    .Where(t => t.Avancement < 100)
                    .OrderBy(t => t.DateDebutPrevue)
                    .FirstOrDefault();

            var milestoneTasks = taskEntities.Where(t => t.EstJalon).OrderBy(t => t.DateDebutPrevue).ToList();

            ficheProjet.ProchainJalon = nextMilestone == null
                ? ficheProjet.ProchainJalon
                : $"{nextMilestone.CodeWbs} - {nextMilestone.Libelle} ({nextMilestone.DateDebutPrevue:dd/MM/yyyy})";
            ficheProjet.JalonsPrincipaux = string.Join(Environment.NewLine,
                (milestoneTasks.Any() ? milestoneTasks : taskEntities.Take(6))
                .Select(FormatPlanningTaskSummary));
            ficheProjet.DecoupageLotsTravail = string.Join(Environment.NewLine, taskEntities.Select(FormatPlanningTaskSummary));
            ficheProjet.PlanificationRessources = string.Join(Environment.NewLine,
                taskEntities
                    .Where(t => !string.IsNullOrWhiteSpace(t.Responsable))
                    .GroupBy(t => t.Responsable)
                    .Select(g => $"{g.Key} : {string.Join(", ", g.Select(t => t.Libelle))}"));

            projet.DateDebut = taskEntities.Min(t => t.DateDebutPrevue);
            projet.DateFinPrevue = taskEntities.Max(t => t.DateFinPrevue);
        }

        private List<RaciLigneInputViewModel> ParseRaciLignes(string? raciPayload)
        {
            if (string.IsNullOrWhiteSpace(raciPayload))
            {
                return new List<RaciLigneInputViewModel>();
            }

            var lines = JsonSerializer.Deserialize<List<RaciLigneInputViewModel>>(raciPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<RaciLigneInputViewModel>();

            var filtered = new List<RaciLigneInputViewModel>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var isEmpty = string.IsNullOrWhiteSpace(line.Activite)
                    && string.IsNullOrWhiteSpace(line.CodeActivite)
                    && string.IsNullOrWhiteSpace(line.Responsable)
                    && string.IsNullOrWhiteSpace(line.Approbateur)
                    && string.IsNullOrWhiteSpace(line.Consulte)
                    && string.IsNullOrWhiteSpace(line.Informe);

                if (isEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Activite))
                    throw new InvalidOperationException($"La ligne RACI #{index + 1} doit préciser une activité.");

                line.CodeActivite = line.CodeActivite?.Trim() ?? string.Empty;
                line.Activite = line.Activite.Trim();
                line.Responsable = line.Responsable?.Trim() ?? string.Empty;
                line.Approbateur = line.Approbateur?.Trim() ?? string.Empty;
                line.Consulte = line.Consulte?.Trim() ?? string.Empty;
                line.Informe = line.Informe?.Trim() ?? string.Empty;
                line.Ordre = filtered.Count;
                filtered.Add(line);
            }

            return filtered;
        }

        private List<CommunicationLigneInputViewModel> ParseCommunicationLignes(string? communicationPayload)
        {
            if (string.IsNullOrWhiteSpace(communicationPayload))
            {
                return new List<CommunicationLigneInputViewModel>();
            }

            var lines = JsonSerializer.Deserialize<List<CommunicationLigneInputViewModel>>(communicationPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CommunicationLigneInputViewModel>();

            var filtered = new List<CommunicationLigneInputViewModel>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var isEmpty = string.IsNullOrWhiteSpace(line.Instance)
                    && string.IsNullOrWhiteSpace(line.Objectif)
                    && string.IsNullOrWhiteSpace(line.Frequence)
                    && string.IsNullOrWhiteSpace(line.Canal)
                    && string.IsNullOrWhiteSpace(line.Participants)
                    && string.IsNullOrWhiteSpace(line.Responsable)
                    && !line.EstCopil;

                if (isEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Instance))
                    throw new InvalidOperationException($"La ligne communication #{index + 1} doit préciser une instance.");

                line.Instance = line.Instance.Trim();
                line.Objectif = line.Objectif?.Trim() ?? string.Empty;
                line.Frequence = line.Frequence?.Trim() ?? string.Empty;
                line.Canal = line.Canal?.Trim() ?? string.Empty;
                line.Participants = line.Participants?.Trim() ?? string.Empty;
                line.Responsable = line.Responsable?.Trim() ?? string.Empty;
                line.Ordre = filtered.Count;
                filtered.Add(line);
            }

            return filtered;
        }

        private List<BudgetLigneInputViewModel> ParseBudgetLignes(string? budgetPayload)
        {
            if (string.IsNullOrWhiteSpace(budgetPayload))
            {
                return new List<BudgetLigneInputViewModel>();
            }

            var lines = JsonSerializer.Deserialize<List<BudgetLigneInputViewModel>>(budgetPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<BudgetLigneInputViewModel>();

            var filtered = new List<BudgetLigneInputViewModel>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var isEmpty = string.IsNullOrWhiteSpace(line.Poste)
                    && string.IsNullOrWhiteSpace(line.Description)
                    && line.Montant == 0
                    && string.IsNullOrWhiteSpace(line.Commentaire);

                if (isEmpty)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.Poste))
                    throw new InvalidOperationException($"La ligne budget #{index + 1} doit préciser un poste.");

                if (line.Montant < 0)
                    throw new InvalidOperationException($"La ligne budget \"{line.Poste}\" ne peut pas avoir un montant négatif.");

                line.Poste = line.Poste.Trim();
                line.Description = line.Description?.Trim() ?? string.Empty;
                line.Commentaire = line.Commentaire?.Trim() ?? string.Empty;
                line.Ordre = filtered.Count;
                filtered.Add(line);
            }

            return filtered;
        }

        private PvKickOffInputViewModel ParsePvKickOff(string? kickOffPayload)
        {
            if (string.IsNullOrWhiteSpace(kickOffPayload))
            {
                return new PvKickOffInputViewModel();
            }

            var model = JsonSerializer.Deserialize<PvKickOffInputViewModel>(kickOffPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PvKickOffInputViewModel();

            model.Heure = model.Heure?.Trim() ?? string.Empty;
            model.Lieu = model.Lieu?.Trim() ?? string.Empty;
            model.Animateur = model.Animateur?.Trim() ?? string.Empty;
            model.Objectifs = model.Objectifs?.Trim() ?? string.Empty;
            model.Participants = model.Participants?.Trim() ?? string.Empty;
            model.OrdreDuJour = model.OrdreDuJour?.Trim() ?? string.Empty;
            model.Decisions = model.Decisions?.Trim() ?? string.Empty;
            model.Actions = model.Actions?.Trim() ?? string.Empty;
            model.Commentaires = model.Commentaires?.Trim() ?? string.Empty;
            return model;
        }

        private static bool HasKickOffData(PvKickOffInputViewModel? model)
        {
            return model != null
                && (model.DateReunion.HasValue
                    || !string.IsNullOrWhiteSpace(model.Heure)
                    || !string.IsNullOrWhiteSpace(model.Lieu)
                    || !string.IsNullOrWhiteSpace(model.Animateur)
                    || !string.IsNullOrWhiteSpace(model.Objectifs)
                    || !string.IsNullOrWhiteSpace(model.Participants)
                    || !string.IsNullOrWhiteSpace(model.OrdreDuJour)
                    || !string.IsNullOrWhiteSpace(model.Decisions)
                    || !string.IsNullOrWhiteSpace(model.Actions)
                    || !string.IsNullOrWhiteSpace(model.Commentaires));
        }

        private async Task ReplaceRaciLinesAsync(Projet projet, IReadOnlyList<RaciLigneInputViewModel> lines)
        {
            var existingLines = await _db.LignesRaciProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLines)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var line in lines)
            {
                _db.LignesRaciProjets.Add(new LigneRaciProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    CodeActivite = line.CodeActivite,
                    Activite = line.Activite,
                    Responsable = line.Responsable,
                    Approbateur = line.Approbateur,
                    Consulte = line.Consulte,
                    Informe = line.Informe,
                    Ordre = line.Ordre,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private async Task ReplaceCommunicationLinesAsync(Projet projet, IReadOnlyList<CommunicationLigneInputViewModel> lines)
        {
            var existingLines = await _db.LignesCommunicationProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLines)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var line in lines)
            {
                _db.LignesCommunicationProjets.Add(new LigneCommunicationProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    Instance = line.Instance,
                    Objectif = line.Objectif,
                    Frequence = line.Frequence,
                    Canal = line.Canal,
                    Participants = line.Participants,
                    Responsable = line.Responsable,
                    EstCopil = line.EstCopil,
                    Ordre = line.Ordre,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private async Task ReplaceBudgetLinesAsync(Projet projet, IReadOnlyList<BudgetLigneInputViewModel> lines)
        {
            var existingLines = await _db.LignesBudgetPlanificationProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLines)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var createdBy = _currentUserService.Matricule ?? "SYSTEM";
            foreach (var line in lines)
            {
                _db.LignesBudgetPlanificationProjets.Add(new LigneBudgetPlanificationProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    Poste = line.Poste,
                    Description = line.Description,
                    Montant = line.Montant,
                    Commentaire = line.Commentaire,
                    Ordre = line.Ordre,
                    DateCreation = DateTime.Now,
                    CreePar = createdBy
                });
            }
        }

        private async Task UpsertPvKickOffAsync(Projet projet, Guid userId, PvKickOffInputViewModel kickOff)
        {
            var existing = await _db.PvKickOffProjets
                .FirstOrDefaultAsync(pv => pv.ProjetId == projet.Id && !pv.EstSupprime);

            if (!HasKickOffData(kickOff))
            {
                if (existing != null)
                {
                    existing.EstSupprime = true;
                    existing.DateModification = DateTime.Now;
                    existing.ModifiePar = _currentUserService.Matricule;
                }

                return;
            }

            if (existing == null)
            {
                existing = new PvKickOffProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                };
                _db.PvKickOffProjets.Add(existing);
            }

            existing.DateReunion = kickOff.DateReunion?.Date;
            existing.Heure = kickOff.Heure;
            existing.Lieu = kickOff.Lieu;
            existing.Animateur = kickOff.Animateur;
            existing.Objectifs = kickOff.Objectifs;
            existing.Participants = kickOff.Participants;
            existing.OrdreDuJour = kickOff.OrdreDuJour;
            existing.Decisions = kickOff.Decisions;
            existing.Actions = kickOff.Actions;
            existing.Commentaires = kickOff.Commentaires;
            existing.DateModification = DateTime.Now;
            existing.ModifiePar = _currentUserService.Matricule;
        }

        private void SynchronizeRaciSummary(FicheProjet ficheProjet, IReadOnlyList<RaciLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                ficheProjet.RaciParActivite = string.Empty;
                return;
            }

            ficheProjet.RaciParActivite = string.Join(Environment.NewLine,
                lines.Select(l => $"{(string.IsNullOrWhiteSpace(l.CodeActivite) ? string.Empty : $"{l.CodeActivite} - ")}{l.Activite} | R: {l.Responsable} | A: {l.Approbateur} | C: {l.Consulte} | I: {l.Informe}"));
        }

        private void SynchronizeCommunicationSummary(FicheProjet ficheProjet, IReadOnlyList<CommunicationLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            ficheProjet.FrequenceReunions = string.Join(" ; ", lines.Select(l => $"{l.Instance}: {l.Frequence}").Where(v => !v.EndsWith(": ")));
            ficheProjet.ParticipantsReunions = string.Join(Environment.NewLine,
                lines.Select(l => $"{l.Instance}: {l.Participants}").Where(v => !v.EndsWith(": ")));
            ficheProjet.CanalCommunication = string.Join(" ; ",
                lines.Select(l => $"{l.Instance}: {l.Canal}").Where(v => !v.EndsWith(": ")));
            ficheProjet.CopilPrevu = lines.Any(l => l.EstCopil);
        }

        private void SynchronizeBudgetSummary(FicheProjet ficheProjet, IReadOnlyList<BudgetLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            ficheProjet.BudgetPrevisionnel = lines.Sum(l => l.Montant);
            ficheProjet.CommentaireBudgetPlanification = string.Join(Environment.NewLine,
                lines.Select(l => $"{l.Poste}: {l.Montant:N2} FCFA{(string.IsNullOrWhiteSpace(l.Commentaire) ? string.Empty : $" - {l.Commentaire}")}"));
        }

        private async Task<(List<string> Generated, List<string> Missing)> GenerateNativePlanificationLivrablesAsync(
            Projet projet,
            Guid userId,
            IReadOnlyList<PlanningTacheInputViewModel> planningTasks,
            IReadOnlyList<RaciLigneInputViewModel> raciLines,
            IReadOnlyList<CommunicationLigneInputViewModel> communicationLines,
            IReadOnlyList<BudgetLigneInputViewModel> budgetLines,
            PvKickOffInputViewModel kickOff)
        {
            var generated = new List<string>();
            var missing = new List<string>();
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var planningEntities = planningTasks
                .Select((task, index) => new TachePlanningProjet
                {
                    CodeWbs = task.CodeWbs,
                    Libelle = task.Libelle,
                    Responsable = task.Responsable,
                    DateDebutPrevue = task.DateDebutPrevue ?? DateTime.Today,
                    DateFinPrevue = task.DateFinPrevue ?? task.DateDebutPrevue ?? DateTime.Today,
                    Avancement = task.Avancement,
                    EstJalon = task.EstJalon,
                    Dependances = task.Dependances,
                    Commentaire = task.Commentaire,
                    Ordre = index
                })
                .ToList();

            if (planningEntities.Any())
            {
                var planningBytes = await _excelService.GeneratePlanningDetailleExcelAsync(projet, planningEntities);
                var wbsBytes = await _excelService.GenerateWbsExcelAsync(projet, planningEntities);

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.PlanningDetaille,
                    $"PlanningDetaille_{projet.CodeProjet}_{timestamp}.xlsx",
                    planningBytes,
                    "Généré automatiquement depuis le planning interactif.");
                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.Wbs,
                    $"WBS_{projet.CodeProjet}_{timestamp}.xlsx",
                    wbsBytes,
                    "Généré automatiquement depuis le planning interactif.");

                generated.Add("Planning détaillé");
                generated.Add("WBS");
            }
            else
            {
                missing.Add("Planning détaillé / WBS");
            }

            if (raciLines.Any())
            {
                var raciBytes = await _excelService.GenerateMatriceRaciExcelAsync(projet, raciLines
                    .Select((line, index) => new LigneRaciProjet
                    {
                        CodeActivite = line.CodeActivite,
                        Activite = line.Activite,
                        Responsable = line.Responsable,
                        Approbateur = line.Approbateur,
                        Consulte = line.Consulte,
                        Informe = line.Informe,
                        Ordre = index
                    })
                    .ToList());

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.MatriceRaci,
                    $"MatriceRaci_{projet.CodeProjet}_{timestamp}.xlsx",
                    raciBytes,
                    "Généré automatiquement depuis la matrice RACI native.");
                generated.Add("Matrice RACI");
            }
            else
            {
                missing.Add("Matrice RACI");
            }

            if (communicationLines.Any())
            {
                var communicationBytes = await _excelService.GenerateSchemaCommunicationExcelAsync(projet, communicationLines
                    .Select((line, index) => new LigneCommunicationProjet
                    {
                        Instance = line.Instance,
                        Objectif = line.Objectif,
                        Frequence = line.Frequence,
                        Canal = line.Canal,
                        Participants = line.Participants,
                        Responsable = line.Responsable,
                        EstCopil = line.EstCopil,
                        Ordre = index
                    })
                    .ToList());

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.SchemaCommunication,
                    $"SchemaCommunication_{projet.CodeProjet}_{timestamp}.xlsx",
                    communicationBytes,
                    "Généré automatiquement depuis le plan de communication natif.");
                generated.Add("Schéma de communication");
            }
            else
            {
                missing.Add("Schéma de communication");
            }

            if (budgetLines.Any())
            {
                var budgetBytes = await _excelService.GenerateBudgetPrevisionnelExcelAsync(projet, budgetLines
                    .Select((line, index) => new LigneBudgetPlanificationProjet
                    {
                        Poste = line.Poste,
                        Description = line.Description,
                        Montant = line.Montant,
                        Commentaire = line.Commentaire,
                        Ordre = index
                    })
                    .ToList(), projet.FicheProjet);

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.BudgetPrevisionnel,
                    $"BudgetPrevisionnel_{projet.CodeProjet}_{timestamp}.xlsx",
                    budgetBytes,
                    "Généré automatiquement depuis le budget natif.");
                generated.Add("Budget prévisionnel");
            }
            else if (projet.FicheProjet?.BudgetPrevisionnel.HasValue == true && projet.FicheProjet.BudgetPrevisionnel.Value > 0)
            {
                var budgetBytes = await _excelService.GenerateBudgetPrevisionnelExcelAsync(projet, Array.Empty<LigneBudgetPlanificationProjet>(), projet.FicheProjet);

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.BudgetPrevisionnel,
                    $"BudgetPrevisionnel_{projet.CodeProjet}_{timestamp}.xlsx",
                    budgetBytes,
                    "Généré automatiquement depuis le budget natif.");
                generated.Add("Budget prévisionnel");
            }
            else
            {
                missing.Add("Budget prévisionnel");
            }

            if (HasKickOffData(kickOff))
            {
                var kickOffBytes = await _excelService.GeneratePvKickOffExcelAsync(projet, new PvKickOffProjet
                {
                    DateReunion = kickOff.DateReunion,
                    Heure = kickOff.Heure,
                    Lieu = kickOff.Lieu,
                    Animateur = kickOff.Animateur,
                    Objectifs = kickOff.Objectifs,
                    Participants = kickOff.Participants,
                    OrdreDuJour = kickOff.OrdreDuJour,
                    Decisions = kickOff.Decisions,
                    Actions = kickOff.Actions,
                    Commentaires = kickOff.Commentaires
                });

                await ReplaceGeneratedPlanningLivrableAsync(
                    projet,
                    userId,
                    TypeLivrable.PvKickOff,
                    $"PVKickOff_{projet.CodeProjet}_{timestamp}.xlsx",
                    kickOffBytes,
                    "Généré automatiquement depuis le PV de kick-off natif.");
                generated.Add("PV de kick-off");
            }
            else
            {
                missing.Add("PV de kick-off");
            }

            return (generated, missing);
        }

        private static byte[] BuildCsvBytes(IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(';', row.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        }

        // Helper method: Vérifie si un utilisateur peut agir comme chef de projet
        private async Task<bool> CanActAsChefProjetAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> IsActiveDsiDelegateAsync(Guid userId)
        {
            return await _db.DelegationsValidationDSI.AnyAsync(d =>
                d.DelegueId == userId &&
                d.EstActive &&
                d.DateDebut <= DateTime.Now &&
                d.DateFin >= DateTime.Now &&
                !d.EstSupprime);
        }

        private async Task<bool> CanValidateCharteAsDirecteurMetierAsync(Projet projet, Guid userId)
        {
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderCharteDM"))
            {
                return false;
            }

            if (projet.SponsorId == userId)
            {
                return true;
            }

            var userDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            return userDirectionId.HasValue && projet.DirectionId == userDirectionId.Value;
        }

        private async Task<bool> CanValidateCharteAsDsiAsync(Guid userId)
        {
            if (await CurrentUserHasPermissionAsync("Projet", "ValiderCharteDSI"))
            {
                return true;
            }

            return await IsActiveDsiDelegateAsync(userId);
        }

        private static bool AreRequiredCharteSignaturesCompleted(DossierSignatureProjet dossier)
        {
            var requiredSignataires = dossier.Signataires
                .Where(s => s.Role == RoleSignataireProjet.Sponsor || s.Role == RoleSignataireProjet.ChefDeProjet)
                .ToList();

            return requiredSignataires.Count == 2 &&
                   requiredSignataires.All(s => s.Statut == StatutSignataireDossierSignature.Signe);
        }

        private static void ResetCharteValidationState(Projet projet)
        {
            projet.CharteValideeParDM = false;
            projet.DateCharteValideeParDM = null;
            projet.CharteValideeParDMId = null;
            projet.CommentaireRefusCharteDM = null;

            projet.CharteValideeParDSI = false;
            projet.DateCharteValideeParDSI = null;
            projet.CharteValideeParDSIId = null;
            projet.CommentaireRefusCharteDSI = null;

            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
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

        private static List<string> BuildAnalyseBlockingItems(Projet projet, LivrableValidationResult validationLivrables)
        {
            var blocages = new List<string>();

            foreach (var livrable in validationLivrables.LivrablesManquants.Distinct())
            {
                blocages.Add($"Livrable manquant : {GetLivrableDisplayName(livrable)}");
            }

            var hasSignedLivrable = projet.Livrables.Any(l =>
                !l.EstSupprime &&
                l.TypeLivrable == TypeLivrable.CharteProjetSignee);

            if (!hasSignedLivrable && validationLivrables.LivrablesManquants.All(l => l != TypeLivrable.CharteProjetSignee))
            {
                blocages.Add("Livrable manquant : Charte projet signée");
            }

            if (projet.CharteProjet?.SignatureSponsor != true)
            {
                blocages.Add("Signature manquante : Sponsor / Directeur Métier sur la charte signée");
            }

            if (projet.CharteProjet?.SignatureChefProjet != true)
            {
                blocages.Add("Signature manquante : Chef de Projet sur la charte signée");
            }

            if (!projet.CharteValideeParDM)
            {
                blocages.Add("Validation manquante : Directeur Métier");
            }

            if (!projet.CharteValideeParDSI)
            {
                blocages.Add("Validation manquante : DSI / RSIT délégué");
            }

            return blocages;
        }

        private static string BuildAnalyseBlockingAlertHtml(IEnumerable<string> blocages)
        {
            var items = string.Join(string.Empty, blocages.Select(item => $"<li>{System.Net.WebUtility.HtmlEncode(item)}</li>"));
            return "Blocage automatique : impossible de passer en phase Planification &amp; Validation tant que les éléments suivants ne sont pas complétés :" +
                   $"<ul class=\"mb-0 mt-2\">{items}</ul>";
        }

        private static string GetLivrableDisplayName(TypeLivrable type)
        {
            return type switch
            {
                TypeLivrable.CahierCharges => "Cahier des charges",
                TypeLivrable.CahierAnalyseTechnique => "Cahier d'analyse technique",
                TypeLivrable.CharteProjet => "Charte projet",
                TypeLivrable.CharteProjetSignee => "Charte projet signée",
                TypeLivrable.NoteCadrage => "Note de cadrage",
                TypeLivrable.Wbs => "WBS",
                TypeLivrable.PlanningDetaille => "Planning détaillé",
                TypeLivrable.MatriceRaci => "Matrice RACI",
                TypeLivrable.SchemaCommunication => "Schéma de communication",
                TypeLivrable.BudgetPrevisionnel => "Budget prévisionnel",
                TypeLivrable.PvKickOff => "PV de kick-off",
                TypeLivrable.CahierTests => "Cahier de tests",
                TypeLivrable.FeuilleAnomalies => "Feuille d'anomalies",
                TypeLivrable.PvRecette => "PV de recette",
                TypeLivrable.RapportHypercare => "Rapport hypercare",
                TypeLivrable.DossierMep => "Dossier MEP",
                TypeLivrable.PvMep => "PV MEP",
                TypeLivrable.RapportCloture => "Rapport de clôture",
                TypeLivrable.PvCloture => "PV de clôture",
                TypeLivrable.DossierExploitation => "Dossier d'exploitation",
                TypeLivrable.CompteRenduReunion => "Compte-rendu de réunion",
                _ => type.ToString()
            };
        }

        private static void NormalizeCharteProjetForPersistence(CharteProjet charte)
        {
            charte.NomProjet = NormalizeRequiredText(charte.NomProjet);
            charte.NumeroProjet = NormalizeOptionalText(charte.NumeroProjet);
            charte.ObjectifProjet = NormalizeRequiredText(charte.ObjectifProjet);
            charte.AssuranceQualite = NormalizeRequiredText(charte.AssuranceQualite);
            charte.Perimetre = NormalizeRequiredText(charte.Perimetre);
            charte.ContraintesInitiales = NormalizeRequiredText(charte.ContraintesInitiales);
            charte.RisquesInitiaux = NormalizeRequiredText(charte.RisquesInitiaux);
            charte.Sponsors = NormalizeRequiredText(charte.Sponsors);
            charte.EmailChefProjet = NormalizeOptionalText(charte.EmailChefProjet);
            charte.CodeDocument = NormalizeRequiredText(charte.CodeDocument);
            charte.TypeDocument = string.IsNullOrWhiteSpace(charte.TypeDocument) ? "Charte de projet" : charte.TypeDocument.Trim();
            charte.Departement = string.IsNullOrWhiteSpace(charte.Departement) ? "SYSTEME D'INFORMATION" : charte.Departement.Trim();
            charte.DescriptionRevision = NormalizeOptionalText(charte.DescriptionRevision);
            charte.RedigePar = NormalizeOptionalText(charte.RedigePar);
            charte.VerifiePar = NormalizeOptionalText(charte.VerifiePar);
            charte.ApprouvePar = NormalizeOptionalText(charte.ApprouvePar);
        }

        private static string NormalizeRequiredText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
