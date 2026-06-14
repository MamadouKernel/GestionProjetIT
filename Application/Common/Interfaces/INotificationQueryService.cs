using GestionProjects.Application.ViewModels.Notification;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

public interface INotificationQueryService
{
    Task<NotificationIndexViewModel> GetIndexAsync(Guid userId, int page, int pageSize);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<IReadOnlyList<UnreadNotificationItem>> GetUnreadNotificationsAsync(Guid userId, int count);
    Task<NotificationOpenInfo?> GetOpenInfoAsync(Guid notificationId, Guid userId);
}

public sealed record UnreadNotificationItem(
    Guid Id,
    string Titre,
    string Message,
    TypeNotification TypeNotification,
    string? EntiteType,
    Guid? EntiteId,
    DateTime DateCreation,
    string DateCreationFormatted);

public sealed record NotificationOpenInfo(string? EntiteType, Guid? EntiteId);
