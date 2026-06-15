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
            // IgnoreQueryFilters sur UtilisateurCree : on veut le voir MÊME s'il a été
            // soft-deleted (c'est precisement ce qu'on cherche a afficher). Le filtre
            // soft-delete sur la demande elle-meme est ensuite reapplique explicitement.
            .Include(d => d.UtilisateurCree)
            .IgnoreQueryFilters()
            .Where(d => !d.EstSupprime)
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

        // Trace consolidee par demande : derniere activation (jeton utilise) du compte cree.
        // Batch en une requete pour eviter le N+1.
        var userIds = paged.Items
            .Where(d => d.UtilisateurCreeId.HasValue)
            .Select(d => d.UtilisateurCreeId!.Value)
            .Distinct()
            .ToList();

        var dernieresActivations = userIds.Count == 0
            ? new Dictionary<Guid, (DateTime Date, string? Ip)>()
            : await _db.JetonsInitialisationMotDePasse
                .IgnoreQueryFilters()
                .Where(j => userIds.Contains(j.UtilisateurId) && j.DateUtilisation != null)
                .GroupBy(j => j.UtilisateurId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Date = g.Max(j => j.DateUtilisation!.Value),
                    Ip = g.OrderByDescending(j => j.DateUtilisation).First().UtiliseDepuisIp
                })
                .ToDictionaryAsync(x => x.UserId, x => (x.Date, x.Ip));

        var traces = new Dictionary<Guid, DemandeAccesTraceDto>();
        foreach (var d in paged.Items)
        {
            var u = d.UtilisateurCree;
            DateTime? dateActivation = null;
            string? ipActivation = null;
            if (u != null && dernieresActivations.TryGetValue(u.Id, out var act))
            {
                dateActivation = act.Date;
                ipActivation = act.Ip;
            }

            var desactive = u?.EstSupprime == true;
            traces[d.Id] = new DemandeAccesTraceDto(
                DateActivation: dateActivation,
                ActiveDepuisIp: ipActivation,
                CompteDesactive: desactive,
                DesactiveParMatricule: desactive ? u?.ModifiePar : null,
                DateDesactivation: desactive ? u?.DateModification : null);
        }

        return new DemandesAccesIndexViewModel
        {
            Items = paged.Items,
            Traces = traces,
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
