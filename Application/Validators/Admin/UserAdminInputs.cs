using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Validators.Admin;

public record CreateUserInput(
    string? Matricule,
    string? Nom,
    string? Prenoms,
    string? Email,
    string? DirectionId,
    string? MotDePasse,
    string? ConfirmMotDePasse,
    string? Roles,
    bool PeutCreerDemandeProjet);

public record UpdateUserInput(
    Guid Id,
    string? Matricule,
    string? Nom,
    string? Prenoms,
    string? Email,
    string? DirectionId,
    string? NouveauMotDePasse,
    string? ConfirmNouveauMotDePasse,
    string? Roles,
    bool PeutCreerDemandeProjet,
    ProfilRessource? ProfilRessource,
    decimal? CapaciteHebdomadaire);

/// <summary>Projection JSON renvoyée pour pré-remplir le formulaire d'édition utilisateur.</summary>
public record UserDetailsDto(
    Guid Id,
    string Matricule,
    string Nom,
    string Prenoms,
    string Email,
    string DirectionId,
    List<int> Roles,
    int Role,
    bool PeutCreerDemandeProjet,
    int? ProfilRessource,
    decimal CapaciteHebdomadaire);
