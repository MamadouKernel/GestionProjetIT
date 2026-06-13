namespace GestionProjects.Application.Common.Results;

/// <summary>
/// Résultat d'une opération applicative (création, modification, suppression).
/// Permet à un service de signaler succès / échec de validation / introuvable
/// sans dépendre des types HTTP du controller.
/// </summary>
public sealed class OperationResult
{
    public bool Succeeded { get; private init; }
    public bool IsNotFound { get; private init; }
    public string? SuccessMessage { get; private init; }
    public IReadOnlyList<FieldError> Errors { get; private init; } = Array.Empty<FieldError>();

    public static OperationResult Success(string message) =>
        new() { Succeeded = true, SuccessMessage = message };

    public static OperationResult NotFound() =>
        new() { Succeeded = false, IsNotFound = true };

    public static OperationResult Invalid(IEnumerable<FieldError> errors) =>
        new() { Succeeded = false, Errors = errors.ToList() };

    public static OperationResult Invalid(string field, string message) =>
        new() { Succeeded = false, Errors = new List<FieldError> { new(field, message) } };
}

public sealed record FieldError(string Field, string Message);
