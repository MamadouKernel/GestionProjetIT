using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Application.ViewModels.Account;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionProjects.Infrastructure.Services;

public sealed class AccountService : IAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordSetupTokenService _passwordSetupTokenService;
    private readonly IEmailService _emailService;
    private readonly IAuditService _audit;
    private readonly IConfiguration _configuration;

    public AccountService(
        ApplicationDbContext db,
        IPasswordSetupTokenService passwordSetupTokenService,
        IEmailService emailService,
        IAuditService audit,
        IConfiguration configuration)
    {
        _db = db;
        _passwordSetupTokenService = passwordSetupTokenService;
        _emailService = emailService;
        _audit = audit;
        _configuration = configuration;
    }

    public async Task<AccountLoginResult> ValidateLocalLoginAsync(LoginViewModel model)
    {
        var user = await _db.Utilisateurs
            .Include(u => u.Direction)
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Matricule == model.Matricule && !u.EstSupprime);

        if (user == null)
        {
            return new AccountLoginResult(null, "Matricule ou mot de passe incorrect.");
        }

        if (string.IsNullOrEmpty(user.MotDePasse))
        {
            return new AccountLoginResult(null, "Ce compte est en attente d'activation. Utilisez le lien recu par email ou contactez la DSI.");
        }

        try
        {
            if (string.IsNullOrEmpty(user.MotDePasse))
            {
                return new AccountLoginResult(null, "Erreur : mot de passe utilisateur invalide.");
            }

            if (!user.MotDePasse.StartsWith("$2"))
            {
                return new AccountLoginResult(null, "Erreur : format de mot de passe invalide.");
            }

            var passwordOk = BCrypt.Net.BCrypt.Verify(model.MotDePasse, user.MotDePasse);
            return passwordOk
                ? new AccountLoginResult(user, null)
                : new AccountLoginResult(null, "Matricule ou mot de passe incorrect.");
        }
        catch (Exception ex)
        {
            return new AccountLoginResult(null, $"Erreur lors de la vérification du mot de passe : {ex.Message}");
        }
    }

    public async Task RecordLoginAsync(Guid userId)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == userId && !u.EstSupprime);
        if (user == null)
        {
            return;
        }

        user.NombreConnexion += 1;
        user.DateDerniereConnexion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<ProfilViewModel?> GetProfilAsync(Guid userId)
    {
        var user = await LoadProfileUserAsync(userId);
        return user == null ? null : BuildProfilViewModel(user);
    }

    public async Task<AccountProfileUpdateResult> UpdateProfilAsync(
        Guid userId,
        ProfilViewModel model,
        bool hasModelStateErrors)
    {
        var user = await LoadProfileUserAsync(userId);
        if (user == null)
        {
            return new AccountProfileUpdateResult(
                NotFound: true,
                Succeeded: false,
                ViewModel: model,
                User: null,
                Errors: Array.Empty<AccountValidationError>());
        }

        var errors = new List<AccountValidationError>();
        var nouveauMotDePasse = model.NouveauMotDePasse;

        if (!string.IsNullOrEmpty(nouveauMotDePasse))
        {
            if (string.IsNullOrEmpty(model.MotDePasseActuel))
            {
                errors.Add(new AccountValidationError(
                    nameof(model.MotDePasseActuel),
                    "Le mot de passe actuel est requis pour changer le mot de passe."));
            }
            else
            {
                try
                {
                    if (!BCrypt.Net.BCrypt.Verify(model.MotDePasseActuel, user.MotDePasse))
                    {
                        errors.Add(new AccountValidationError(
                            nameof(model.MotDePasseActuel),
                            "Le mot de passe actuel est incorrect."));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new AccountValidationError(
                        string.Empty,
                        $"Erreur lors de la vérification du mot de passe : {ex.Message}"));
                }
            }
        }

        if (hasModelStateErrors || errors.Count > 0)
        {
            var invalidModel = PrepareProfilViewModelForRedisplay(model, user);
            return new AccountProfileUpdateResult(
                NotFound: false,
                Succeeded: false,
                ViewModel: invalidModel,
                User: user,
                Errors: errors);
        }

        user.Nom = model.Nom;
        user.Prenoms = model.Prenoms;
        user.Email = model.Email;

        if (!string.IsNullOrEmpty(nouveauMotDePasse))
        {
            user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);
        }

        await _db.SaveChangesAsync();

        return new AccountProfileUpdateResult(
            NotFound: false,
            Succeeded: true,
            ViewModel: BuildProfilViewModel(user),
            User: user,
            Errors: Array.Empty<AccountValidationError>());
    }

    public async Task<InscriptionViewModel> BuildInscriptionViewModelAsync()
    {
        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .OrderBy(d => d.Libelle)
            .Select(d => new DirectionSelectItem { Id = d.Id, Libelle = d.Libelle })
            .ToListAsync();

        return new InscriptionViewModel { Directions = directions };
    }

    public async Task<IReadOnlyList<AccountLookupItem>> GetServicesByDirectionAsync(Guid directionId)
    {
        return await _db.Services
            .Where(s => s.DirectionId == directionId && !s.EstSupprime && s.EstActive)
            .OrderBy(s => s.Libelle)
            .Select(s => new AccountLookupItem(s.Id, s.Libelle))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AccountLookupItem>> GetDirecteursMetierByDirectionAsync(Guid directionId)
    {
        return await _db.Utilisateurs
            .Where(u => !u.EstSupprime && u.DirectionId == directionId)
            .Join(
                _db.UtilisateurRoles.Where(r => r.Role == RoleUtilisateur.DirecteurMetier && !r.EstSupprime),
                u => u.Id,
                r => r.UtilisateurId,
                (u, r) => u)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenoms)
            .Select(u => new AccountLookupItem(u.Id, (u.Nom + " " + u.Prenoms).Trim()))
            .ToListAsync();
    }

    public async Task DemarrerReinitialisationMotDePasseAsync(string matricule, string email, string? ip)
    {
        // Par securite (anti-enumeration), cette methode ne renvoie jamais d'information sur
        // l'existence du compte : l'appelant (controller) affiche toujours le meme message,
        // que le matricule/email corresponde ou non a un utilisateur reel.
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u =>
            !u.EstSupprime &&
            u.Matricule == matricule &&
            u.Email != null && u.Email.ToLower() == email.Trim().ToLower());

        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var anciensJetons = await _db.JetonsInitialisationMotDePasse
            .Where(j => j.UtilisateurId == user.Id && !j.EstSupprime && j.DateUtilisation == null)
            .ToListAsync();
        foreach (var j in anciensJetons)
        {
            j.EstSupprime = true;
            j.DateModification = DateTime.UtcNow;
            j.ModifiePar = "SYSTEM";
        }

        var jeton = await _passwordSetupTokenService.CreerAsync(user.Id, "SYSTEM");
        await _db.SaveChangesAsync();

        var lien = BuildReinitialisationLink(user.Id, jeton.Token);
        var nomComplet = $"{user.Prenoms} {user.Nom}".Trim();
        await _emailService.EnvoyerReinitialisationMotDePasseAsync(
            user.Email, nomComplet, user.Matricule, lien, jeton.DateExpiration);

        await _audit.LogActionAsync("DEMANDE_REINITIALISATION_MOT_DE_PASSE", "Utilisateur", user.Id,
            new { user.Matricule, ip });
    }

    private string BuildReinitialisationLink(Guid utilisateurId, string token)
    {
        var baseUrl = (_configuration["SmtpSettings:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
        var path = $"/Account/InitialiserMotDePasse?utilisateurId={utilisateurId}&token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }

    private Task<Utilisateur?> LoadProfileUserAsync(Guid userId)
    {
        return _db.Utilisateurs
            .Include(u => u.Direction)
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.EstSupprime);
    }

    private static ProfilViewModel BuildProfilViewModel(Utilisateur user)
    {
        return new ProfilViewModel
        {
            Id = user.Id,
            Matricule = user.Matricule,
            Nom = user.Nom,
            Prenoms = user.Prenoms,
            Email = user.Email,
            DirectionLibelle = user.Direction?.Libelle,
            Role = user.Role.ToString(),
            DateDerniereConnexion = user.DateDerniereConnexion,
            NombreConnexion = user.NombreConnexion
        };
    }

    private static ProfilViewModel PrepareProfilViewModelForRedisplay(ProfilViewModel model, Utilisateur user)
    {
        model.NouveauMotDePasse = null;
        model.ConfirmerMotDePasse = null;
        model.MotDePasseActuel = null;
        model.DirectionLibelle = user.Direction?.Libelle;
        model.Role = user.Role.ToString();
        model.DateDerniereConnexion = user.DateDerniereConnexion;
        model.NombreConnexion = user.NombreConnexion;
        return model;
    }
}
