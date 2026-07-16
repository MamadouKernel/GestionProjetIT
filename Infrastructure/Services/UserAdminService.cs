using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Helpers;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionProjects.Infrastructure.Services;

public class UserAdminService : IUserAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IUtilisateurService _utilisateurService;
    private readonly IPasswordSetupTokenService _passwordSetupTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public UserAdminService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        IUtilisateurService utilisateurService,
        IPasswordSetupTokenService passwordSetupTokenService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _utilisateurService = utilisateurService;
        _passwordSetupTokenService = passwordSetupTokenService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<UsersListViewModel> GetListAsync(string? recherche, Guid? directionId, RoleUtilisateur? role, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = pageSize is 5 or 10 or 15 or 20 or 25 or 50 ? pageSize : 5;

        var query = _db.Utilisateurs
            .Include(u => u.Direction)
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(u =>
                u.Nom.Contains(recherche) ||
                u.Prenoms.Contains(recherche) ||
                u.Matricule.Contains(recherche) ||
                u.Email.Contains(recherche));

        if (directionId.HasValue)
            query = query.Where(u => u.DirectionId == directionId.Value);

        if (role.HasValue)
            query = query.Where(u => u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == role.Value));

        query = query.OrderBy(u => u.Nom).ThenBy(u => u.Prenoms);

        var paged = await query.ToPagedResultAsync(page, pageSize);

        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .OrderBy(d => d.Libelle)
            .ToListAsync();

        // Ids des directions avec un DM actif : enrichit visuellement la modale Creer
        // un utilisateur sans bloquer (l'AdminIT peut y vouloir affecter un premier DM).
        var directionsAvecDm = await _db.Utilisateurs
            .Where(u => !u.EstSupprime &&
                        u.DirectionId.HasValue &&
                        u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
            .Select(u => u.DirectionId!.Value)
            .Distinct()
            .ToListAsync();

        return new UsersListViewModel
        {
            Users               = paged.Items,
            Directions          = directions,
            DirectionsAvecDm    = directionsAvecDm.ToHashSet(),
            AllRoles            = Enum.GetValues<RoleUtilisateur>().ToList(),
            TotalCount          = paged.TotalCount,
            PageNumber          = paged.PageNumber,
            TotalPages          = paged.TotalPages,
            PageSize            = paged.PageSize,
            Recherche           = recherche,
            SelectedDirectionId = directionId,
            SelectedRole        = role
        };
    }

    public async Task<UserDetailsDto?> GetDetailsAsync(Guid id)
    {
        var user = await _db.Utilisateurs
            .Include(u => u.Direction)
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

        if (user == null)
            return null;

        var rolesActifs = user.GetRolesActifs().Select(r => (int)r).ToList();

        return new UserDetailsDto(
            user.Id, user.Matricule, user.Nom, user.Prenoms, user.Email,
            user.DirectionId?.ToString() ?? "",
            rolesActifs,
            rolesActifs.FirstOrDefault(),
            user.PeutCreerDemandeProjet,
            user.ProfilRessource.HasValue ? (int)user.ProfilRessource.Value : null,
            user.CapaciteHebdomadaire);
    }

    public async Task<OperationResult> CreateAsync(CreateUserInput input)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(input.Matricule)) errors.Add(new("Matricule", "Le matricule est requis."));
        if (string.IsNullOrWhiteSpace(input.Nom))       errors.Add(new("Nom", "Le nom est requis."));
        if (string.IsNullOrWhiteSpace(input.Prenoms))   errors.Add(new("Prenoms", "Les prénoms sont requis."));
        if (string.IsNullOrWhiteSpace(input.Email))     errors.Add(new("Email", "L'email est requis."));

        if (string.IsNullOrWhiteSpace(input.MotDePasse))
            errors.Add(new("motDePasse", "Le mot de passe est requis."));
        else if (!ValidationHelper.IsStrongPassword(input.MotDePasse))
            errors.Add(new("motDePasse", ValidationHelper.StrongPasswordPolicyMessage));

        if (string.IsNullOrWhiteSpace(input.ConfirmMotDePasse))
            errors.Add(new("confirmMotDePasse", "La confirmation du mot de passe est requise."));

        if (!string.IsNullOrWhiteSpace(input.MotDePasse) && !string.IsNullOrWhiteSpace(input.ConfirmMotDePasse)
            && input.MotDePasse != input.ConfirmMotDePasse)
            errors.Add(new("confirmMotDePasse", "Les mots de passe ne correspondent pas."));

        if (!string.IsNullOrWhiteSpace(input.Matricule) && await _utilisateurService.MatriculeExisteAsync(input.Matricule))
            errors.Add(new("Matricule", "Ce matricule existe déjà."));
        if (!string.IsNullOrWhiteSpace(input.Email) && await _utilisateurService.EmailExisteAsync(input.Email))
            errors.Add(new("Email", "Cet email existe déjà."));

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        var directionGuid = Guid.TryParse(input.DirectionId, out var dg) ? dg : (Guid?)null;
        var roles = _utilisateurService.ParseSelectedRoles(input.Roles);

        var user = await _utilisateurService.CreateUserAsync(
            input.Matricule!, input.Nom!, input.Prenoms!, input.Email!, input.MotDePasse!,
            directionGuid, roles, input.PeutCreerDemandeProjet,
            input.ProfilRessource, input.CapaciteHebdomadaire ?? 40);

        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("CreationUtilisateur", "Utilisateur", user.Id,
            null, new { user.Matricule, user.Nom, user.Prenoms, user.Email, Roles = roles });

        return OperationResult.Success("Utilisateur créé avec succès.");
    }

    public async Task<OperationResult> UpdateAsync(UpdateUserInput input)
    {
        // AsNoTracking : cette lecture sert uniquement aux comparaisons et au
        // contrôle d'existence. UpdateUserAsync recharge l'entité avec tracking.
        var existing = await _db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == input.Id && !u.EstSupprime);

        if (existing == null)
            return OperationResult.NotFound();

        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(input.Matricule)) errors.Add(new("Matricule", "Le matricule est requis."));
        if (string.IsNullOrWhiteSpace(input.Nom))       errors.Add(new("Nom", "Le nom est requis."));
        if (string.IsNullOrWhiteSpace(input.Prenoms))   errors.Add(new("Prenoms", "Les prénoms sont requis."));
        if (string.IsNullOrWhiteSpace(input.Email))     errors.Add(new("Email", "L'email est requis."));

        if (!string.IsNullOrWhiteSpace(input.Matricule) && input.Matricule != existing.Matricule
            && await _utilisateurService.MatriculeExisteAsync(input.Matricule, input.Id))
            errors.Add(new("Matricule", "Ce matricule existe déjà."));
        if (!string.IsNullOrWhiteSpace(input.Email) && input.Email != existing.Email
            && await _utilisateurService.EmailExisteAsync(input.Email, input.Id))
            errors.Add(new("Email", "Cet email existe déjà."));

        if (!string.IsNullOrEmpty(input.NouveauMotDePasse))
        {
            if (string.IsNullOrWhiteSpace(input.ConfirmNouveauMotDePasse))
                errors.Add(new("confirmNouveauMotDePasse", "La confirmation du mot de passe est requise."));
            else if (input.NouveauMotDePasse != input.ConfirmNouveauMotDePasse)
                errors.Add(new("confirmNouveauMotDePasse", "Les mots de passe ne correspondent pas."));
            else if (!ValidationHelper.IsStrongPassword(input.NouveauMotDePasse))
                errors.Add(new("nouveauMotDePasse", ValidationHelper.StrongPasswordPolicyMessage));
        }

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        var ancienMatricule = existing.Matricule;
        var directionGuid   = Guid.TryParse(input.DirectionId, out var dg) ? dg : (Guid?)null;
        var roles           = _utilisateurService.ParseSelectedRoles(input.Roles);

        await _utilisateurService.UpdateUserAsync(
            input.Id, input.Matricule!, input.Nom!, input.Prenoms!, input.Email!, directionGuid,
            roles, input.NouveauMotDePasse, input.PeutCreerDemandeProjet,
            input.ProfilRessource, input.CapaciteHebdomadaire);

        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("ModificationUtilisateur", "Utilisateur", input.Id,
            new { AncienMatricule = ancienMatricule },
            new { NouveauMatricule = input.Matricule, MotDePasseModifie = !string.IsNullOrEmpty(input.NouveauMotDePasse), Roles = roles });

        return OperationResult.Success("Utilisateur modifié avec succès.");
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        var user = await _db.Utilisateurs.FindAsync(id);
        if (user == null)
            return OperationResult.NotFound();

        user.EstSupprime      = true;
        user.DateModification = DateTime.UtcNow;
        user.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("DesactivationUtilisateur", "Utilisateur", user.Id,
            new { user.Matricule, user.Nom, user.Prenoms });

        return OperationResult.Success("Utilisateur désactivé.");
    }

    public async Task<OperationResult> ResetPasswordAsync(Guid id, string? nouveauMotDePasse)
    {
        var user = await _db.Utilisateurs.FindAsync(id);
        if (user == null)
            return OperationResult.NotFound();

        if (string.IsNullOrWhiteSpace(nouveauMotDePasse))
            return OperationResult.Invalid("nouveauMotDePasse", "Le mot de passe est requis.");

        if (!ValidationHelper.IsStrongPassword(nouveauMotDePasse))
            return OperationResult.Invalid("nouveauMotDePasse", ValidationHelper.StrongPasswordPolicyMessage);

        user.MotDePasse       = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);
        user.DateModification = DateTime.UtcNow;
        user.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("REINITIALISATION_MOT_DE_PASSE", "Utilisateur", user.Id,
            new { user.Matricule });

        return OperationResult.Success("Mot de passe réinitialisé avec succès.");
    }

    public async Task<OperationResult> RenvoyerLienActivationAsync(Guid id)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);
        if (user == null)
            return OperationResult.NotFound();

        if (string.IsNullOrWhiteSpace(user.Email))
            return OperationResult.Invalid("Email", "L'utilisateur n'a pas d'adresse email valide.");

        // Invalider les jetons précédents non utilisés (soft-delete) pour ne garder qu'un lien actif.
        var anciensJetons = await _db.JetonsInitialisationMotDePasse
            .Where(j => j.UtilisateurId == id && !j.EstSupprime && j.DateUtilisation == null)
            .ToListAsync();
        foreach (var j in anciensJetons)
        {
            j.EstSupprime      = true;
            j.DateModification = DateTime.UtcNow;
            j.ModifiePar       = _currentUser.Matricule;
        }

        var jeton = await _passwordSetupTokenService.CreerAsync(user.Id, _currentUser.Matricule ?? "SYSTEM");
        await _db.SaveChangesAsync();

        var lien = BuildActivationLink(user.Id, jeton.Token);
        var nomComplet = $"{user.Prenoms} {user.Nom}".Trim();
        await _emailService.EnvoyerActivationCompteAsync(
            user.Email, nomComplet, user.Matricule, lien, jeton.DateExpiration);

        await _audit.LogActionAsync("RENVOI_LIEN_ACTIVATION", "Utilisateur", user.Id,
            new { user.Matricule, user.Email });

        return OperationResult.Success($"Lien d'activation renvoyé à {user.Email}.");
    }

    private string BuildActivationLink(Guid utilisateurId, string token)
    {
        var baseUrl = (_configuration["SmtpSettings:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
        var path = $"/Account/InitialiserMotDePasse?utilisateurId={utilisateurId}&token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }
}
