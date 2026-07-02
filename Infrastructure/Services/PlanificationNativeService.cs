using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GestionProjects.Infrastructure.Services
{
    public class PlanificationNativeService : IPlanificationNativeService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly IExcelService _excelService;
        private readonly IFileStorageService _fileStorage;

        public PlanificationNativeService(
            ApplicationDbContext db,
            ICurrentUserService currentUserService,
            IExcelService excelService,
            IFileStorageService fileStorage)
        {
            _db = db;
            _currentUserService = currentUserService;
            _excelService = excelService;
            _fileStorage = fileStorage;
        }

        public async Task<FicheProjet> GetOrCreateFicheProjetAsync(Guid projetId, Guid userId)
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

        public List<PlanningTacheInputViewModel> ParsePlanningTaches(string? ganttPayload)
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

        public async Task ReplacePlanningTasksAsync(Projet projet, Guid userId, IReadOnlyList<PlanningTacheInputViewModel> tasks)
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

        public void SynchronizePlanningSummary(Projet projet, FicheProjet ficheProjet, IReadOnlyList<PlanningTacheInputViewModel> taskInputs)
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

        public List<RaciLigneInputViewModel> ParseRaciLignes(string? raciPayload)
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

        public List<CommunicationLigneInputViewModel> ParseCommunicationLignes(string? communicationPayload)
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

        public List<BudgetLigneInputViewModel> ParseBudgetLignes(string? budgetPayload)
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

        public PvKickOffInputViewModel ParsePvKickOff(string? kickOffPayload)
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

        public async Task ReplaceRaciLinesAsync(Projet projet, IReadOnlyList<RaciLigneInputViewModel> lines)
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

        public async Task ReplaceCommunicationLinesAsync(Projet projet, IReadOnlyList<CommunicationLigneInputViewModel> lines)
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

        public async Task ReplaceBudgetLinesAsync(Projet projet, IReadOnlyList<BudgetLigneInputViewModel> lines)
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

        public async Task UpsertPvKickOffAsync(Projet projet, Guid userId, PvKickOffInputViewModel kickOff)
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

        public void SynchronizeRaciSummary(FicheProjet ficheProjet, IReadOnlyList<RaciLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                ficheProjet.RaciParActivite = string.Empty;
                return;
            }

            ficheProjet.RaciParActivite = string.Join(Environment.NewLine,
                lines.Select(l => $"{(string.IsNullOrWhiteSpace(l.CodeActivite) ? string.Empty : $"{l.CodeActivite} - ")}{l.Activite} | R: {l.Responsable} | A: {l.Approbateur} | C: {l.Consulte} | I: {l.Informe}"));
        }

        public void SynchronizeCommunicationSummary(FicheProjet ficheProjet, IReadOnlyList<CommunicationLigneInputViewModel> lines)
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

        public void SynchronizeBudgetSummary(FicheProjet ficheProjet, IReadOnlyList<BudgetLigneInputViewModel> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            ficheProjet.BudgetPrevisionnel = lines.Sum(l => l.Montant);
            ficheProjet.CommentaireBudgetPlanification = string.Join(Environment.NewLine,
                lines.Select(l => $"{l.Poste}: {l.Montant:N2} FCFA{(string.IsNullOrWhiteSpace(l.Commentaire) ? string.Empty : $" - {l.Commentaire}")}"));
        }

        public async Task<(List<string> Generated, List<string> Missing)> GenerateLivrablesAsync(
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

        private async Task ReplaceGeneratedPlanningLivrableAsync(
            Projet projet,
            Guid userId,
            TypeLivrable typeLivrable,
            string fileName,
            byte[] content,
            string comment)
        {
            var existingLivrables = await _db.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id
                    && l.Phase == PhaseProjet.PlanificationValidation
                    && l.TypeLivrable == typeLivrable
                    && !l.EstSupprime)
                .ToListAsync();

            foreach (var existing in existingLivrables)
            {
                existing.EstSupprime = true;
                existing.DateModification = DateTime.Now;
                existing.ModifiePar = _currentUserService.Matricule;
            }

            var relativePath = await _fileStorage.SaveGeneratedFileAsync(
                content,
                fileName,
                Path.Combine("projets", projet.CodeProjet, "planification", "generated"));

            _db.LivrablesProjets.Add(new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.PlanificationValidation,
                TypeLivrable = typeLivrable,
                NomDocument = fileName,
                CheminRelatif = relativePath,
                DateDepot = DateTime.Now,
                DeposeParId = userId,
                Commentaire = comment,
                Version = $"auto-{DateTime.Now:yyyyMMddHHmmss}",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM"
            });
        }
    }
}
