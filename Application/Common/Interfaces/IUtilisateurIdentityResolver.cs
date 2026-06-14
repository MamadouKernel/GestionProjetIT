using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces;

public interface IUtilisateurIdentityResolver
{
    Task<UtilisateurIdentityResolution> ResolveActiveUserAsync(
        string? email,
        string? matricule,
        UtilisateurIdentityResolutionMode mode,
        bool includeRoles = false,
        bool includeDirection = false);
}

public enum UtilisateurIdentityResolutionMode
{
    Strict = 1,
    PreferEmail = 2
}

public sealed record UtilisateurIdentityResolution(
    Utilisateur? Utilisateur,
    string? ErrorMessage)
{
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public static UtilisateurIdentityResolution Success(Utilisateur? utilisateur) =>
        new(utilisateur, null);

    public static UtilisateurIdentityResolution Error(string message) =>
        new(null, message);
}
