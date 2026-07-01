namespace GestionProjects.Application.Validators.Admin;

public record CreateDelegationDsiInput(
    string? DSIId, string? DelegueId, DateTime DateDebut, DateTime DateFin,
    bool EstActive, Guid CurrentUserId, bool HasFullScope);

public record UpdateDelegationDsiInput(
    Guid Id, string? DSIId, string? DelegueId, DateTime DateDebut, DateTime DateFin,
    bool EstActive, Guid CurrentUserId, bool HasFullScope);

public record CreateDelegationChefInput(
    string? ProjetId, string? DelegantId, string? DelegueId, DateTime DateDebut,
    bool EstActive, Guid CurrentUserId, bool HasFullScope);

public record UpdateDelegationChefInput(
    Guid Id, string? ProjetId, string? DelegantId, string? DelegueId, DateTime DateDebut,
    bool EstActive, Guid CurrentUserId, bool HasFullScope);

public record CreateDelegationDmInput(
    string? DirecteurMetierId, string? DelegueId, DateTime DateDebut, DateTime DateFin,
    bool EstActive, Guid CurrentUserId, bool HasFullScope);

public record UpdateDelegationDmInput(
    Guid Id, string? DirecteurMetierId, string? DelegueId, DateTime DateDebut, DateTime DateFin,
    bool EstActive, Guid CurrentUserId, bool HasFullScope);

/// <summary>Projection JSON d'une délégation DSI (pré-remplissage formulaire).</summary>
public record DelegationDsiDetailsDto(
    Guid id, string dsiId, string delegueId, string dateDebut, string dateFin, bool estActive);

/// <summary>Projection JSON d'une délégation chef de projet (pré-remplissage formulaire).</summary>
public record DelegationChefDetailsDto(
    Guid id, string projetId, string delegantId, string delegueId, string dateDebut, string dateFin, bool estActive);

/// <summary>Projection JSON d'une délégation Directeur Métier (pré-remplissage formulaire).</summary>
public record DelegationDmDetailsDto(
    Guid id, string directeurMetierId, string delegueId, string dateDebut, string dateFin, bool estActive);
