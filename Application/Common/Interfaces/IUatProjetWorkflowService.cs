using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Workflow de gestion UAT centré sur les cas, exécutions et campagnes de test.
/// Les contrôles d'autorisation restent dans le controller.
/// </summary>
public interface IUatProjetWorkflowService
{
    Task<WorkflowResult> AjouterCasTestAsync(
        Guid projetId,
        string titre,
        string? description,
        string? resultatAttendu,
        PrioriteAnomalie priorite,
        bool estObligatoire,
        Guid? campagneId);

    Task<WorkflowResult> ExecuterCasTestAsync(
        Guid projetId,
        Guid casTestId,
        StatutExecutionTest statut,
        string? commentaire,
        Guid? campagneId,
        Guid executeParId);

    Task<WorkflowResult> AjouterCampagneTestAsync(
        Guid projetId,
        string nom,
        string? descriptionCampagne,
        Environnement environnement,
        DateTime dateLancement);

    Task<WorkflowResult> SupprimerCasTestAsync(Guid projetId, Guid casTestId);
}
