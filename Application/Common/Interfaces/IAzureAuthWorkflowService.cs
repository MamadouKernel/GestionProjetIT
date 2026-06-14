namespace GestionProjects.Application.Common.Interfaces;

public interface IAzureAuthWorkflowService
{
    Task<AzureDirectionDetection> DetectDirectionAsync(string? azureDepartment);
    Task<bool> HasPendingAccessRequestAsync(string email, string matricule);
    Task RecordSuccessfulLoginAsync(Guid utilisateurId);
    Task<DemandeAccesWorkflowResult> SoumettreDemandeAzureAsync(SoumettreDemandeAccesAzureInput input);
}

public sealed record AzureDirectionDetection(Guid? DirectionId, string? DirectionLibelle);

public sealed record SoumettreDemandeAccesAzureInput(
    string Email,
    string Nom,
    string Prenom,
    string Matricule,
    string Justification,
    string? AzureDepartment,
    string? DirectionDetecteeId);
