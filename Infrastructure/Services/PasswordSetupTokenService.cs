using System.Security.Cryptography;
using System.Text;
using GestionProjects.Application.Common.Helpers;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionProjects.Infrastructure.Services;

public class PasswordSetupTokenService : IPasswordSetupTokenService
{
    private const int TokenByteLength = 32;
    private const int DefaultExpirationHours = 24;

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public PasswordSetupTokenService(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<PasswordSetupTokenCreation> CreerAsync(Guid utilisateurId, string creePar)
    {
        var now = DateTime.UtcNow;
        var existingTokens = await _db.JetonsInitialisationMotDePasse
            .Where(j => j.UtilisateurId == utilisateurId && !j.EstSupprime && j.DateUtilisation == null)
            .ToListAsync();

        foreach (var existing in existingTokens)
        {
            existing.EstSupprime = true;
            existing.DateModification = now;
            existing.ModifiePar = creePar;
        }

        var rawToken = GenerateToken();
        var expiration = now.AddHours(GetExpirationHours());

        _db.JetonsInitialisationMotDePasse.Add(new JetonInitialisationMotDePasse
        {
            Id = Guid.NewGuid(),
            UtilisateurId = utilisateurId,
            TokenHash = HashToken(rawToken),
            DateExpiration = expiration,
            DateCreation = now,
            CreePar = creePar,
            EstSupprime = false
        });

        return new PasswordSetupTokenCreation(rawToken, expiration);
    }

    public async Task<OperationResult> InitialiserMotDePasseAsync(
        Guid utilisateurId,
        string token,
        string nouveauMotDePasse,
        string? ip,
        string modifiePar)
    {
        if (utilisateurId == Guid.Empty || string.IsNullOrWhiteSpace(token))
            return OperationResult.Invalid("Token", "Le lien d'activation est invalide.");

        if (!ValidationHelper.IsStrongPassword(nouveauMotDePasse))
            return OperationResult.Invalid("NouveauMotDePasse", ValidationHelper.StrongPasswordPolicyMessage);

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token);
        var jeton = await _db.JetonsInitialisationMotDePasse
            .Include(j => j.Utilisateur)
            .FirstOrDefaultAsync(j =>
                j.UtilisateurId == utilisateurId &&
                j.TokenHash == tokenHash &&
                !j.EstSupprime);

        if (jeton == null || jeton.DateUtilisation != null || jeton.DateExpiration < now || jeton.Utilisateur.EstSupprime)
            return OperationResult.Invalid("Token", "Le lien d'activation est invalide ou expire.");

        jeton.DateUtilisation = now;
        jeton.UtiliseDepuisIp = ip;
        jeton.DateModification = now;
        jeton.ModifiePar = modifiePar;

        jeton.Utilisateur.MotDePasse = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);
        jeton.Utilisateur.DateModification = now;
        jeton.Utilisateur.ModifiePar = modifiePar;

        var autresJetons = await _db.JetonsInitialisationMotDePasse
            .Where(j =>
                j.UtilisateurId == utilisateurId &&
                j.Id != jeton.Id &&
                !j.EstSupprime &&
                j.DateUtilisation == null)
            .ToListAsync();

        foreach (var autreJeton in autresJetons)
        {
            autreJeton.EstSupprime = true;
            autreJeton.DateModification = now;
            autreJeton.ModifiePar = modifiePar;
        }

        await _db.SaveChangesAsync();
        return OperationResult.Success("Mot de passe initialise avec succes.");
    }

    private int GetExpirationHours()
    {
        var configured = _configuration.GetValue<int?>("Security:PasswordSetupTokenHours");
        return configured is > 0 and <= 168 ? configured.Value : DefaultExpirationHours;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
