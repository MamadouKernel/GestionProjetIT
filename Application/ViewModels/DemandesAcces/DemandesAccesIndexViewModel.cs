using GestionProjects.Application.Common.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.DemandesAcces
{
    public class DemandesAccesIndexViewModel
    {
        public List<DemandeAccesAzureAd> Items { get; set; } = new();
        /// <summary>
        /// Trace consolidee par demande (qui a approuve, qui a active, qui a desactive).
        /// Indexee par Id de la demande pour decouplage avec EF.
        /// </summary>
        public Dictionary<Guid, DemandeAccesTraceDto> Traces { get; set; } = new();
        public IEnumerable<SelectOption> Directions { get; set; } = Enumerable.Empty<SelectOption>();
        public string? Recherche { get; set; }
        public StatutDemandeAcces? SelectedStatut { get; set; }
        public Guid? FocusId { get; set; }
        public int TotalCount { get; set; }

        // Pagination
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>
    /// Petite projection regroupant les acteurs et dates de la vie d'un compte
    /// issu d'une demande d'acces : approbation, activation utilisateur, desactivation.
    /// </summary>
    public sealed record DemandeAccesTraceDto(
        // Activation par l'utilisateur lui-même (clic sur le lien + mot de passe defini)
        DateTime? DateActivation,
        string? ActiveDepuisIp,
        // Desactivation du compte (soft-delete par un admin)
        bool CompteDesactive,
        string? DesactiveParMatricule,
        DateTime? DateDesactivation);
}
