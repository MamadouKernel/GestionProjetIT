using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class HomeDashboardService : IHomeDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionService _permissionService;

    public HomeDashboardService(ApplicationDbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<DashboardStatsViewModel> BuildAsync(Guid userId, DashboardUrlFactory urlFactory)
    {
            var stats = new DashboardStatsViewModel();

            // RÃ©cupÃ©rer tous les rÃ´les actifs
            var activePermissions = (await _permissionService.GetCurrentUserActivePermissionsAsync())
                .ToHashSet();

            static bool HasPermission(HashSet<(string Controleur, string Action)> permissions, string controleur, string action)
                => permissions.Contains((controleur, action));

            var isAdminScope =
                HasPermission(activePermissions, "Admin", "Users") ||
                HasPermission(activePermissions, "Autorisations", "Index") ||
                HasPermission(activePermissions, "DemandesAcces", "Index");

            var isDsiOrAdmin =
                isAdminScope ||
                HasPermission(activePermissions, "Projet", "ValiderPlanificationDSI") ||
                HasPermission(activePermissions, "Projet", "ForcerStatut");

            var isRsi = HasPermission(activePermissions, "Projet", "EditCommentaireTechnique");
            var isDm =
                HasPermission(activePermissions, "Projet", "ValiderCharteDM") ||
                HasPermission(activePermissions, "Projet", "ValiderPlanificationDM") ||
                HasPermission(activePermissions, "Admin", "ValiderDemandeCreationCompteDM");
            var isCp =
                HasPermission(activePermissions, "Projet", "UpdateProgress") ||
                HasPermission(activePermissions, "Projet", "AddRisk");
            var isDemandeur =
                HasPermission(activePermissions, "DemandeProjet", "Create") ||
                HasPermission(activePermissions, "DemandeProjet", "Soumettre") ||
                HasPermission(activePermissions, "DemandeProjet", "DupliquerDemande");

            var profileLabels = new List<string>();
            if (isAdminScope) profileLabels.Add("AdminIT");
            if (isDsiOrAdmin && !profileLabels.Contains("DSI")) profileLabels.Add("DSI");
            if (isRsi) profileLabels.Add("ResponsableSolutionsIT");
            if (isDm) profileLabels.Add("DirecteurMetier");
            if (isCp) profileLabels.Add("ChefDeProjet");
            if (isDemandeur) profileLabels.Add("Demandeur");

            stats.UserRoles = profileLabels;

            var sixMoisAgo = DateTime.Now.AddMonths(-6);
            var frCulture = new System.Globalization.CultureInfo("fr-FR");

            // DSI/AdminIT : vue globale (prioritaire, pas de cumul nÃ©cessaire)
            if (isDsiOrAdmin)
            {
                var now = DateTime.Now;
                var startOfWeek = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
                var endOfWeek = startOfWeek.AddDays(7);
                var startOfTrend = startOfWeek.AddDays(-28);

                var projets = await _db.Projets
                    .Where(p => !p.EstSupprime)
                    .Include(p => p.Direction)
                    .Include(p => p.ChefProjet)
                    .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                    .Include(p => p.FicheProjet)
                    .ToListAsync();

                var projetIds = projets.Select(p => p.Id).ToHashSet();

                stats.TotalProjets = projets.Count;
                stats.ProjetsEnCours = projets.Count(p => p.StatutProjet == StatutProjet.EnCours);
                stats.TotalDemandes = await _db.DemandesProjets.CountAsync(d => !d.EstSupprime);
                stats.DemandesEnAttente = await _db.DemandesProjets.CountAsync(d => !d.EstSupprime && d.StatutDemande == StatutDemande.EnAttenteValidationDSI);
                stats.TotalUtilisateurs = await _db.Utilisateurs.CountAsync(u => !u.EstSupprime);
                stats.ProjetsEnAnalyse = projets.Count(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                stats.ProjetsEnPlanification = projets.Count(p => p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                stats.ProjetsEnExecution = projets.Count(p => p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                stats.ProjetsEnUAT = projets.Count(p => p.PhaseActuelle == PhaseProjet.UatMep);
                stats.ProjetsClotures = projets.Count(p => p.StatutProjet == StatutProjet.Cloture);
                stats.ProjetsSuspendus = projets.Count(p => p.StatutProjet == StatutProjet.Suspendu);
                stats.DemandesValidees = await _db.DemandesProjets.CountAsync(d => !d.EstSupprime && d.StatutDemande == StatutDemande.ValideeParDSI);
                stats.DemandesRejetees = await _db.DemandesProjets.CountAsync(d => !d.EstSupprime && d.StatutDemande == StatutDemande.RejeteeParDSI);
                stats.TotalLivrables = await _db.LivrablesProjets.CountAsync(l => !l.EstSupprime);
                stats.TauxAvancementMoyen = projets.Any() ? projets.Average(p => p.PourcentageAvancement) : 0;

                stats.ProjetsVerts = projets.Count(p => p.EtatProjet == EtatProjet.Vert);
                stats.ProjetsOranges = projets.Count(p => p.EtatProjet == EtatProjet.Orange);
                stats.ProjetsRouges = projets.Count(p => p.EtatProjet == EtatProjet.Rouge);
                stats.ValidationsDMEnAttente = await _db.DemandesProjets.CountAsync(d => !d.EstSupprime && d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier);
                stats.ValidationsDSIEnAttente = stats.DemandesEnAttente;
                stats.ChartesNonValidees = projets.Count(p =>
                    p.PhaseActuelle != PhaseProjet.Demande &&
                    p.StatutProjet != StatutProjet.Cloture &&
                    p.StatutProjet != StatutProjet.Annule &&
                    !p.CharteValidee);

                var demandesCloture = await _db.DemandesClotureProjets
                    .Where(dc => !dc.EstSupprime)
                    .Include(dc => dc.Projet)
                    .ThenInclude(p => p.DemandeProjet)
                    .Include(dc => dc.DemandePar)
                    .ToListAsync();

                stats.CloturesEnAttente = demandesCloture.Count(dc =>
                    !dc.EstTerminee &&
                    dc.StatutValidationDemandeur == StatutValidationCloture.Validee &&
                    dc.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
                    dc.StatutValidationDSI == StatutValidationCloture.EnAttente);

                var risques = await _db.RisquesProjets
                    .Where(r => !r.EstSupprime && projetIds.Contains(r.ProjetId))
                    .Select(r => new { r.ProjetId, r.Impact, r.DateCreationRisque })
                    .ToListAsync();

                stats.TotalRisques = risques.Count;
                stats.RisquesCritiques = risques.Count(r => r.Impact == ImpactRisque.Critique);

                var anomalies = await _db.AnomaliesProjets
                    .Where(a => !a.EstSupprime && projetIds.Contains(a.ProjetId))
                    .Select(a => new { a.ProjetId, a.Statut, a.DateCreation })
                    .ToListAsync();

                stats.TotalAnomalies = anomalies.Count;
                stats.AnomaliesOuvertes = anomalies.Count(a => a.Statut == StatutAnomalie.Ouverte);

                var fiches = await _db.FicheProjets
                    .Where(f => !f.EstSupprime && projetIds.Contains(f.ProjetId))
                    .ToListAsync();

                stats.BudgetTotalPortefeuille = fiches.Sum(f => f.BudgetPrevisionnel ?? 0);
                stats.BudgetConsommeTotal = fiches.Sum(f => f.BudgetConsomme ?? 0);
                stats.ProjetsDepassementBudget = fiches.Count(f =>
                    (f.BudgetPrevisionnel ?? 0) > 0 &&
                    (f.BudgetConsomme ?? 0) > (f.BudgetPrevisionnel ?? 0));

                var recentCharges = await _db.ChargesProjets
                    .Where(c =>
                        !c.EstSupprime &&
                        projetIds.Contains(c.ProjetId) &&
                        c.SemaineDebut >= startOfTrend &&
                        c.SemaineDebut < endOfWeek)
                    .Include(c => c.Ressource)
                    .ToListAsync();

                var currentWeekCharges = recentCharges
                    .Where(c => c.SemaineDebut >= startOfWeek && c.SemaineDebut < endOfWeek)
                    .ToList();

                var allProjectCharges = await _db.ChargesProjets
                    .Where(c => !c.EstSupprime && projetIds.Contains(c.ProjetId))
                    .GroupBy(c => c.ProjetId)
                    .Select(g => new
                    {
                        ProjetId = g.Key,
                        ChargePrevisionnelle = g.Sum(c => c.ChargePrevisionnelle),
                        ChargeReelle = g.Sum(c => c.ChargeReelle ?? 0)
                    })
                    .ToListAsync();

                var ressourcesPilotes = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime && u.ProfilRessource.HasValue)
                    .Select(u => new { u.Id, u.CapaciteHebdomadaire, u.ProfilRessource })
                    .ToListAsync();

                var chargesParRessource = currentWeekCharges
                    .GroupBy(c => c.RessourceId)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            ChargePrevisionnelle = g.Sum(c => c.ChargePrevisionnelle),
                            ChargeReelle = g.Sum(c => c.ChargeReelle ?? 0)
                        });

                stats.RessourcesSurchargees = ressourcesPilotes.Count(r =>
                {
                    if (!chargesParRessource.TryGetValue(r.Id, out var charge))
                    {
                        return false;
                    }

                    return charge.ChargePrevisionnelle > r.CapaciteHebdomadaire ||
                           charge.ChargeReelle > r.CapaciteHebdomadaire;
                });

                stats.RessourcesDisponibles = ressourcesPilotes.Count(r =>
                {
                    if (!chargesParRessource.TryGetValue(r.Id, out var charge))
                    {
                        return true;
                    }

                    return charge.ChargePrevisionnelle < r.CapaciteHebdomadaire;
                });

                stats.ProjetsAvecDepassementCharge = currentWeekCharges
                    .GroupBy(c => c.ProjetId)
                    .Count(g => g.Sum(c => c.ChargeReelle ?? c.ChargePrevisionnelle) > g.Sum(c => c.ChargePrevisionnelle));

                stats.ChargeTotaleSemaine = currentWeekCharges.Sum(c => c.ChargePrevisionnelle);

                var chargeTotalePrevisionnelle = ressourcesPilotes.Sum(r => r.CapaciteHebdomadaire);
                stats.TauxUtilisationCapacite = chargeTotalePrevisionnelle > 0
                    ? Math.Round((double)stats.ChargeTotaleSemaine / (double)chargeTotalePrevisionnelle * 100, 1)
                    : 0;

                stats.ProjetsParStatut = projets
                    .GroupBy(p => p.StatutProjet)
                    .ToDictionary(g => FormatStatutProjet(g.Key), g => g.Count());

                stats.DemandesParStatut = await _db.DemandesProjets
                    .Where(d => !d.EstSupprime)
                    .GroupBy(d => d.StatutDemande)
                    .ToDictionaryAsync(g => FormatStatutDemande(g.Key), g => g.Count());

                stats.ProjetsParPhase = projets
                    .GroupBy(p => p.PhaseActuelle)
                    .ToDictionary(g => FormatPhaseProjet(g.Key), g => g.Count());

                stats.ProjetsParEtat = projets
                    .GroupBy(p => p.EtatProjet)
                    .ToDictionary(g => FormatEtatProjet(g.Key), g => g.Count());

                stats.ProjetsParDirection = projets
                    .Where(p => p.Direction != null)
                    .GroupBy(p => p.Direction!.Libelle)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

                stats.ChargeParEquipe = currentWeekCharges
                    .Where(c => c.Ressource?.ProfilRessource != null)
                    .GroupBy(c => c.Ressource!.ProfilRessource)
                    .OrderByDescending(g => g.Sum(c => c.ChargePrevisionnelle))
                    .Take(5)
                    .ToDictionary(
                        g => FormatProfilRessource(g.Key),
                        g => Math.Round((double)g.Sum(c => c.ChargePrevisionnelle), 1));

                for (var i = 4; i >= 0; i--)
                {
                    var weekStart = startOfWeek.AddDays(-7 * i);
                    var weekEnd = weekStart.AddDays(7);
                    var weekCharges = recentCharges.Where(c => c.SemaineDebut >= weekStart && c.SemaineDebut < weekEnd).ToList();

                    stats.ChargeTendanceLabels.Add($"S{System.Globalization.ISOWeek.GetWeekOfYear(weekStart)}");
                    stats.ChargeTendancePrevisionnelle.Add(Math.Round((double)weekCharges.Sum(c => c.ChargePrevisionnelle), 1));
                    stats.ChargeTendanceReelle.Add(Math.Round((double)weekCharges.Sum(c => c.ChargeReelle ?? 0), 1));
                }

                var ppm = projets.Where(p => p.DateCreation >= sixMoisAgo)
                    .Select(p => p.DateCreation)
                    .GroupBy(p => new { p.Year, p.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ToList();
                foreach (var g in ppm) stats.EvolutionProjets[new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy", frCulture)] = g.Count();

                var dpm = (await _db.DemandesProjets.Where(d => d.DateSoumission >= sixMoisAgo).Select(d => d.DateSoumission).ToListAsync())
                    .GroupBy(d => new { d.Year, d.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ToList();
                foreach (var g in dpm) stats.EvolutionDemandes[new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy", frCulture)] = g.Count();

                var projetsAvecUrgence = projets.Where(p => p.DemandeProjet != null).ToList();
                stats.ProjetsParUrgence = projetsAvecUrgence.GroupBy(p => p.DemandeProjet!.Urgence.ToString()).ToDictionary(g => g.Key, g => g.Count());
                stats.ProjetsParCriticite = projetsAvecUrgence.GroupBy(p => p.DemandeProjet!.Criticite.ToString()).ToDictionary(g => g.Key, g => g.Count());

                var risqueParProjet = risques
                    .GroupBy(r => r.ProjetId)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            Critiques = g.Count(r => r.Impact == ImpactRisque.Critique),
                            Eleves = g.Count(r => r.Impact == ImpactRisque.Eleve),
                            MaxImpact = g.Max(r => r.Impact)
                        });

                var anomaliesOuvertesParProjet = anomalies
                    .Where(a => a.Statut == StatutAnomalie.Ouverte)
                    .GroupBy(a => a.ProjetId)
                    .ToDictionary(g => g.Key, g => g.Count());

                var chargeParProjet = allProjectCharges.ToDictionary(
                    c => c.ProjetId,
                    c => new { c.ChargePrevisionnelle, c.ChargeReelle });

                stats.AdminCriticalProjects = projets
                    .Where(p =>
                        p.StatutProjet != StatutProjet.Cloture &&
                        p.StatutProjet != StatutProjet.Annule &&
                        (p.EtatProjet == EtatProjet.Rouge ||
                         p.EtatProjet == EtatProjet.Orange ||
                         (p.DateFinPrevue.HasValue && p.DateFinPrevue.Value.Date < now.Date) ||
                         risqueParProjet.ContainsKey(p.Id)))
                    .OrderByDescending(p => p.EtatProjet == EtatProjet.Rouge)
                    .ThenByDescending(p => p.EtatProjet == EtatProjet.Orange)
                    .ThenBy(p => p.DateFinPrevue ?? DateTime.MaxValue)
                    .Take(5)
                    .Select(p =>
                    {
                        var retard = p.DateFinPrevue.HasValue
                            ? (p.DateFinPrevue.Value.Date - now.Date).Days
                            : 0;

                        var risque = risqueParProjet.TryGetValue(p.Id, out var risqueProjet)
                            ? risqueProjet.Critiques > 0
                                ? ("Ã‰levÃ©", "badge-modern-danger")
                                : risqueProjet.Eleves > 0
                                    ? ("Moyen", "badge-modern-warning")
                                    : ("Faible", "badge-modern-info")
                            : ("-", "badge-modern-secondary");

                        return new DashboardCriticalProjectItem
                        {
                            ProjetId = p.Id,
                            Projet = p.Titre,
                            Phase = FormatPhaseProjet(p.PhaseActuelle),
                            Etat = FormatEtatProjet(p.EtatProjet),
                            Retard = !p.DateFinPrevue.HasValue
                                ? "-"
                                : retard < 0
                                    ? $"+{Math.Abs(retard)} j"
                                    : retard == 0
                                        ? "Aujourd'hui"
                                        : $"{retard} j",
                            Risque = risque.Item1,
                            RisqueBadgeClass = risque.Item2,
                            ChefProjet = FormatUtilisateurCourt(p.ChefProjet?.Nom, p.ChefProjet?.Prenoms),
                            Url = BuildUrl(urlFactory, "Details", "Projet", new { id = p.Id }) ?? "#"
                        };
                    })
                    .ToList();

                var validationRows = new List<(DateTime SortDate, DashboardValidationItem Item)>();

                var demandesValidationDM = await _db.DemandesProjets
                    .Where(d => !d.EstSupprime && d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier)
                    .Include(d => d.Demandeur)
                    .OrderBy(d => d.DateSoumission)
                    .Take(3)
                    .ToListAsync();

                validationRows.AddRange(demandesValidationDM.Select(d => (
                    d.DateSoumission,
                    new DashboardValidationItem
                    {
                        Type = "Demande",
                        Element = "Validation Directeur mÃ©tier",
                        Projet = d.Titre ?? string.Empty,
                        Demandeur = FormatUtilisateurCourt(d.Demandeur?.Nom, d.Demandeur?.Prenoms),
                        DateLabel = d.DateSoumission.ToString("dd/MM/yyyy"),
                        Url = BuildUrl(urlFactory, "Details", "DemandeProjet", new { id = d.Id }) ?? "#"
                    })));

                var demandesValidationDSI = await _db.DemandesProjets
                    .Where(d => !d.EstSupprime && d.StatutDemande == StatutDemande.EnAttenteValidationDSI)
                    .Include(d => d.Demandeur)
                    .OrderBy(d => d.DateSoumission)
                    .Take(3)
                    .ToListAsync();

                validationRows.AddRange(demandesValidationDSI.Select(d => (
                    d.DateSoumission,
                    new DashboardValidationItem
                    {
                        Type = "Demande",
                        Element = "Validation DSI",
                        Projet = d.Titre ?? string.Empty,
                        Demandeur = FormatUtilisateurCourt(d.Demandeur?.Nom, d.Demandeur?.Prenoms),
                        DateLabel = d.DateSoumission.ToString("dd/MM/yyyy"),
                        Url = BuildUrl(urlFactory, "Details", "DemandeProjet", new { id = d.Id }) ?? "#"
                    })));

                validationRows.AddRange(projets
                    .Where(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification && p.CharteValideeParDM && !p.CharteValideeParDSI)
                    .Take(2)
                    .Select(p => (
                        p.DateModification ?? p.DateCreation,
                        new DashboardValidationItem
                        {
                            Type = "Charte",
                            Element = "Charte Ã  valider DSI",
                            Projet = p.Titre,
                            Demandeur = FormatUtilisateurCourt(p.DemandeProjet?.Demandeur?.Nom, p.DemandeProjet?.Demandeur?.Prenoms),
                            DateLabel = (p.DateModification ?? p.DateCreation).ToString("dd/MM/yyyy"),
                            Url = BuildUrl(urlFactory, "Details", "Projet", new { id = p.Id, tab = "analyse" }) ?? "#"
                        })));

                validationRows.AddRange(projets
                    .Where(p => p.PhaseActuelle == PhaseProjet.PlanificationValidation && p.PlanningValideParDM && !p.PlanningValideParDSI)
                    .Take(2)
                    .Select(p => (
                        p.DateModification ?? p.DateCreation,
                        new DashboardValidationItem
                        {
                            Type = "Planning",
                            Element = "Planning Ã  valider DSI",
                            Projet = p.Titre,
                            Demandeur = FormatUtilisateurCourt(p.DemandeProjet?.Demandeur?.Nom, p.DemandeProjet?.Demandeur?.Prenoms),
                            DateLabel = (p.DateModification ?? p.DateCreation).ToString("dd/MM/yyyy"),
                            Url = BuildUrl(urlFactory, "Details", "Projet", new { id = p.Id, tab = "planification" }) ?? "#"
                        })));

                validationRows.AddRange(demandesCloture
                    .Where(dc =>
                        !dc.EstTerminee &&
                        dc.StatutValidationDemandeur == StatutValidationCloture.Validee &&
                        dc.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
                        dc.StatutValidationDSI == StatutValidationCloture.EnAttente)
                    .Take(2)
                    .Select(dc => (
                        dc.DateDemande,
                        new DashboardValidationItem
                        {
                            Type = "ClÃ´ture",
                            Element = "ClÃ´ture de projet",
                            Projet = dc.Projet.Titre,
                            Demandeur = FormatUtilisateurCourt(dc.DemandePar.Nom, dc.DemandePar.Prenoms),
                            DateLabel = dc.DateDemande.ToString("dd/MM/yyyy"),
                            Url = BuildUrl(urlFactory, "Details", "Projet", new { id = dc.ProjetId, tab = "cloture" }) ?? "#"
                        })));

                stats.AdminPendingValidations = validationRows
                    .OrderBy(v => v.SortDate)
                    .Take(6)
                    .Select(v => v.Item)
                    .ToList();

                stats.AdminMilestones = projets
                    .Where(p =>
                        p.DateFinPrevue.HasValue &&
                        p.StatutProjet != StatutProjet.Cloture &&
                        p.StatutProjet != StatutProjet.Annule)
                    .OrderBy(p => p.DateFinPrevue)
                    .Take(5)
                    .Select(p => new DashboardMilestoneItem
                    {
                        DateLabel = p.DateFinPrevue!.Value.ToString("dd/MM/yyyy"),
                        Projet = p.Titre,
                        Libelle = string.IsNullOrWhiteSpace(p.FicheProjet?.ProchainJalon)
                            ? $"Jalon {FormatPhaseProjet(p.PhaseActuelle)}"
                            : p.FicheProjet!.ProchainJalon!,
                        Url = BuildUrl(urlFactory, "Details", "Projet", new { id = p.Id }) ?? "#"
                    })
                    .ToList();

                var alertRows = new List<DashboardAlertItem>();

                alertRows.AddRange(projets
                    .Where(p =>
                        p.DateFinPrevue.HasValue &&
                        p.DateFinPrevue.Value.Date < now.Date &&
                        p.StatutProjet != StatutProjet.Cloture &&
                        p.StatutProjet != StatutProjet.Annule)
                    .OrderBy(p => p.DateFinPrevue)
                    .Take(2)
                    .Select(p => new DashboardAlertItem
                    {
                        Niveau = "danger",
                        IconClass = "bi bi-exclamation-triangle-fill",
                        Message = $"{p.Titre} est en retard de {Math.Abs((p.DateFinPrevue!.Value.Date - now.Date).Days)} jour(s).",
                        DateLabel = p.DateFinPrevue!.Value.ToString("dd/MM/yyyy"),
                        Url = BuildUrl(urlFactory, "Details", "Projet", new { id = p.Id }) ?? "#"
                    }));

                alertRows.AddRange(risqueParProjet
                    .Where(r => r.Value.Critiques > 0)
                    .OrderByDescending(r => r.Value.Critiques)
                    .Take(2)
                    .Select(r =>
                    {
                        var projet = projets.FirstOrDefault(p => p.Id == r.Key);
                        return new DashboardAlertItem
                        {
                            Niveau = "warning",
                            IconClass = "bi bi-shield-exclamation",
                            Message = $"{projet?.Titre ?? "Projet"} comporte {r.Value.Critiques} risque(s) critique(s).",
                            DateLabel = now.ToString("dd/MM/yyyy HH:mm"),
                            Url = projet != null ? (BuildUrl(urlFactory, "Details", "Projet", new { id = projet.Id, tab = "analyse" }) ?? "#") : "#"
                        };
                    }));

                alertRows.AddRange(anomaliesOuvertesParProjet
                    .OrderByDescending(a => a.Value)
                    .Take(2)
                    .Select(a =>
                    {
                        var projet = projets.FirstOrDefault(p => p.Id == a.Key);
                        return new DashboardAlertItem
                        {
                            Niveau = "info",
                            IconClass = "bi bi-bug-fill",
                            Message = $"{projet?.Titre ?? "Projet"} compte {a.Value} anomalie(s) ouverte(s).",
                            DateLabel = now.ToString("dd/MM/yyyy HH:mm"),
                            Url = projet != null ? (BuildUrl(urlFactory, "Details", "Projet", new { id = projet.Id, tab = "uat" }) ?? "#") : "#"
                        };
                    }));

                stats.AdminAlerts = alertRows.Take(5).ToList();

                stats.AdminPortfolioRows = projets
                    .Where(p => p.StatutProjet != StatutProjet.Cloture && p.StatutProjet != StatutProjet.Annule)
                    .OrderByDescending(p => p.EtatProjet == EtatProjet.Rouge)
                    .ThenByDescending(p => p.EtatProjet == EtatProjet.Orange)
                    .ThenByDescending(p => p.PourcentageAvancement)
                    .Take(5)
                    .Select(p =>
                    {
                        var budgetPrevisionnel = p.FicheProjet?.BudgetPrevisionnel;
                        var budgetConsomme = p.FicheProjet?.BudgetConsomme;

                        chargeParProjet.TryGetValue(p.Id, out var chargeProjet);

                        return new DashboardPortfolioItem
                        {
                            ProjetId = p.Id,
                            Projet = p.Titre,
                            Direction = p.Direction?.Libelle ?? "Non dÃ©finie",
                            Phase = FormatPhaseProjet(p.PhaseActuelle),
                            Etat = FormatEtatProjet(p.EtatProjet),
                            Avancement = p.PourcentageAvancement,
                            Budget = budgetPrevisionnel.HasValue || budgetConsomme.HasValue
                                ? $"{FormatMontantCompact(budgetConsomme)} / {FormatMontantCompact(budgetPrevisionnel)}"
                                : "-",
                            Charge = chargeProjet != null
                                ? $"{Math.Round(chargeProjet.ChargeReelle, 0)} h / {Math.Round(chargeProjet.ChargePrevisionnelle, 0)} h"
                                : "-",
                            ChefProjet = FormatUtilisateurCourt(p.ChefProjet?.Nom, p.ChefProjet?.Prenoms),
                            Url = BuildUrl(urlFactory, "Details", "Projet", new { id = p.Id }) ?? "#"
                        };
                    })
                    .ToList();
            }
            else
            {
                // Pour les autres rÃ´les : cumuler les stats de CHAQUE rÃ´le actif

                // ===== ResponsableSolutionsIT =====
                if (isRsi)
                {
                    stats.TotalProjets = await _db.Projets.CountAsync();
                    stats.ProjetsEnCours = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.EnCours);
                    stats.ProjetsEnAnalyse = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                    stats.ProjetsEnPlanification = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                    stats.ProjetsEnExecution = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                    stats.ProjetsEnUAT = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.UatMep);
                    stats.ProjetsClotures = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.Cloture);
                    stats.TotalRisques = await _db.Set<RisqueProjet>().CountAsync();
                    stats.RisquesCritiques = await _db.Set<RisqueProjet>().CountAsync(r => r.Impact == ImpactRisque.Critique);
                    stats.TotalAnomalies = await _db.Set<AnomalieProjet>().CountAsync();
                    stats.AnomaliesOuvertes = await _db.Set<AnomalieProjet>().CountAsync(a => a.Statut == StatutAnomalie.Ouverte);
                    stats.TotalLivrables = await _db.Set<LivrableProjet>().CountAsync();
                    var avRSI = await _db.Projets.Where(p => p.PourcentageAvancement > 0).Select(p => p.PourcentageAvancement).ToListAsync();
                    stats.TauxAvancementMoyen = avRSI.Any() ? avRSI.Average() : 0;

                    // Graphiques RSI
                    stats.RSIProjetsParStatut = await _db.Projets.GroupBy(p => p.StatutProjet).ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());
                    stats.RSIProjetsParPhase = await _db.Projets.GroupBy(p => p.PhaseActuelle).ToDictionaryAsync(g => FormatPhaseProjet(g.Key), g => g.Count());
                    stats.RSIProjetsParDirection = await _db.Projets.Include(p => p.Direction).Where(p => p.Direction != null).GroupBy(p => p.Direction!.Libelle).ToDictionaryAsync(g => g.Key, g => g.Count());
                }

                // ===== DirecteurMetier =====
                if (isDm)
                {
                    var userDM = await _db.Utilisateurs.Include(u => u.Direction).FirstOrDefaultAsync(u => u.Id == userId);
                    var directionId = userDM?.DirectionId;

                    stats.DemandesAValiderDM = await _db.DemandesProjets.CountAsync(d =>
                        (d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier || d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI) &&
                        d.DirecteurMetierId == userId);
                    stats.DemandesValideesDM = await _db.DemandesProjets.CountAsync(d => d.DirecteurMetierId == userId && d.StatutDemande == StatutDemande.EnAttenteValidationDSI);
                    stats.DemandesRejeteesParDM = await _db.DemandesProjets.CountAsync(d => d.DirecteurMetierId == userId && d.StatutDemande == StatutDemande.RejeteeParDirecteurMetier);

                    IQueryable<Projet> projetsQueryDM = _db.Projets.Where(p => p.SponsorId == userId);
                    if (directionId.HasValue)
                        projetsQueryDM = _db.Projets.Where(p => p.SponsorId == userId || p.DirectionId == directionId);
                    stats.ProjetsMaDirection = await projetsQueryDM.CountAsync();

                    // Graphiques DirecteurMetier
                    stats.DMDemandesParStatut = await _db.DemandesProjets.Where(d => d.DirecteurMetierId == userId)
                        .GroupBy(d => d.StatutDemande).ToDictionaryAsync(g => FormatStatutDemande(g.Key), g => g.Count());
                    if (directionId.HasValue)
                    {
                        stats.DMProjetsParStatut = await _db.Projets.Where(p => p.DirectionId == directionId || p.SponsorId == userId)
                            .GroupBy(p => p.StatutProjet).ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());
                    }
                }

                // ===== ChefDeProjet =====
                if (isCp)
                {
                    stats.MesProjetsGeres = await _db.Projets.CountAsync(p => p.ChefProjetId == userId);
                    stats.MesProjetsGeresEnCours = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.StatutProjet == StatutProjet.EnCours);
                    stats.MesProjetsGeresEnAnalyse = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                    stats.MesProjetsGeresEnPlanification = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                    stats.MesProjetsGeresEnExecution = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                    stats.MesProjetsGeresEnUAT = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.UatMep);
                    stats.TotalRisques = await _db.Set<RisqueProjet>().CountAsync(r => r.Projet!.ChefProjetId == userId);
                    stats.RisquesCritiques = await _db.Set<RisqueProjet>().CountAsync(r => r.Projet!.ChefProjetId == userId && r.Impact == ImpactRisque.Critique);
                    stats.AnomaliesOuvertes = await _db.Set<AnomalieProjet>().CountAsync(a => a.Projet!.ChefProjetId == userId && a.Statut == StatutAnomalie.Ouverte);
                    stats.TotalLivrables = await _db.Set<LivrableProjet>().CountAsync(l => l.Projet!.ChefProjetId == userId);
                    var avCP = await _db.Projets.Where(p => p.ChefProjetId == userId && p.PourcentageAvancement > 0).Select(p => p.PourcentageAvancement).ToListAsync();
                    stats.TauxAvancementMoyen = avCP.Any() ? avCP.Average() : 0;

                    // Graphiques ChefDeProjet
                    stats.CPProjetsParPhase = await _db.Projets.Where(p => p.ChefProjetId == userId)
                        .GroupBy(p => p.PhaseActuelle).ToDictionaryAsync(g => FormatPhaseProjet(g.Key), g => g.Count());
                    stats.CPProjetsParStatut = await _db.Projets.Where(p => p.ChefProjetId == userId)
                        .GroupBy(p => p.StatutProjet).ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());
                }

                // ===== Demandeur =====
                if (isDemandeur)
                {
                    stats.MesDemandes = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId);
                    stats.MesDemandesEnAttente = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId &&
                        (d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier || d.StatutDemande == StatutDemande.EnAttenteValidationDSI));
                    stats.MesDemandesValidees = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId && d.StatutDemande == StatutDemande.ValideeParDSI);
                    stats.MesDemandesRejetees = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId &&
                        (d.StatutDemande == StatutDemande.RejeteeParDirecteurMetier || d.StatutDemande == StatutDemande.RejeteeParDSI));
                    stats.MesProjetsCreesParDemande = await _db.Projets.CountAsync(p => p.DemandeProjet != null && p.DemandeProjet.DemandeurId == userId);

                    // Graphique Demandeur
                    stats.DemandeurDemandesParStatut = await _db.DemandesProjets.Where(d => d.DemandeurId == userId)
                        .GroupBy(d => d.StatutDemande).ToDictionaryAsync(g => FormatStatutDemande(g.Key), g => g.Count());
                }
            }

        return stats;
    }

    private static string? BuildUrl(DashboardUrlFactory urlFactory, string action, string controller, object? values)
    {
        return urlFactory(action, controller, values);
    }

        private static string FormatStatutProjet(StatutProjet statut)
        {
            return statut switch
            {
                StatutProjet.NonDemarre => "Non DÃ©marrÃ©",
                StatutProjet.EnCours => "En Cours",
                StatutProjet.Suspendu => "Suspendu",
                StatutProjet.ClotureEnCours => "ClÃ´ture en Cours",
                StatutProjet.Cloture => "ClÃ´turÃ©",
                StatutProjet.Annule => "AnnulÃ©",
                _ => statut.ToString()
            };
        }

        private static string FormatStatutDemande(StatutDemande statut)
        {
            return statut switch
            {
                StatutDemande.Brouillon => "Brouillon",
                StatutDemande.EnAttenteValidationDirecteurMetier => "En Attente DM",
                StatutDemande.CorrectionDemandeeParDirecteurMetier => "Correction DemandÃ©e",
                StatutDemande.RejeteeParDirecteurMetier => "RejetÃ©e par DM",
                StatutDemande.EnAttenteValidationDSI => "En Attente DSI",
                StatutDemande.RetourneeAuDemandeurParDSI => "Retour Demandeur",
                StatutDemande.RetourneeAuDirecteurMetierParDSI => "Retour DM",
                StatutDemande.RejeteeParDSI => "RejetÃ©e par DSI",
                StatutDemande.ValideeParDSI => "ValidÃ©e",
                _ => statut.ToString()
            };
        }

        private static string FormatPhaseProjet(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.Demande => "Demande",
                PhaseProjet.AnalyseClarification => "Analyse & Clarification",
                PhaseProjet.PlanificationValidation => "Planification",
                PhaseProjet.ExecutionSuivi => "ExÃ©cution",
                PhaseProjet.UatMep => "UAT & MEP",
                PhaseProjet.ClotureLeconsApprises => "ClÃ´ture",
                _ => phase.ToString()
            };
        }

        private static string FormatEtatProjet(EtatProjet etat)
        {
            return etat switch
            {
                EtatProjet.Vert => "Vert",
                EtatProjet.Orange => "Orange",
                EtatProjet.Rouge => "Rouge",
                _ => etat.ToString()
            };
        }

        private static string FormatProfilRessource(ProfilRessource? profil)
        {
            return profil switch
            {
                ProfilRessource.Developpement => "DÃ©veloppement",
                ProfilRessource.Infrastructure => "Infrastructure",
                ProfilRessource.Support => "Support",
                ProfilRessource.DBA => "DBA",
                ProfilRessource.ChefProjet => "Chefferie projet",
                ProfilRessource.Architecte => "Architecture",
                ProfilRessource.Analyste => "Analyse",
                ProfilRessource.Autre => "Autres",
                _ => "Non dÃ©fini"
            };
        }

        private static string FormatUtilisateurCourt(string? nom, string? prenoms)
        {
            if (string.IsNullOrWhiteSpace(nom) && string.IsNullOrWhiteSpace(prenoms))
            {
                return "Non affectÃ©";
            }

            if (string.IsNullOrWhiteSpace(prenoms))
            {
                return nom ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(nom))
            {
                return prenoms ?? string.Empty;
            }

            return $"{nom} {prenoms}";
        }

        private static string FormatMontantCompact(decimal? montant)
        {
            if (!montant.HasValue || montant.Value <= 0)
            {
                return "0,0 M";
            }

            return $"{Math.Round(montant.Value / 1_000_000m, 1):N1} M";
        }
}
