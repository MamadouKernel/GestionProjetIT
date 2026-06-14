namespace GestionProjects.Application.Common.Interfaces;

public interface IDashboardAnalyticsService
{
    Task<bool> CanAccessAsync();
    Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByPhaseItem>>> GetProjetsParPhaseAsync(Guid userId);
    Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByEtatItem>>> GetRagAsync(Guid userId);
    Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByDirectionItem>>> GetProjetsParDirectionAsync();
    Task<DashboardAnalyticsResult<IReadOnlyList<DashboardCountByStatutItem>>> GetProjetsParStatutAsync(Guid userId);
    Task<DashboardAnalyticsResult<IReadOnlyList<DashboardMonthlyTrendItem>>> GetTendanceMensuelleAsync(Guid userId);
    Task<DashboardAnalyticsResult<IReadOnlyList<DashboardPhaseDurationItem>>> GetDureeParPhaseAsync(Guid userId);
    Task<DashboardAnalyticsResult<DashboardKpiStats>> GetKpisAsync(Guid userId);
}

public sealed record DashboardAnalyticsResult<T>(bool IsForbidden, T? Data)
{
    public static DashboardAnalyticsResult<T> Forbidden() => new(true, default);
    public static DashboardAnalyticsResult<T> Success(T data) => new(false, data);
}

public sealed record DashboardCountByPhaseItem(string Phase, int Count);
public sealed record DashboardCountByEtatItem(string Etat, int Count);
public sealed record DashboardCountByDirectionItem(string Direction, int Count);
public sealed record DashboardCountByStatutItem(string Statut, int Count);
public sealed record DashboardMonthlyTrendItem(string Periode, int Count);
public sealed record DashboardPhaseDurationItem(string Phase, double DureeMoyenneJours);

public sealed record DashboardKpiStats(
    int TotalProjets,
    int ProjetsEnCours,
    int ProjetsCloturesThisYear,
    int ProjetsRouges,
    int ProjetsOranges,
    int ProjetsVerts,
    double AvancementMoyen,
    int DemandesEnAttente,
    int ProjetsEnRetard,
    double TauxRetard,
    double TauxDepassementBudget,
    double TauxDepassementCharge,
    double TauxUtilisationCapacite);
