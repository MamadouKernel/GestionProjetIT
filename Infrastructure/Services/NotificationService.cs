using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GestionProjects.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        public async Task NotifierUtilisateurAsync(
            Guid utilisateurId,
            TypeNotification type,
            string titre,
            string message,
            string? entiteType = null,
            Guid? entiteId = null,
            object? donneesSupplementaires = null)
        {
            try
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UtilisateurId = utilisateurId,
                    TypeNotification = type,
                    Titre = titre,
                    Message = message,
                    EntiteType = entiteType,
                    EntiteId = entiteId,
                    EstLue = false,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    DonneesSupplementaires = donneesSupplementaires != null ? JsonSerializer.Serialize(donneesSupplementaires) : null
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log l'erreur mais ne pas faire échouer l'opération principale
                _logger.LogError(ex, 
                    "Erreur lors de l'envoi de la notification. UtilisateurId: {UtilisateurId}, Type: {Type}, Titre: {Titre}", 
                    utilisateurId, type, titre);
            }
        }

        public async Task NotifierRoleAsync(
            RoleUtilisateur role,
            TypeNotification type,
            string titre,
            string message,
            string? entiteType = null,
            Guid? entiteId = null,
            object? donneesSupplementaires = null)
        {
            try
            {
                // Récupérer tous les utilisateurs avec ce rôle actif
                var utilisateurs = await _context.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && 
                               u.UtilisateurRoles.Any(ur => !ur.EstSupprime && 
                                                           ur.Role == role &&
                                                           (!ur.DateDebut.HasValue || ur.DateDebut.Value <= DateTime.Now) &&
                                                           (!ur.DateFin.HasValue || ur.DateFin.Value >= DateTime.Now)))
                    .ToListAsync();

                foreach (var utilisateur in utilisateurs)
                {
                    await NotifierUtilisateurAsync(utilisateur.Id, type, titre, message, entiteType, entiteId, donneesSupplementaires);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Erreur lors de l'envoi de notifications par rôle. Role: {Role}, Type: {Type}, Titre: {Titre}", 
                    role, type, titre);
            }
        }

        public async Task NotifierResponsablesSolutionsITAsync(
            TypeNotification type,
            string titre,
            string message,
            string? entiteType = null,
            Guid? entiteId = null,
            object? donneesSupplementaires = null)
        {
            await NotifierRoleAsync(RoleUtilisateur.ResponsableSolutionsIT, type, titre, message, entiteType, entiteId, donneesSupplementaires);
        }

        public async Task MarquerCommeLueAsync(Guid notificationId, Guid utilisateurId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UtilisateurId == utilisateurId && !n.EstSupprime);

                if (notification != null && !notification.EstLue)
                {
                    notification.EstLue = true;
                    notification.DateLecture = DateTime.Now;
                    notification.DateModification = DateTime.Now;
                    notification.ModifiePar = _currentUserService.Matricule;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Erreur lors du marquage de la notification comme lue. NotificationId: {NotificationId}, UtilisateurId: {UtilisateurId}", 
                    notificationId, utilisateurId);
            }
        }

        public async Task MarquerToutesCommeLuesAsync(Guid utilisateurId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UtilisateurId == utilisateurId && !n.EstLue && !n.EstSupprime)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.EstLue = true;
                    notification.DateLecture = DateTime.Now;
                    notification.DateModification = DateTime.Now;
                    notification.ModifiePar = _currentUserService.Matricule;
                }

                if (notifications.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Erreur lors du marquage de toutes les notifications comme lues. UtilisateurId: {UtilisateurId}", 
                    utilisateurId);
            }
        }
    }
}

