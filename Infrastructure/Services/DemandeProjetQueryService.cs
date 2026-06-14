using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.ViewModels.DemandeProjet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class DemandeProjetQueryService : IDemandeProjetQueryService
{
    private readonly ApplicationDbContext _db;

    public DemandeProjetQueryService(ApplicationDbContext db) => _db = db;

    public async Task<DemandeProjetIndexViewModel> GetIndexAsync(
        Guid userId, bool canManageDemandes,
        Guid? directionId, Guid? demandeurId, Guid? directeurMetierId,
        int page, int pageSize)
    {
        IQueryable<DemandeProjet> query = _db.DemandesProjets
            .Include(d => d.Direction)
            .Include(d => d.Demandeur)
            .Include(d => d.DirecteurMetier);

        if (!canManageDemandes)
        {
            query = query.Where(d => d.DemandeurId == userId);
        }
        else if (!directionId.HasValue && !demandeurId.HasValue && !directeurMetierId.HasValue)
        {
            query = query.Where(d => d.DemandeurId == userId);
        }
        else
        {
            if (directionId.HasValue)        query = query.Where(d => d.DirectionId == directionId.Value);
            if (demandeurId.HasValue)        query = query.Where(d => d.DemandeurId == demandeurId.Value);
            if (directeurMetierId.HasValue)  query = query.Where(d => d.DirecteurMetierId == directeurMetierId.Value);
        }

        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var paged = await query.OrderByDescending(d => d.DateSoumission).ToPagedResultAsync(page, pageSize);

        var vm = new DemandeProjetIndexViewModel
        {
            Demandes   = paged.Items,
            PageNumber = paged.PageNumber,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize   = paged.PageSize
        };

        if (canManageDemandes)
        {
            vm.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .Select(d => new SelectOption(d.Id.ToString(), d.Libelle, directionId == d.Id, false))
                .ToListAsync();

            vm.Demandeurs = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
                .Select(u => new SelectOption(u.Id.ToString(), $"{u.Nom} {u.Prenoms}", demandeurId == u.Id, false))
                .ToListAsync();

            vm.DirecteursMetier = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
                .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
                .Select(u => new SelectOption(u.Id.ToString(), $"{u.Nom} {u.Prenoms}", directeurMetierId == u.Id, false))
                .ToListAsync();
        }

        return vm;
    }

    public async Task<PagedResult<DemandeProjet>> GetListeValidationDMAsync(
        Guid userId, bool hasAdminScope, string? recherche, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = _db.DemandesProjets
            .Include(d => d.Direction)
            .Include(d => d.Demandeur)
            .Include(d => d.DirecteurMetier)
            .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                        d.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier ||
                        d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI);

        if (!hasAdminScope)
            query = query.Where(d => d.DirecteurMetierId == userId);

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(d => (d.Titre != null && d.Titre.Contains(recherche)) ||
                                     d.Demandeur.Nom.Contains(recherche) ||
                                     d.Demandeur.Prenoms.Contains(recherche));

        return await query.OrderByDescending(d => d.DateSoumission).ToPagedResultAsync(page, pageSize);
    }

    public async Task<ValidationDsiListResult> GetListeValidationDSIAsync(
        string? recherche, Guid? directionId, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = _db.DemandesProjets
            .Include(d => d.Direction)
            .Include(d => d.Demandeur)
            .Include(d => d.DirecteurMetier)
            .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDSI);

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(d => (d.Titre != null && d.Titre.Contains(recherche)) ||
                                     d.Demandeur.Nom.Contains(recherche) ||
                                     d.Demandeur.Prenoms.Contains(recherche));

        if (directionId.HasValue)
            query = query.Where(d => d.DirectionId == directionId.Value);

        var paged = await query.OrderByDescending(d => d.DateSoumission).ToPagedResultAsync(page, pageSize);

        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .OrderBy(d => d.Libelle)
            .Select(d => new SelectOption(d.Id.ToString(), d.Libelle, directionId == d.Id, false))
            .ToListAsync();

        return new ValidationDsiListResult(paged, directions);
    }

    public async Task<PagedResult<DemandeProjet>> GetHistoriqueValidationsDSIAsync(
        string? recherche, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = _db.DemandesProjets
            .Include(d => d.Direction)
            .Include(d => d.Demandeur)
            .Include(d => d.DirecteurMetier)
            .Where(d => d.StatutDemande == StatutDemande.ValideeParDSI ||
                        d.StatutDemande == StatutDemande.RejeteeParDSI ||
                        d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI ||
                        d.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI);

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(d => (d.Titre != null && d.Titre.Contains(recherche)) ||
                                     d.Demandeur.Nom.Contains(recherche) ||
                                     d.Demandeur.Prenoms.Contains(recherche));

        return await query.OrderByDescending(d => d.DateValidationDSI ?? d.DateSoumission).ToPagedResultAsync(page, pageSize);
    }

    public async Task<DemandeProjet?> GetForDetailsAsync(Guid id)
    {
        return await _db.DemandesProjets
            .Include(d => d.Direction)
            .Include(d => d.Demandeur)
            .Include(d => d.DirecteurMetier)
            .Include(d => d.Annexes)
            .Include(d => d.Projet)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<Utilisateur>> GetChefsProjetDisponiblesAsync()
    {
        var chefsProjet = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
            .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
            .ToListAsync();

        var delegationsActives = await _db.DelegationsChefProjet
            .Include(d => d.Delegue)
            .Where(d => !d.EstSupprime && d.EstActive && d.DateDebut <= DateTime.Now && d.DateFin == null)
            .Select(d => d.Delegue!)
            .Where(u => !u.EstSupprime)
            .ToListAsync();

        return chefsProjet
            .Union(delegationsActives)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
            .ToList();
    }

    public async Task<HistoriqueActionsDMViewModel> GetHistoriqueActionsDMAsync(
        Guid userId, bool hasAdminScope, int page, int pageSize)
    {
        var actionsDM = new[] { "VALIDATION_DM", "REJET_DM", "CORRECTION_DM" };

        var logsQuery = _db.AuditLogs
            .Include(a => a.Utilisateur)
            .Where(a => actionsDM.Contains(a.TypeAction) && a.Entite == "DemandeProjet" && !a.EstSupprime);

        if (!hasAdminScope)
            logsQuery = logsQuery.Where(a => a.UtilisateurId == userId);

        var total = await logsQuery.CountAsync();
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 50);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var logs = await logsQuery
            .OrderByDescending(a => a.DateAction)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var demandeIds = logs
            .Where(l => Guid.TryParse(l.EntiteId, out _))
            .Select(l => Guid.Parse(l.EntiteId))
            .Distinct()
            .ToList();

        var demandes = await _db.DemandesProjets
            .Include(d => d.Demandeur)
            .Include(d => d.Direction)
            .Where(d => demandeIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);

        return new HistoriqueActionsDMViewModel
        {
            Logs       = logs,
            Demandes   = demandes,
            Page       = page,
            TotalPages = totalPages,
            Total      = total
        };
    }

    public async Task<VerificationDoublonsViewModel?> DetecterDoublonsAsync(DemandeProjet demande)
    {
        if (string.IsNullOrWhiteSpace(demande.Titre))
            return null;

        var titreNormalise = NormaliserTexte(demande.Titre);

        var existantes = await _db.DemandesProjets
            .Include(d => d.Demandeur)
            .Include(d => d.Direction)
            .Where(d => d.Id != demande.Id && !string.IsNullOrWhiteSpace(d.Titre))
            .ToListAsync();

        var similaires = new List<DemandeSimilaireDto>();
        foreach (var existante in existantes)
        {
            var similarite = CalculerSimilarite(titreNormalise, NormaliserTexte(existante.Titre ?? string.Empty));
            if (similarite < 0.7) continue;

            var projet = await _db.Projets
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.DemandeProjetId == existante.Id);

            similaires.Add(new DemandeSimilaireDto
            {
                DemandeId        = existante.Id,
                Titre            = existante.Titre ?? string.Empty,
                StatutDemande    = existante.StatutDemande,
                DateSoumission   = existante.DateSoumission,
                Demandeur        = $"{existante.Demandeur?.Nom} {existante.Demandeur?.Prenoms}",
                Direction        = existante.Direction?.Libelle ?? "N/A",
                CommentaireRejet = GetCommentaireRejet(existante),
                Similarite       = similarite,
                ProjetExistant   = projet != null
                    ? new ProjetExistantDto
                      {
                          ProjetId      = projet.Id,
                          CodeProjet    = projet.CodeProjet,
                          Titre         = projet.Titre,
                          StatutProjet  = projet.StatutProjet,
                          PhaseActuelle = projet.PhaseActuelle,
                          ChefProjet    = projet.ChefProjet != null
                              ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}"
                              : "Non assigné"
                      }
                    : null
            });
        }

        if (similaires.Count == 0)
            return null;

        return new VerificationDoublonsViewModel
        {
            DemandeCourante    = demande,
            DemandesSimilaires = similaires.OrderByDescending(s => s.Similarite).ToList()
        };
    }

    private static string NormaliserTexte(string texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return string.Empty;
        return texte.ToLowerInvariant().Trim().Replace(" ", "").Replace("-", "").Replace("_", "");
    }

    private static double CalculerSimilarite(string texte1, string texte2)
    {
        if (string.IsNullOrEmpty(texte1) || string.IsNullOrEmpty(texte2)) return 0.0;

        var longueurMax = Math.Max(texte1.Length, texte2.Length);
        var minLength   = Math.Min(texte1.Length, texte2.Length);
        if (minLength == 0) return 0.0;

        var caracteresCommuns = 0;
        for (int i = 0; i < minLength; i++)
            if (texte1[i] == texte2[i]) caracteresCommuns++;

        if (texte1.Contains(texte2) || texte2.Contains(texte1)) return 0.9;

        return (double)caracteresCommuns / longueurMax;
    }

    private static string? GetCommentaireRejet(DemandeProjet demande) => demande.StatutDemande switch
    {
        StatutDemande.RejeteeParDirecteurMetier             => demande.CommentaireDirecteurMetier,
        StatutDemande.RejeteeParDSI                         => demande.CommentaireDSI,
        StatutDemande.CorrectionDemandeeParDirecteurMetier  => demande.CommentaireDirecteurMetier,
        StatutDemande.RetourneeAuDemandeurParDSI            => demande.CommentaireDSI,
        _                                                   => null
    };
}
