namespace GestionProjects.Application.Validators.Admin;

public record CreateParametreInput(string? Cle, string? Valeur, string? Description);
public record UpdateParametreInput(Guid Id, string? Cle, string? Valeur, string? Description);

public record ParametresWorkflowInput(
    string? DsiPrincipalId,
    string? DsiDelegueId,
    int? DelaiInactiviteSessionMinutes,
    string? RepertoireStockageRacine,
    string? TypesLivrables);

/// <summary>Projection JSON pour pré-remplir le formulaire d'édition d'un paramètre.</summary>
public record ParametreDetailsDto(Guid Id, string Cle, string Valeur, string Description);
