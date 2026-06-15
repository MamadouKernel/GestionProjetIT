using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

public interface IDemandeAccesWorkflowService
{
    Task<DemandeAccesWorkflowResult> SoumettreDemandeLocaleAsync(SoumettreDemandeAccesLocaleInput input);
    Task<DemandeAccesWorkflowResult> ApprouverAsync(ApprouverDemandeAccesInput input);
    Task<DemandeAccesWorkflowResult> RejeterAsync(RejeterDemandeAccesInput input);
}

public sealed record SoumettreDemandeAccesLocaleInput(
    string Nom,
    string Prenoms,
    string Email,
    string Matricule,
    Guid DirectionId,
    string RolesSouhaites,
    string? Message);

public sealed record ApprouverDemandeAccesInput(
    Guid DemandeId,
    Guid? DirectionId,
    string? Commentaire,
    RoleUtilisateur Role,
    Guid TraiteParId);

public sealed record RejeterDemandeAccesInput(
    Guid DemandeId,
    string? Commentaire,
    Guid TraiteParId);

public sealed record DemandeAccesWorkflowResult(
    bool Succeeded,
    string? ErrorMessage,
    string? InfoMessage,
    string? SuccessMessage,
    Guid? FocusId = null)
{
    public static DemandeAccesWorkflowResult Success(string message, Guid? focusId = null) =>
        new(true, null, null, message, focusId);

    public static DemandeAccesWorkflowResult Error(string message, Guid? focusId = null) =>
        new(false, message, null, null, focusId);

    public static DemandeAccesWorkflowResult Info(string message, Guid? focusId = null) =>
        new(false, null, message, null, focusId);
}
