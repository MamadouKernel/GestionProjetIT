namespace GestionProjects.Application.Common.Interfaces;

public interface INotificationTargetResolver
{
    Task<NotificationTarget> ResolveAsync(string? entiteType, Guid? entiteId);
}

public sealed record NotificationTarget(
    string Controller,
    string Action,
    IReadOnlyDictionary<string, object?> RouteValues)
{
    public static NotificationTarget NotificationsIndex() =>
        new("Notification", "Index", new Dictionary<string, object?>());
}
