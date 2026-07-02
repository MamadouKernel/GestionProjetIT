using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace GestionProjects.Infrastructure.Services;

public sealed class AzureAuthWorkflowService : IAzureAuthWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notificationService;

    public AzureAuthWorkflowService(
        ApplicationDbContext db,
        INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    public async Task<AzureDirectionDetection> DetectDirectionAsync(string? azureDepartment)
    {
        if (string.IsNullOrWhiteSpace(azureDepartment))
        {
            return new AzureDirectionDetection(null, null);
        }

        var normalizedDepartment = NormalizeForMatch(azureDepartment);
        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .ToListAsync();

        Direction? bestMatch = directions.FirstOrDefault(d => NormalizeForMatch(d.Code) == normalizedDepartment);
        bestMatch ??= directions.FirstOrDefault(d => NormalizeForMatch(d.Libelle) == normalizedDepartment);
        bestMatch ??= directions.FirstOrDefault(d => normalizedDepartment.Contains(NormalizeForMatch(d.Code)));
        bestMatch ??= directions.FirstOrDefault(d => normalizedDepartment.Contains(NormalizeForMatch(d.Libelle)));
        bestMatch ??= directions.FirstOrDefault(d => NormalizeForMatch(d.Libelle).Contains(normalizedDepartment));

        return new AzureDirectionDetection(bestMatch?.Id, bestMatch?.Libelle);
    }

    public Task<bool> HasPendingAccessRequestAsync(string email, string matricule)
    {
        return _db.DemandesAccesAzureAd
            .AnyAsync(d =>
                (d.Email == email || d.Matricule == matricule) &&
                d.Statut == StatutDemandeAcces.EnAttente);
    }

    public async Task RecordSuccessfulLoginAsync(Guid utilisateurId)
    {
        var utilisateur = await _db.Utilisateurs
            .FirstOrDefaultAsync(u => u.Id == utilisateurId && !u.EstSupprime);

        if (utilisateur == null)
        {
            return;
        }

        utilisateur.DateDerniereConnexion = DateTime.UtcNow;
        utilisateur.NombreConnexion++;
        await _db.SaveChangesAsync();
    }

    public async Task<DemandeAccesWorkflowResult> SoumettreDemandeAzureAsync(SoumettreDemandeAccesAzureInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Email) ||
            string.IsNullOrWhiteSpace(input.Matricule) ||
            string.IsNullOrWhiteSpace(input.Justification))
        {
            return DemandeAccesWorkflowResult.Error("Informations manquantes.");
        }

        var demandeExiste = await _db.DemandesAccesAzureAd
            .AnyAsync(d => d.Email == input.Email && d.Statut == StatutDemandeAcces.EnAttente);

        if (demandeExiste)
        {
            return DemandeAccesWorkflowResult.Info("Une demande d'accÃ¨s a dÃ©jÃ  Ã©tÃ© envoyÃ©e pour ce compte. Veuillez patienter.");
        }

        Guid? parsedDirectionId = null;
        if (Guid.TryParse(input.DirectionDetecteeId, out var directionId))
        {
            parsedDirectionId = directionId;
        }

        var demandeAcces = new DemandeAccesAzureAd
        {
            Id = Guid.NewGuid(),
            Email = input.Email.Trim(),
            Nom = (input.Nom ?? string.Empty).Trim(),
            Prenoms = (input.Prenom ?? string.Empty).Trim(),
            Matricule = input.Matricule.Trim(),
            Justification = input.Justification.Trim(),
            AzureDepartment = (input.AzureDepartment ?? string.Empty).Trim(),
            DirectionDetecteeId = parsedDirectionId,
            Statut = StatutDemandeAcces.EnAttente,
            CreePar = "AZURE_AD"
        };

        _db.DemandesAccesAzureAd.Add(demandeAcces);
        await _db.SaveChangesAsync();

        var directionLabel = "Non dÃ©terminÃ©e";
        if (parsedDirectionId.HasValue)
        {
            directionLabel = await _db.Directions
                .Where(d => d.Id == parsedDirectionId.Value)
                .Select(d => d.Libelle)
                .FirstOrDefaultAsync() ?? directionLabel;
        }

        await _notificationService.NotifierRoleAsync(
            RoleUtilisateur.AdminIT,
            TypeNotification.DemandeSupportTechnique,
            "Nouvelle demande d'accÃ¨s Azure AD",
            $"Demande d'accÃ¨s Azure AD de {demandeAcces.Prenoms} {demandeAcces.Nom} ({demandeAcces.Email}). Direction dÃ©tectÃ©e : {directionLabel}.",
            DomainEntityTypes.DemandeAccesAzureAd,
            demandeAcces.Id,
            new
            {
                demandeAcces.Email,
                demandeAcces.Matricule,
                demandeAcces.AzureDepartment,
                demandeAcces.Justification
            });

        return DemandeAccesWorkflowResult.Success(
            "Votre demande d'accÃ¨s a Ã©tÃ© envoyÃ©e aux administrateurs. Vous recevrez un retour aprÃ¨s traitement.",
            demandeAcces.Id);
    }

    private static string NormalizeForMatch(string value)
    {
        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
