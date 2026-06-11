using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    public interface IElectronicSignatureService
    {
        Task<DossierSignatureProjet> InitialiserCharteAsync(Guid projetId, FournisseurSignatureElectronique fournisseur, string? currentUserMatricule);
        Task<SignatureOperationResult> EnvoyerDossierAsync(Guid dossierId, string? currentUserMatricule);
        Task<SignatureOperationResult> EnregistrerDecisionSignataireAsync(Guid dossierId, Guid signataireId, bool approuver, string? currentUserMatricule);
        Task<SignatureOperationResult> FinaliserDossierAsync(Guid dossierId, string nomDocumentSigne, string cheminDocumentSigne, string? currentUserMatricule);
    }

    public class SignatureOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DossierSignatureProjet? Dossier { get; set; }
    }
}
