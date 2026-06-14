using GestionProjects.Application.ViewModels;

namespace GestionProjects.Application.Common.Interfaces;

public delegate string? DashboardUrlFactory(string action, string controller, object? values);

public interface IHomeDashboardService
{
    Task<DashboardStatsViewModel> BuildAsync(Guid userId, DashboardUrlFactory urlFactory);
}
