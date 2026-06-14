using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class NotificationTargetResolver : INotificationTargetResolver
{
    private readonly ApplicationDbContext _db;

    public NotificationTargetResolver(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationTarget> ResolveAsync(string? entiteType, Guid? entiteId)
    {
        if (string.IsNullOrWhiteSpace(entiteType) || !entiteId.HasValue)
        {
            return NotificationTarget.NotificationsIndex();
        }

        return entiteType switch
        {
            DomainEntityTypes.Projet => ProjectDetails(entiteId.Value),
            DomainEntityTypes.DemandeProjet => DemandDetails(entiteId.Value),
            DomainEntityTypes.DemandeAccesAzureAd => AccessRequest(entiteId.Value),
            DomainEntityTypes.DemandeCreationCompte => AccountRequests(),
            DomainEntityTypes.DemandeClotureProjet => await ClosureRequestAsync(entiteId.Value),
            DomainEntityTypes.AnomalieProjet => await ProjectChildAsync(
                await _db.AnomaliesProjets
                    .Where(a => a.Id == entiteId.Value && !a.EstSupprime)
                    .Select(a => (Guid?)a.ProjetId)
                    .FirstOrDefaultAsync(),
                ProjectDetailTabs.Execution),
            DomainEntityTypes.LivrableProjet => await ProjectChildAsync(
                await _db.LivrablesProjets
                    .Where(l => l.Id == entiteId.Value && !l.EstSupprime)
                    .Select(l => (Guid?)l.ProjetId)
                    .FirstOrDefaultAsync(),
                ProjectDetailTabs.Livrables),
            _ => NotificationTarget.NotificationsIndex()
        };
    }

    private static NotificationTarget ProjectDetails(Guid projetId, string? tab = null)
    {
        var routeValues = new Dictionary<string, object?> { ["id"] = projetId };
        if (!string.IsNullOrWhiteSpace(tab))
        {
            routeValues["tab"] = tab;
        }

        return new NotificationTarget(DomainEntityTypes.Projet, "Details", routeValues);
    }

    private static NotificationTarget DemandDetails(Guid demandeId) =>
        new(DomainEntityTypes.DemandeProjet, "Details", new Dictionary<string, object?> { ["id"] = demandeId });

    private static NotificationTarget AccessRequest(Guid demandeAccesId) =>
        new("DemandesAcces", "Index", new Dictionary<string, object?> { ["focusId"] = demandeAccesId });

    private static NotificationTarget AccountRequests() =>
        new("Admin", "DemandesCreationCompte", new Dictionary<string, object?>());

    private async Task<NotificationTarget> ClosureRequestAsync(Guid demandeClotureId)
    {
        var projetId = await _db.DemandesClotureProjets
            .Where(d => d.Id == demandeClotureId && !d.EstSupprime)
            .Select(d => (Guid?)d.ProjetId)
            .FirstOrDefaultAsync();

        return await ProjectChildAsync(projetId, ProjectDetailTabs.Cloture);
    }

    private static Task<NotificationTarget> ProjectChildAsync(Guid? projetId, string tab)
    {
        return Task.FromResult(projetId.HasValue
            ? ProjectDetails(projetId.Value, tab)
            : NotificationTarget.NotificationsIndex());
    }
}
