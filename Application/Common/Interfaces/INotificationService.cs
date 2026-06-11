using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task NotifierUtilisateurAsync(Guid utilisateurId, TypeNotification type, string titre, string message, string? entiteType = null, Guid? entiteId = null, object? donneesSupplementaires = null);
        Task NotifierRoleAsync(RoleUtilisateur role, TypeNotification type, string titre, string message, string? entiteType = null, Guid? entiteId = null, object? donneesSupplementaires = null);
        Task NotifierResponsablesSolutionsITAsync(TypeNotification type, string titre, string message, string? entiteType = null, Guid? entiteId = null, object? donneesSupplementaires = null);
        Task MarquerCommeLueAsync(Guid notificationId, Guid utilisateurId);
        Task MarquerToutesCommeLuesAsync(Guid utilisateurId);
    }
}

