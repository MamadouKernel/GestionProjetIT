using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels.DemandeProjet;
using Microsoft.AspNetCore.Http;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Transitions du workflow d'une demande de projet (validation/rejet/correction,
/// côté Directeur Métier et DSI). Le controller fait le gating de permission ;
/// le service applique l'appartenance, l'état, la persistance et les notifications.
/// (Soumission sera ajoutée dans un incrément ultérieur.)
/// </summary>
public interface IDemandeProjetWorkflowService
{
    Task<WorkflowResult> ValiderDmAsync(
        Guid id, string? commentaire,
        string? titre, string? description, string? objectifs, string? avantagesAttendus,
        Guid currentUserId, bool hasAdminScope, string nomActeur);

    Task<WorkflowResult> RejeterDmAsync(
        Guid id, string commentaire, Guid currentUserId, bool hasAdminScope, string nomActeur);

    Task<WorkflowResult> DemanderCorrectionDmAsync(
        Guid id, string commentaire, Guid currentUserId, bool hasAdminScope, string nomActeur);

    // ── Côté DSI ──────────────────────────────────────────────────────────────
    Task<ValiderDsiResult> ValiderDsiAsync(
        Guid id, string? commentaire, Guid? chefProjetId, bool isDelegue, string nomActeur);

    Task<WorkflowResult> RejeterDsiAsync(
        Guid id, string? commentaire, bool isDelegue, string nomActeur);

    Task<WorkflowResult> RenvoyerAuDemandeurDsiAsync(
        Guid id, string? commentaire, bool isDelegue, string nomActeur);

    Task<WorkflowResult> RenvoyerAuDmDsiAsync(
        Guid id, string commentaire, bool isDelegue, string nomActeur);

    // ── Documents / duplication ────────────────────────────────────────────────
    Task<WorkflowResult> AjouterDocumentsComplementairesAsync(
        Guid id, List<IFormFile>? documents, Guid currentUserId, bool canManageDemandes);

    Task<DuplicationResult> DupliquerDemandeAsync(Guid id, Guid currentUserId, bool canManageDemandes);

    // ── Soumission ──────────────────────────────────────────────────────────────
    Task<SoumissionResult> SoumettreAsync(Guid id, Guid currentUserId, bool hasAdminScope, bool ignorerDoublons);
}

/// <summary>
/// Résultat de la validation DSI : sur succès, crée un projet et porte son Id
/// pour permettre la redirection vers la fiche projet.
/// </summary>
public sealed record ValiderDsiResult(bool IsNotFound, string? ErrorMessage, string? SuccessMessage, Guid? ProjetId)
{
    public static ValiderDsiResult NotFound() => new(true, null, null, null);
    public static ValiderDsiResult Error(string message) => new(false, message, null, null);
    public static ValiderDsiResult Success(string message, Guid projetId) => new(false, null, message, projetId);
}

/// <summary>
/// Résultat de la duplication d'une demande : sur succès, porte l'Id de la
/// nouvelle demande créée (pour rediriger vers sa fiche).
/// </summary>
public sealed record DuplicationResult(bool IsNotFound, bool IsForbidden, string? ErrorMessage, string? SuccessMessage, Guid? NouvelleDemandeId)
{
    public static DuplicationResult NotFound() => new(true, false, null, null, null);
    public static DuplicationResult Forbidden() => new(false, true, null, null, null);
    public static DuplicationResult Error(string message) => new(false, false, message, null, null);
    public static DuplicationResult Success(string message, Guid nouvelleDemandeId) => new(false, false, null, message, nouvelleDemandeId);
}

/// <summary>
/// Résultat d'une soumission. Si <see cref="Doublons"/> est non nul, le controller
/// doit afficher la vue de vérification des doublons (rendu depuis un POST).
/// </summary>
public sealed record SoumissionResult(
    bool IsNotFound, bool IsForbidden, string? ErrorMessage, string? SuccessMessage,
    VerificationDoublonsViewModel? Doublons)
{
    public static SoumissionResult NotFound() => new(true, false, null, null, null);
    public static SoumissionResult Forbidden() => new(false, true, null, null, null);
    public static SoumissionResult Error(string message) => new(false, false, message, null, null);
    public static SoumissionResult DoublonsDetectes(VerificationDoublonsViewModel vm) => new(false, false, null, null, vm);
    public static SoumissionResult Success(string message) => new(false, false, null, message, null);
}
