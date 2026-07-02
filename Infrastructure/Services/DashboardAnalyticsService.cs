using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class DashboardAnalyticsService : IDashboardAnalyticsService
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionService _permissionService;

    public DashboardAnalyticsService(ApplicationDbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    private enum DashboardScope
    {
        None,
        Portfolio,
        Direction,
        ChefProjet,
        Demandeur
    }

    private sealed record DashboardScopeContext(DashboardScope Scope, Guid? DirectionId = null);

    public Task<bool> CanAccessAsync()
    {
        return _permissionService.CurrentUserHasPermissionAsync("Dashboard", "Index");
    }

    public async Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByPhaseItem>>> GetProjetsParPhaseAsync(Guid userId)
    {
        var scope = await ResolveScopeAsync(userId);
        if (scope.Scope == DashboardScope.None || !await CanAccessAsync())
        {
            return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByPhaseItem>>.Forbidden();
        }

        var data = await ApplyProjectScope(
                _db.Projets.Include(p => p.DemandeProjet).Where(p => !p.EstSupprime),
                scope,
                userId)
            .GroupBy(p => p.PhaseActuelle)
            .Select(g => new DashboardCountByPhaseItem(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByPhaseItem>>.Success(data);
    }

    public async Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByEtatItem>>> GetRagAsync(Guid userId)
    {
        var scope = await ResolveScopeAsync(userId);
        if (scope.Scope == DashboardScope.None || !await CanAccessAsync())
        {
            return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByEtatItem>>.Forbidden();
        }

        var data = await ApplyProjectScope(
                _db.Projets
                    .Include(p => p.DemandeProjet)
                    .Where(p => !p.EstSupprime &&
                                p.StatutProjet != StatutProjet.Cloture &&
                                p.StatutProjet != StatutProjet.Annule),
                scope,
                userId)
            .GroupBy(p => p.EtatProjet)
            .Select(g => new DashboardCountByEtatItem(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByEtatItem>>.Success(data);
    }

    public async Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByDirectionItem>>> GetProjetsParDirectionAsync()
    {
        if (!await CanAccessAsync() || !await HasPortfolioScopeAsync())
        {
            return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByDirectionItem>>.Forbidden();
        }

        var data = await _db.Projets
            .Where(p => !p.EstSupprime)
            .Include(p => p.Direction)
            .GroupBy(p => p.Direction!.Libelle)
            .Select(g => new DashboardCountByDirectionItem(g.Key ?? "Non d\u00e9finie", g.Count()))
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByDirectionItem>>.Success(data);
    }

    public async Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByStatutItem>>> GetProjetsParStatutAsync(Guid userId)
    {
        var scope = await ResolveScopeAsync(userId);
        if (scope.Scope == DashboardScope.None || !await CanAccessAsync())
        {
            return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByStatutItem>>.Forbidden();
        }

        var data = await ApplyProjectScope(
                _db.Projets.Where(p => !p.EstSupprime),
                scope,
                userId)
            .GroupBy(p => p.StatutProjet)
            .Select(g => new DashboardCountByStatutItem(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return DashboardAnalyticsResult<IReadOnlyList<DashboardCountByStatutItem>>.Success(data);
    }

    public async Task<DashboardAnalyticsResult<IReadOnlyList<DashboardMonthlyTrendItem>>> GetTendanceMensuelleAsync(Guid userId)
    {
        var scope = await ResolveScopeAsync(userId);
        if (scope.Scope == DashboardScope.None || !await CanAccessAsync())
        {
            return DashboardAnalyticsResult<IReadOnlyList<DashboardMonthlyTrendItem>>.Forbidden();
        }

        var depuis = DateTime.UtcNow.AddMonths(-11);
        var data = await ApplyProjectScope(
                _db.Projets.Where(p => !p.EstSupprime && p.DateCreation >= depuis),
                scope,
                userId)
            .GroupBy(p => new { p.DateCreation.Year, p.DateCreation.Month })
            .Select(g => new
            {
                Annee = g.Key.Year,
                Mois = g.Key.Month,
                Count = g.Count()
            })
            .OrderBy(x => x.Annee)
            .ThenBy(x => x.Mois)
            .ToListAsync();

        var result = data
            .Select(d => new DashboardMonthlyTrendItem($"{d.Annee}-{d.Mois:D2}", d.Count))
            .ToList();

        return DashboardAnalyticsResult<IReadOnlyList<DashboardMonthlyTrendItem>>.Success(result);
    }

    public async Task<DashboardAnalyticsResult<IReadOnlyList<DashboardPhaseDurationItem>>> GetDureeParPhaseAsync(Guid userId)
    {
        var scope = await ResolveScopeAsync(userId);
        if (scope.Scope == DashboardScope.None || !await CanAccessAsync())
        {
            return DashboardAnalyticsResult<IReadOnlyList<DashboardPhaseDurationItem>>.Forbidden();
        }

        var scopedProjectIds = await ApplyProjectScope(
                _db.Projets.Where(p => !p.EstSupprime),
                scope,
                userId)
            .Select(p => p.Id)
            .ToListAsync();

        var historiques = await _db.HistoriquePhasesProjets
            .Where(h => !h.EstSupprime &&
                        h.DateFin.HasValue &&
                        scopedProjectIds.Contains(h.ProjetId))
            .ToListAsync();

        var result = historiques
            .GroupBy(h => h.Phase)
            .Select(g => new DashboardPhaseDurationItem(
                g.Key.ToString(),
                Math.Round(g.Average(h => (h.DateFin!.Value - h.DateDebut).TotalDays), 1)))
            .OrderBy(x => x.Phase)
            .ToList();

        return DashboardAnalyticsResult<IReadOnlyList<DashboardPhaseDurationItem>>.Success(result);
    }

    public async Task<DashboardAnalyticsResult<DashboardKpiStats>> GetKpisAsync(Guid userId)
    {
        var scope = await ResolveScopeAsync(userId);
        if (scope.Scope == DashboardScope.None || !await CanAccessAsync())
        {
            return DashboardAnalyticsResult<DashboardKpiStats>.Forbidden();
        }

        var projets = await ApplyProjectScope(
                _db.Projets.Include(p => p.DemandeProjet).Where(p => !p.EstSupprime),
                scope,
                userId)
            .ToListAsync();

        var demandesQuery = ApplyDemandeScope(
            _db.DemandesProjets.Where(d => !d.EstSupprime),
            scope,
            userId);

        var demandesEnAttente = await demandesQuery.CountAsync(d =>
            d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
            d.StatutDemande == StatutDemande.EnAttenteValidationDSI);

        var now = DateTime.UtcNow;
        var projetsEnRetard = projets.Count(p =>
            p.DateFinPrevue.HasValue &&
            p.DateFinPrevue.Value < now &&
            p.StatutProjet != StatutProjet.Cloture &&
            p.StatutProjet != StatutProjet.Annule);

        var projetsMesures = projets.Where(p =>
            p.StatutProjet != StatutProjet.Cloture &&
            p.StatutProjet != StatutProjet.Annule).ToList();

        var tauxRetard = projetsMesures.Any()
            ? Math.Round((double)projetsEnRetard / projetsMesures.Count * 100, 1)
            : 0;

        var ficheIds = projets.Select(p => p.Id).ToHashSet();
        var fiches = await _db.FicheProjets
            .Where(f => ficheIds.Contains(f.ProjetId) && !f.EstSupprime)
            .ToListAsync();

        var projetsDepassementBudget = fiches.Count(f => f.EcartsBudget.HasValue && f.EcartsBudget.Value < 0);
        var tauxDepassementBudget = fiches.Any()
            ? Math.Round((double)projetsDepassementBudget / fiches.Count * 100, 1)
            : 0;

        var charges = await _db.ChargesProjets
            .Where(c => ficheIds.Contains(c.ProjetId) && !c.EstSupprime)
            .Include(c => c.Ressource)
            .ToListAsync();

        var totalChargePrevi = charges.Sum(c => (double)c.ChargePrevisionnelle);
        var totalChargeReelle = charges.Where(c => c.ChargeReelle.HasValue).Sum(c => (double)c.ChargeReelle!.Value);
        var tauxUtilisationCapacite = totalChargePrevi > 0
            ? Math.Round(totalChargeReelle / totalChargePrevi * 100, 1)
            : 0;

        var projetsDepassementCharge = charges
            .Where(c => c.ChargeReelle.HasValue && c.ChargeReelle.Value > c.ChargePrevisionnelle)
            .Select(c => c.ProjetId)
            .Distinct()
            .Count();

        var tauxDepassementCharge = ficheIds.Any()
            ? Math.Round((double)projetsDepassementCharge / ficheIds.Count * 100, 1)
            : 0;

        var data = new DashboardKpiStats(
            TotalProjets: projets.Count,
            ProjetsEnCours: projets.Count(p => p.StatutProjet == StatutProjet.EnCours),
            ProjetsCloturesThisYear: projets.Count(p => p.StatutProjet == StatutProjet.Cloture &&
                p.DateModification?.Year == DateTime.UtcNow.Year),
            ProjetsRouges: projets.Count(p => p.EtatProjet == EtatProjet.Rouge),
            ProjetsOranges: projets.Count(p => p.EtatProjet == EtatProjet.Orange),
            ProjetsVerts: projets.Count(p => p.EtatProjet == EtatProjet.Vert),
            AvancementMoyen: projets.Any() ? Math.Round(projets.Average(p => (double)p.PourcentageAvancementAffiche), 1) : 0,
            DemandesEnAttente: demandesEnAttente,
            ProjetsEnRetard: projetsEnRetard,
            TauxRetard: tauxRetard,
            TauxDepassementBudget: tauxDepassementBudget,
            TauxDepassementCharge: tauxDepassementCharge,
            TauxUtilisationCapacite: tauxUtilisationCapacite);

        return DashboardAnalyticsResult<DashboardKpiStats>.Success(data);
    }

    private async Task<bool> HasPortfolioScopeAsync()
    {
        return await CurrentUserHasAnyPermissionAsync(
            ("Projet", "Portefeuille"),
            ("Admin", "Users"),
            ("Autorisations", "Index"),
            ("DemandesAcces", "Index"));
    }

    private async Task<bool> HasDirectionScopeAsync()
    {
        return await CurrentUserHasAnyPermissionAsync(
            ("DemandeProjet", "ListeValidationDM"),
            ("Projet", "ValidationsProjet"),
            ("Projet", "ListeValidationClotureDM"),
            ("Projet", "ValiderCharteDM"),
            ("Projet", "ValiderPlanificationDM"),
            ("Projet", "ValiderRecette"));
    }

    private async Task<bool> HasChefProjetScopeAsync()
    {
        return await CurrentUserHasAnyPermissionAsync(
            ("Projet", "UpdateProgress"),
            ("Projet", "ValiderAnalyse"));
    }

    private async Task<bool> HasDemandeurScopeAsync()
    {
        return await CurrentUserHasAnyPermissionAsync(
            ("DemandeProjet", "Index"),
            ("DemandeProjet", "Create"),
            ("Projet", "ListeValidationClotureDemandeur"));
    }

    private async Task<DashboardScopeContext> ResolveScopeAsync(Guid userId)
    {
        if (await HasPortfolioScopeAsync())
        {
            return new DashboardScopeContext(DashboardScope.Portfolio);
        }

        if (await HasDirectionScopeAsync())
        {
            return new DashboardScopeContext(DashboardScope.Direction, await GetCurrentUserDirectionIdAsync(userId));
        }

        if (await HasChefProjetScopeAsync())
        {
            return new DashboardScopeContext(DashboardScope.ChefProjet);
        }

        if (await HasDemandeurScopeAsync())
        {
            return new DashboardScopeContext(DashboardScope.Demandeur);
        }

        return new DashboardScopeContext(DashboardScope.None);
    }

    private async Task<Guid?> GetCurrentUserDirectionIdAsync(Guid userId)
    {
        return await _db.Utilisateurs
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.EstSupprime)
            .Select(u => u.DirectionId)
            .FirstOrDefaultAsync();
    }

    private static IQueryable<Projet> ApplyProjectScope(
        IQueryable<Projet> query,
        DashboardScopeContext scope,
        Guid userId)
    {
        return scope.Scope switch
        {
            DashboardScope.Portfolio => query,
            DashboardScope.Direction when scope.DirectionId.HasValue => query.Where(p => p.DirectionId == scope.DirectionId.Value),
            DashboardScope.ChefProjet => query.Where(p => p.ChefProjetId == userId),
            DashboardScope.Demandeur => query.Where(p => p.DemandeProjet != null && p.DemandeProjet.DemandeurId == userId),
            _ => query.Where(p => false)
        };
    }

    private static IQueryable<DemandeProjet> ApplyDemandeScope(
        IQueryable<DemandeProjet> query,
        DashboardScopeContext scope,
        Guid userId)
    {
        return scope.Scope switch
        {
            DashboardScope.Portfolio => query,
            DashboardScope.Direction when scope.DirectionId.HasValue => query.Where(d => d.DirectionId == scope.DirectionId.Value),
            DashboardScope.ChefProjet => query.Where(d => d.Projet != null && d.Projet.ChefProjetId == userId),
            DashboardScope.Demandeur => query.Where(d => d.DemandeurId == userId),
            _ => query.Where(d => false)
        };
    }

    private async Task<bool> CurrentUserHasAnyPermissionAsync(params (string Controleur, string Action)[] permissions)
    {
        foreach (var permission in permissions)
        {
            if (await _permissionService.CurrentUserHasPermissionAsync(permission.Controleur, permission.Action))
            {
                return true;
            }
        }

        return false;
    }
}
