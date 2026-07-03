using GestionProjects.Application.Common.Results;

namespace GestionProjects.Application.Common.Interfaces;

public interface IPasswordSetupTokenService
{
    Task<PasswordSetupTokenCreation> CreerAsync(Guid utilisateurId, string creePar, bool estReinitialisation = false);
    Task<OperationResult> InitialiserMotDePasseAsync(
        Guid utilisateurId,
        string token,
        string nouveauMotDePasse,
        string? ip,
        string modifiePar);
}

public sealed record PasswordSetupTokenCreation(string Token, DateTime DateExpiration);
