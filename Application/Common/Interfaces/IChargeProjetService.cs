using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Persistance des charges (saisie, soumission, validation). Le contrôleur garde
    /// l'autorisation et le formatage JSON ; ce service possède la logique de données + audit.
    /// Retourne l'entité ChargeProjet mise à jour (ou null si introuvable) pour que le
    /// contrôleur compose sa réponse.
    /// </summary>
    public interface IChargeProjetService
    {
        Task<ProjetChargesViewModel> BuildChargesViewModelAsync(
            Projet projet, Guid currentUserId, bool isPilotage, bool isProjectMember);

        Task<ChargeProjet> SaisirAsync(
            Guid projetId, Guid ressourceId, DateTime semaineDebut,
            decimal? chargePrevisionnelle, decimal? chargeReelle,
            string? commentaire, string? typeActivite, string? activite,
            Guid userId, bool canEditForecast, bool canEditActual);

        Task<ChargeProjet?> SoumettreAsync(Guid projetId, Guid ressourceId, DateTime semaineDebut);

        Task<ChargeProjet?> MettreAJourValidationAsync(
            Guid projetId, Guid ressourceId, DateTime semaineDebut,
            StatutValidationCharge statut, string? commentaireValidation, Guid userId);
    }
}
