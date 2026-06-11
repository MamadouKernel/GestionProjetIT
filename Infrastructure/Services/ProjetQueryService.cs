using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Implémentation du service de requêtes projet.
    /// Centralise le filtrage par scope et le chargement des listes de référence.
    /// </summary>
    public class ProjetQueryService : IProjetQueryService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICacheService _cache;

        public ProjetQueryService(ApplicationDbContext db, ICacheService cache)
        {
            _db = db;
            _cache = cache;
        }

        /// <inheritdoc/>
        public async Task<Guid?> GetUserDirectionIdAsync(Guid userId)
        {
            return await _db.Utilisateurs
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.EstSupprime)
                .Select(u => u.DirectionId)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public Task<IQueryable<Projet>> AppliquerScopeAsync(
            IQueryable<Projet> query,
            Guid userId,
            bool canPortfolioAccess,
            bool hasChefProjetScope,
            bool hasDmScope,
            bool hasDemandeurScope,
            Guid? currentUserDirectionId)
        {
            // Accès portefeuille complet : DSI / AdminIT — pas de filtre
            if (canPortfolioAccess)
                return Task.FromResult(query);

            if (hasChefProjetScope && hasDmScope)
            {
                query = currentUserDirectionId.HasValue
                    ? query.Where(p => p.ChefProjetId == userId || p.SponsorId == userId || p.DirectionId == currentUserDirectionId.Value)
                    : query.Where(p => p.ChefProjetId == userId || p.SponsorId == userId);
            }
            else if (hasChefProjetScope)
            {
                query = query.Where(p => p.ChefProjetId == userId);
            }
            else if (hasDmScope)
            {
                query = currentUserDirectionId.HasValue
                    ? query.Where(p => p.SponsorId == userId || p.DirectionId == currentUserDirectionId.Value)
                    : query.Where(p => p.SponsorId == userId);
            }
            else if (hasDemandeurScope)
            {
                query = query.Where(p => p.DemandeProjet != null && p.DemandeProjet.DemandeurId == userId);
            }
            else
            {
                // Aucun scope : rien n'est visible
                query = query.Where(_ => false);
            }

            return Task.FromResult(query);
        }

        /// <inheritdoc/>
        public async Task<ProjetFiltresViewModel> ChargerFiltresAsync(
            Guid? directionId,
            Guid? chefProjetId,
            PhaseProjet? phase,
            StatutProjet? statut,
            EtatProjet? etat)
        {
            // Directions depuis le cache (30 min)
            var directionsCached = await _cache.GetOrSetAsync(
                "directions_active",
                () => _db.Directions
                    .Where(d => !d.EstSupprime && d.EstActive)
                    .OrderBy(d => d.Libelle)
                    .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Libelle })
                    .ToListAsync(),
                TimeSpan.FromMinutes(30));

            // Chefs de projet depuis le cache (15 min)
            var chefsCached = await _cache.GetOrSetAsync(
                "chefs_projet",
                () => _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = $"{u.Nom} {u.Prenoms}" })
                    .ToListAsync(),
                TimeSpan.FromMinutes(15));

            return new ProjetFiltresViewModel
            {
                Directions = (directionsCached ?? new())
                    .Select(d => new SelectListItem
                    {
                        Value = d.Value,
                        Text = d.Text,
                        Selected = directionId.HasValue && d.Value == directionId.Value.ToString()
                    }).ToList(),

                ChefsProjet = (chefsCached ?? new())
                    .Select(c => new SelectListItem
                    {
                        Value = c.Value,
                        Text = c.Text,
                        Selected = chefProjetId.HasValue && c.Value == chefProjetId.Value.ToString()
                    }).ToList(),

                Phases = Enum.GetValues(typeof(PhaseProjet))
                    .Cast<PhaseProjet>()
                    .Select(p => new SelectListItem
                    {
                        Value = ((int)p).ToString(),
                        Text = p.ToString(),
                        Selected = phase == p
                    }).ToList(),

                Statuts = Enum.GetValues(typeof(StatutProjet))
                    .Cast<StatutProjet>()
                    .Select(s => new SelectListItem
                    {
                        Value = ((int)s).ToString(),
                        Text = s.ToString(),
                        Selected = statut == s
                    }).ToList(),

                Etats = Enum.GetValues(typeof(EtatProjet))
                    .Cast<EtatProjet>()
                    .Select(e => new SelectListItem
                    {
                        Value = ((int)e).ToString(),
                        Text = e.ToString(),
                        Selected = etat == e
                    }).ToList()
            };
        }
    }
}
