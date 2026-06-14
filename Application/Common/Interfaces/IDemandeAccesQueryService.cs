using GestionProjects.Application.ViewModels.DemandesAcces;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

public interface IDemandeAccesQueryService
{
    Task<DemandesAccesIndexViewModel> GetIndexAsync(
        string? recherche,
        StatutDemandeAcces? statut,
        Guid? focusId,
        int page,
        int pageSize);
}
