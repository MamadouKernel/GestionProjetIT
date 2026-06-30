using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Évaluation des membres de l'équipe projet (Chef de Projet/DSI uniquement),
    /// typiquement renseignée à la clôture. Une seule évaluation par membre par
    /// projet : EnregistrerAsync fait un upsert. L'autorisation reste au contrôleur.
    /// </summary>
    public interface IEvaluationMembreProjetService
    {
        Task<List<EvaluationMembreProjet>> ListerAsync(Guid projetId);

        Task<WorkflowResult> EnregistrerAsync(
            Guid projetId, Guid membreProjetId, Guid evaluateurId,
            int noteQualite, int noteRespectDelais, int noteCollaboration, string? commentaire);

        Task<WorkflowResult> SupprimerAsync(Guid evaluationId, Guid userId);
    }
}
