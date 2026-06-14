using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class UtilisateurIdentityResolver : IUtilisateurIdentityResolver
{
    private readonly ApplicationDbContext _db;

    public UtilisateurIdentityResolver(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UtilisateurIdentityResolution> ResolveActiveUserAsync(
        string? email,
        string? matricule,
        UtilisateurIdentityResolutionMode mode,
        bool includeRoles = false,
        bool includeDirection = false)
    {
        var emailNormalise = email?.Trim();
        var matriculeNormalise = matricule?.Trim();

        if (string.IsNullOrWhiteSpace(emailNormalise) && string.IsNullOrWhiteSpace(matriculeNormalise))
        {
            return UtilisateurIdentityResolution.Error("Email ou matricule requis pour identifier l'utilisateur.");
        }

        IQueryable<Utilisateur> query = _db.Utilisateurs.Where(u => !u.EstSupprime);

        if (includeRoles)
        {
            query = query.Include(u => u.UtilisateurRoles);
        }

        if (includeDirection)
        {
            query = query.Include(u => u.Direction);
        }

        var matches = await query
            .Where(u =>
                (!string.IsNullOrWhiteSpace(emailNormalise) && u.Email == emailNormalise) ||
                (!string.IsNullOrWhiteSpace(matriculeNormalise) && u.Matricule == matriculeNormalise))
            .ToListAsync();

        var utilisateurParEmail = matches.FirstOrDefault(u => SameIdentityValue(u.Email, emailNormalise));
        var utilisateurParMatricule = matches.FirstOrDefault(u => SameIdentityValue(u.Matricule, matriculeNormalise));

        if (utilisateurParEmail != null &&
            utilisateurParMatricule != null &&
            utilisateurParEmail.Id != utilisateurParMatricule.Id)
        {
            return UtilisateurIdentityResolution.Error(
                "L'e-mail et le matricule correspondent a deux comptes utilisateurs differents.");
        }

        if (mode == UtilisateurIdentityResolutionMode.Strict &&
            utilisateurParEmail != null &&
            utilisateurParMatricule == null &&
            !SameIdentityValue(utilisateurParEmail.Matricule, matriculeNormalise))
        {
            return UtilisateurIdentityResolution.Error(
                $"L'e-mail est deja associe au matricule {utilisateurParEmail.Matricule}.");
        }

        var utilisateur = mode == UtilisateurIdentityResolutionMode.PreferEmail
            ? utilisateurParEmail ?? utilisateurParMatricule
            : utilisateurParMatricule ?? utilisateurParEmail;

        return UtilisateurIdentityResolution.Success(utilisateur);
    }

    private static bool SameIdentityValue(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
