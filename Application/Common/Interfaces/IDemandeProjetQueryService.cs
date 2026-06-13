using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.DemandeProjet;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Lecture / requêtes du domaine DemandeProjet. Le controller fournit
/// l'identité et les drapeaux de permission déjà résolus ; le service
/// construit les requêtes, la pagination et les listes déroulantes.
/// </summary>
public interface IDemandeProjetQueryService
{
    Task<DemandeProjetIndexViewModel> GetIndexAsync(
        Guid userId, bool canManageDemandes,
        Guid? directionId, Guid? demandeurId, Guid? directeurMetierId,
        int page, int pageSize);

    Task<PagedResult<DemandeProjet>> GetListeValidationDMAsync(
        Guid userId, bool hasAdminScope, string? recherche, int page, int pageSize);

    Task<ValidationDsiListResult> GetListeValidationDSIAsync(
        string? recherche, Guid? directionId, int page, int pageSize);

    Task<PagedResult<DemandeProjet>> GetHistoriqueValidationsDSIAsync(
        string? recherche, int page, int pageSize);

    Task<DemandeProjet?> GetForDetailsAsync(Guid id);

    Task<List<Utilisateur>> GetChefsProjetDisponiblesAsync();

    Task<HistoriqueActionsDMViewModel> GetHistoriqueActionsDMAsync(
        Guid userId, bool hasAdminScope, int page, int pageSize);

    /// <summary>Détecte les demandes au titre similaire (≥ 70%). Retourne null si aucune.</summary>
    Task<VerificationDoublonsViewModel?> DetecterDoublonsAsync(DemandeProjet demande);
}

/// <summary>Liste de validation DSI paginée + liste des directions pour le filtre.</summary>
public sealed record ValidationDsiListResult(PagedResult<DemandeProjet> Paged, List<SelectListItem> Directions);
