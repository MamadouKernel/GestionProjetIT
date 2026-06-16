using GestionProjects.Application.ViewModels.DemandesAcces;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Query dédiée a l'espace Directeur Métier : ne renvoie QUE les demandes
/// rattachees aux directions ou ce DM est rattache et a le role DM actif.
/// </summary>
public interface IDemandeAccesDmQueryService
{
    Task<DemandesAccesIndexViewModel> GetIndexPourDmAsync(
        Guid dmUtilisateurId,
        string? recherche,
        StatutDemandeAcces? statut,
        Guid? focusId,
        int page,
        int pageSize);
}
