using GestionProjects.Application.Common.Results;

namespace GestionProjects.Application.Common.Interfaces;

public interface IDemandeCreationCompteWorkflowService
{
    Task<WorkflowResult> SoumettreAsync(SoumettreDemandeCreationCompteInput input);
}

public sealed record SoumettreDemandeCreationCompteInput(
    string Nom,
    string Prenoms,
    string Email,
    Guid? DirectionId,
    string? ServiceLibelle,
    Guid? DirecteurMetierId);
