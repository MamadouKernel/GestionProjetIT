using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
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
        public async Task<ProjetHistoriqueDMViewModel> BuildHistoriqueDMAsync(
            string? recherche,
            Guid? directionId,
            PhaseProjet? phase,
            StatutProjet? statut,
            int page,
            int pageSize,
            Guid userId,
            bool canPortfolioAccess,
            Guid? currentUserDirectionId)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.HistoriquePhases)
                    .ThenInclude(h => h.ModifieParUtilisateur);

            if (!canPortfolioAccess)
            {
                query = currentUserDirectionId.HasValue
                    ? query.Where(p => p.SponsorId == userId || p.DirectionId == currentUserDirectionId.Value)
                    : query.Where(p => p.SponsorId == userId);
            }

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(p => p.Titre.Contains(recherche) || p.CodeProjet.Contains(recherche));

            if (directionId.HasValue)
                query = query.Where(p => p.DirectionId == directionId.Value);

            if (phase.HasValue)
                query = query.Where(p => p.PhaseActuelle == phase.Value);

            if (statut.HasValue)
                query = query.Where(p => p.StatutProjet == statut.Value);

            var pagedProjets = await query.OrderByDescending(p => p.DateCreation).ToPagedResultAsync(page, pageSize);
            var projets = pagedProjets.Items;

            // Optimisation : charger les données uniquement pour la page courante
            var projetIds = projets.Select(p => p.Id).ToList();

            // Charger tous les logs d'audit en une seule requête
            var allAuditLogsList = await _db.AuditLogs
                .Include(a => a.Utilisateur)
                .Where(a => a.Entite == "Projet" && projetIds.Any(id => a.EntiteId == id.ToString()))
                .ToListAsync();

            var allAuditLogs = allAuditLogsList
                .Where(a => Guid.TryParse(a.EntiteId, out var id) && projetIds.Contains(id))
                .GroupBy(a => Guid.Parse(a.EntiteId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.DateAction).ToList());

            // Charger toutes les statistiques en une seule requête par type
            var livrablesCounts = await _db.LivrablesProjets
                .Where(l => projetIds.Contains(l.ProjetId))
                .GroupBy(l => l.ProjetId)
                .Select(g => new { ProjetId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjetId, x => x.Count);

            var anomaliesCounts = await _db.AnomaliesProjets
                .Where(a => projetIds.Contains(a.ProjetId))
                .GroupBy(a => a.ProjetId)
                .Select(g => new
                {
                    ProjetId = g.Key,
                    Total = g.Count(),
                    Ouvertes = g.Count(a => a.Statut == Domain.Enums.StatutAnomalie.Ouverte)
                })
                .ToDictionaryAsync(x => x.ProjetId, x => new { x.Total, x.Ouvertes });

            var risquesCounts = await _db.RisquesProjets
                .Where(r => projetIds.Contains(r.ProjetId))
                .GroupBy(r => r.ProjetId)
                .Select(g => new
                {
                    ProjetId = g.Key,
                    Total = g.Count(),
                    Critiques = g.Count(r => r.Impact == Domain.Enums.ImpactRisque.Critique)
                })
                .ToDictionaryAsync(x => x.ProjetId, x => new { x.Total, x.Critiques });

            // Construire le ViewModel
            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            var vmHistorique = new ProjetHistoriqueDMViewModel
            {
                Projets          = projets,
                Directions       = directions,
                Recherche        = recherche,
                SelectedDirectionId = directionId,
                SelectedPhase    = phase,
                SelectedStatut   = statut,
                PageNumber       = pagedProjets.PageNumber,
                TotalPages       = pagedProjets.TotalPages,
                TotalCount       = pagedProjets.TotalCount,
                PageSize         = pagedProjets.PageSize,
            };

            foreach (var projet in projets)
            {
                var auditLogs = allAuditLogs.TryGetValue(projet.Id, out var logs) ? logs : new List<Domain.Models.AuditLog>();
                var totalLivrables = livrablesCounts.TryGetValue(projet.Id, out var livCount) ? livCount : 0;
                var totalAnomalies = anomaliesCounts.TryGetValue(projet.Id, out var anomCount) ? anomCount.Total : 0;
                var anomaliesOuvertes = anomaliesCounts.TryGetValue(projet.Id, out var anomCount2) ? anomCount2.Ouvertes : 0;
                var totalRisques = risquesCounts.TryGetValue(projet.Id, out var riskCount) ? riskCount.Total : 0;
                var risquesCritiques = risquesCounts.TryGetValue(projet.Id, out var riskCount2) ? riskCount2.Critiques : 0;

                vmHistorique.ProjetsAvecHistorique.Add(new ProjetHistoriqueItem
                {
                    Projet            = projet,
                    AuditLogs         = auditLogs,
                    TotalLivrables    = totalLivrables,
                    TotalAnomalies    = totalAnomalies,
                    AnomaliesOuvertes = anomaliesOuvertes,
                    TotalRisques      = totalRisques,
                    RisquesCritiques  = risquesCritiques
                });
            }

            return vmHistorique;
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
                    .Select(d => new SelectOption(d.Id.ToString(), d.Libelle, false, false))
                    .ToListAsync(),
                TimeSpan.FromMinutes(30));

            // Chefs de projet depuis le cache (15 min)
            var chefsCached = await _cache.GetOrSetAsync(
                "chefs_projet",
                () => _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
                    .Select(u => new SelectOption(u.Id.ToString(), $"{u.Nom} {u.Prenoms}", false, false))
                    .ToListAsync(),
                TimeSpan.FromMinutes(15));

            return new ProjetFiltresViewModel
            {
                Directions = (directionsCached ?? new())
                    .Select(d => d with { Selected = directionId.HasValue && d.Value == directionId.Value.ToString() }).ToList(),

                ChefsProjet = (chefsCached ?? new())
                    .Select(c => c with { Selected = chefProjetId.HasValue && c.Value == chefProjetId.Value.ToString() }).ToList(),

                Phases = Enum.GetValues(typeof(PhaseProjet))
                    .Cast<PhaseProjet>()
                    .Select(p => new SelectOption(((int)p).ToString(), p.ToString(), phase == p)).ToList(),

                Statuts = Enum.GetValues(typeof(StatutProjet))
                    .Cast<StatutProjet>()
                    .Select(s => new SelectOption(((int)s).ToString(), s.ToString(), statut == s)).ToList(),

                Etats = Enum.GetValues(typeof(EtatProjet))
                    .Cast<EtatProjet>()
                    .Select(e => new SelectOption(((int)e).ToString(), e.ToString(), etat == e)).ToList()
            };
        }
    }
}
