using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.ViewModels.DemandesAcces;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class DemandeAccesDmQueryService : IDemandeAccesDmQueryService
{
    private readonly ApplicationDbContext _db;

    public DemandeAccesDmQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DemandesAccesIndexViewModel> GetIndexPourDmAsync(
        Guid dmUtilisateurId,
        bool isAdminIT,
        string? recherche,
        StatutDemandeAcces? statut,
        Guid? focusId,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        // 1. Quelles directions ce DM couvre ? (son rattachement + role DM actif)
        // AdminIT n'a pas besoin du role DM : acces total, toutes directions confondues.
        List<Guid> directionsCouvertes;
        if (isAdminIT)
        {
            directionsCouvertes = await _db.Directions
                .Where(d => !d.EstSupprime)
                .Select(d => d.Id)
                .ToListAsync();
        }
        else
        {
            directionsCouvertes = await _db.Utilisateurs
                .Where(u => u.Id == dmUtilisateurId &&
                            !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .Select(u => u.DirectionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToListAsync();
        }

        var query = _db.DemandesAccesAzureAd
            .Include(d => d.DirectionDetectee)
            .Include(d => d.TraitePar)
            .Include(d => d.UtilisateurCree)
            .Include(d => d.ValideeParDm)
            .IgnoreQueryFilters()
            .Where(d => !d.EstSupprime &&
                        d.DirectionDetecteeId.HasValue &&
                        directionsCouvertes.Contains(d.DirectionDetecteeId.Value))
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
            .Where(d => !d.EstSupprime && d.EstActive && directionsCouvertes.Contains(d.Id))
            .OrderBy(d => d.Libelle)
            .Select(d => new SelectOption(d.Id.ToString(), d.Libelle, false, false))
            .ToListAsync();

        return new DemandesAccesIndexViewModel
        {
            Items = paged.Items,
            Traces = new(), // pas utilise sur cet ecran
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
