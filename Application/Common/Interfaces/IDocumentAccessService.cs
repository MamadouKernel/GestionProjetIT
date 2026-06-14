namespace GestionProjects.Application.Common.Interfaces;

public interface IDocumentAccessService
{
    Task<DocumentAccessResult> GetDemandeCahierAsync(Guid demandeId, Guid userId);
    Task<DocumentAccessResult> GetDemandeAnnexeAsync(Guid demandeId, Guid documentId, Guid userId);
    Task<DocumentAccessResult> GetProjetLivrableAsync(Guid projetId, Guid livrableId, Guid userId);
    Task<DocumentAccessResult> GetDossierSignatureSourceAsync(Guid projetId, Guid dossierId, Guid userId);
}

public sealed record DocumentAccessResult(
    bool IsNotFound,
    bool IsForbidden,
    string? RelativePath,
    string? FileName,
    string? Title)
{
    public static DocumentAccessResult NotFound() => new(true, false, null, null, null);
    public static DocumentAccessResult Forbidden() => new(false, true, null, null, null);

    public static DocumentAccessResult Success(string relativePath, string fileName, string title) =>
        new(false, false, relativePath, fileName, title);
}
