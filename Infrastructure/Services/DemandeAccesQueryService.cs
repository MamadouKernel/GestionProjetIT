using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.ViewModels.DemandesAcces;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class DemandeAccesQueryService : IDemandeAccesQueryService
{
    private readonly ApplicationDbContext _db;

    public DemandeAccesQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DemandesAccesIndexViewModel> GetIndexAsync(
        string? recherche,
        StatutDemandeAcces? statut,
        Guid? focusId,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.DemandesAccesAzureAd
            .Include(d => d.DirectionDetectee)
            .Include(d => d.TraitePar)
            .AsQueryable();

        if (focusId.HasValue)
        {
            query = query.Where(d => d.Id == focusId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(recherche))
        {
            query = query.Where(d =>
                d.Nom.Contains(recherche) ||
                d.Prenoms.Contains(recherche) ||
                d.Email.Contains(recherche) ||
                d.Matricule.Contains(recherche));
        }

        if (!focusId.HasValue && statut.HasValue)
        {
            query = query.Where(d => d.Statut == statut.Value);
        }

        var paged = await query
            .OrderBy(d => d.Statut)
            .ThenByDescending(d => d.DateCreation)
            .ToPagedResultAsync(page, pageSize);

        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .OrderBy(d => d.Libelle)
            .Select(d => new SelectOption(d.Id.ToString(), d.Libelle, false, false))
            .ToListAsync();

        return new DemandesAccesIndexViewModel
        {
            Items = paged.Items,
            Directions = directions,
            Recherche = recherche,
            SelectedStatut = statut,
            FocusId = focusId,
            TotalCount = paged.TotalCount,
            PageNumber = paged.PageNumber,
            TotalPages = paged.TotalPages,
            PageSize = paged.PageSize
        };
    }
}
