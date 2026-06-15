using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Réalisation des bénéfices : définition des bénéfices attendus (cadrage) puis
    /// évaluation lors de la revue post-implémentation. L'autorisation reste au contrôleur.
    /// </summary>
    public interface IBeneficeProjetService
    {
        Task<List<BeneficeProjet>> ListerAsync(Guid projetId);

        Task<WorkflowResult> AjouterAsync(
            Guid projetId, Guid userId, string libelle, string indicateur,
            string valeurCible, DateTime? dateCibleRealisation);

        Task<WorkflowResult> EvaluerAsync(
            Guid beneficeId, Guid userId, StatutBenefice statut,
            string? valeurRealisee, string? commentaire);

        Task<WorkflowResult> SupprimerAsync(Guid beneficeId, Guid userId);
    }
}
