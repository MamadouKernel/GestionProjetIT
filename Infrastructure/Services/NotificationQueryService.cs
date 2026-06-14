using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels.Notification;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class NotificationQueryService : INotificationQueryService
{
    private readonly ApplicationDbContext _db;

    public NotificationQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationIndexViewModel> GetIndexAsync(Guid userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var pagedResult = await _db.Notifications
            .Where(n => n.UtilisateurId == userId && !n.EstSupprime)
            .OrderByDescending(n => n.DateCreation)
            .ToPagedResultAsync(page, pageSize);

        var nonLues = await GetUnreadCountAsync(userId);

        return new NotificationIndexViewModel
        {
            Items = pagedResult.Items,
            NonLues = nonLues,
            PageNumber = pagedResult.PageNumber,
            TotalPages = pagedResult.TotalPages,
            TotalCount = pagedResult.TotalCount,
            PageSize = pagedResult.PageSize
        };
    }

    public Task<int> GetUnreadCountAsync(Guid userId)
    {
        return _db.Notifications
            .CountAsync(n => n.UtilisateurId == userId && !n.EstLue && !n.EstSupprime);
    }

    public async Task<IReadOnlyList<UnreadNotificationItem>> GetUnreadNotificationsAsync(Guid userId, int count)
    {
        count = Math.Clamp(count, 1, 20);

        var notifications = await _db.Notifications
            .Where(n => n.UtilisateurId == userId && !n.EstLue && !n.EstSupprime)
            .OrderByDescending(n => n.DateCreation)
            .Take(count)
            .Select(n => new
            {
                n.Id,
                n.Titre,
                n.Message,
                n.TypeNotification,
                n.EntiteType,
                n.EntiteId,
                n.DateCreation
            })
            .ToListAsync();

        return notifications
            .Select(n => new UnreadNotificationItem(
                n.Id,
                n.Titre,
                n.Message,
                n.TypeNotification,
                n.EntiteType,
                n.EntiteId,
                n.DateCreation,
                n.DateCreation.ToString("dd/MM/yyyy HH:mm")))
            .ToList();
    }

    public async Task<NotificationOpenInfo?> GetOpenInfoAsync(Guid notificationId, Guid userId)
    {
        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.Id == notificationId && n.UtilisateurId == userId && !n.EstSupprime)
            .Select(n => new NotificationOpenInfo(n.EntiteType, n.EntiteId))
            .FirstOrDefaultAsync();
    }
}
