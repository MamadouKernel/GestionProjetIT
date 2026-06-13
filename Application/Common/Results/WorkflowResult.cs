namespace GestionProjects.Application.Common.Results;

/// <summary>
/// Résultat d'une action de workflow (validation/refus en plusieurs étapes).
/// Couvre les cas introuvable / interdit / erreur métier (message + redirection)
/// / succès, sans dépendre des types HTTP.
/// </summary>
public sealed class WorkflowResult
{
    public bool Succeeded { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsForbidden { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? SuccessMessage { get; private init; }

    public static WorkflowResult Success(string message) =>
        new() { Succeeded = true, SuccessMessage = message };

    public static WorkflowResult NotFound() =>
        new() { IsNotFound = true };

    public static WorkflowResult Forbidden() =>
        new() { IsForbidden = true };

    /// <summary>Erreur métier non bloquante : message affiché puis redirection (pas de re-render).</summary>
    public static WorkflowResult Error(string message) =>
        new() { ErrorMessage = message };
}
