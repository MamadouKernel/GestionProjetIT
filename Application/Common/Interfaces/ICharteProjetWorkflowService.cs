using GestionProjects.Application.Common.Results;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Workflow de validation de la charte projet. Le controller conserve le gating de permission ;
/// le service applique les règles métier, la persistance et l'audit.
/// </summary>
public interface ICharteProjetWorkflowService
{
    Task<WorkflowResult> ValiderDmAsync(Guid projetId, Guid userId);
    Task<WorkflowResult> RejeterDmAsync(Guid projetId, string commentaire);
    Task<WorkflowResult> ValiderDsiAsync(Guid projetId, Guid userId);
    Task<WorkflowResult> RejeterDsiAsync(Guid projetId, string commentaire);
}
