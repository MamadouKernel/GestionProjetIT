using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GestionProjects.Infrastructure.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string typeAction, string entite, Guid? entiteId, object? anciennesValeurs = null, object? nouvellesValeurs = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogActionAsync(
            string typeAction,
            string entite,
            Guid? entiteId,
            object? anciennesValeurs = null,
            object? nouvellesValeurs = null)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                var (ip, userAgent) = GetRequestInfo();

                var log = CreateAuditLog(
                    typeAction, 
                    entite, 
                    entiteId, 
                    user?.Id, 
                    ip, 
                    userAgent, 
                    anciennesValeurs, 
                    nouvellesValeurs,
                    _currentUserService.Matricule ?? string.Empty);

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log l'erreur mais ne pas faire échouer l'opération principale
                _logger.LogError(ex, 
                    "Erreur lors de l'enregistrement de l'audit. TypeAction: {TypeAction}, Entite: {Entite}, EntiteId: {EntiteId}", 
                    typeAction, entite, entiteId);
            }
        }

        private async Task<Domain.Models.Utilisateur?> GetCurrentUserAsync()
        {
            if (string.IsNullOrWhiteSpace(_currentUserService.Matricule))
                return null;

            return await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Matricule == _currentUserService.Matricule);
        }

        private (string ip, string userAgent) GetRequestInfo()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ip = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";
            return (ip, userAgent);
        }

        private static AuditLog CreateAuditLog(
            string typeAction,
            string entite,
            Guid? entiteId,
            Guid? utilisateurId,
            string ip,
            string userAgent,
            object? anciennesValeurs,
            object? nouvellesValeurs,
            string creePar)
        {
            return new AuditLog
            {
                Id = Guid.NewGuid(),
                UtilisateurId = utilisateurId,
                DateAction = DateTime.Now,
                TypeAction = typeAction,
                Entite = entite,
                EntiteId = entiteId?.ToString() ?? string.Empty,
                AnciennesValeurs = SerializeIfNotNull(anciennesValeurs),
                NouvellesValeurs = SerializeIfNotNull(nouvellesValeurs),
                AdresseIP = ip,
                UserAgent = userAgent,
                DateCreation = DateTime.Now,
                CreePar = creePar
            };
        }

        private static string SerializeIfNotNull(object? value)
        {
            return value != null ? JsonSerializer.Serialize(value) : string.Empty;
        }
    }
}

