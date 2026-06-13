using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class DelegationAdminService : IDelegationAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public DelegationAdminService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<DelegationsPageViewModel> GetPageAsync(
        Guid currentUserId, bool hasFullScope, string? tab,
        string? rechercheDsi, string? rechercheChef,
        int pageDsi, int pageChef, int pageSize)
    {
        pageDsi  = Math.Max(1, pageDsi);
        pageChef = Math.Max(1, pageChef);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var dsiQuery = _db.DelegationsValidationDSI
            .Include(d => d.DSI).Include(d => d.Delegue)
            .Where(d => !d.EstSupprime).AsQueryable();

        if (!hasFullScope)
            dsiQuery = dsiQuery.Where(d => d.DSIId == currentUserId || d.DelegueId == currentUserId);

        if (!string.IsNullOrWhiteSpace(rechercheDsi))
            dsiQuery = dsiQuery.Where(d =>
                (d.DSI != null && (d.DSI.Nom.Contains(rechercheDsi) || d.DSI.Prenoms.Contains(rechercheDsi))) ||
                (d.Delegue != null && (d.Delegue.Nom.Contains(rechercheDsi) || d.Delegue.Prenoms.Contains(rechercheDsi))));

        var pagedDsi = await dsiQuery.OrderByDescending(d => d.DateDebut).ToPagedResultAsync(pageDsi, pageSize);

        var dsis = await _db.Utilisateurs
            .Where(u => !u.EstSupprime)
            .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
            .ToListAsync();

        var deleguesDsi = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ResponsableSolutionsIT))
            .OrderBy(u => u.Nom)
            .ToListAsync();

        var chefQuery = _db.DelegationsChefProjet
            .Include(d => d.Delegant).Include(d => d.Delegue)
            .Where(d => !d.EstSupprime).AsQueryable();

        if (!hasFullScope)
            chefQuery = chefQuery.Where(d => d.DelegantId == currentUserId || d.DelegueId == currentUserId);

        if (!string.IsNullOrWhiteSpace(rechercheChef))
            chefQuery = chefQuery.Where(d =>
                (d.Delegant != null && (d.Delegant.Nom.Contains(rechercheChef) || d.Delegant.Prenoms.Contains(rechercheChef))) ||
                (d.Delegue  != null && (d.Delegue.Nom.Contains(rechercheChef)  || d.Delegue.Prenoms.Contains(rechercheChef))));

        var pagedChef = await chefQuery.Include(d => d.Projet).OrderByDescending(d => d.DateDebut).ToPagedResultAsync(pageChef, pageSize);

        var projets = await _db.Projets
            .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
            .OrderByDescending(p => p.DateCreation)
            .ToListAsync();

        var delegants = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                       (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)) &&
                       (hasFullScope || u.Id == currentUserId))
            .OrderBy(u => u.Nom)
            .ToListAsync();

        var deleguesChefProjet = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
            .OrderBy(u => u.Nom)
            .ToListAsync();

        return new DelegationsPageViewModel
        {
            DelegationsDSI        = pagedDsi.Items,
            DelegationsChefProjet = pagedChef.Items,
            DSIs                  = dsis,
            DeleguesDSI           = deleguesDsi,
            Delegants             = delegants,
            DeleguesChefProjet    = deleguesChefProjet,
            Projets               = projets,
            ActiveTab             = tab ?? "dsi",
            CanAdminDelegations   = hasFullScope,
            CurrentUserId         = currentUserId,
            PageNumberDsi         = pagedDsi.PageNumber,
            TotalPagesDsi         = pagedDsi.TotalPages,
            TotalCountDsi         = pagedDsi.TotalCount,
            PageSizeDsi           = pagedDsi.PageSize,
            RechercheDsi          = rechercheDsi,
            PageNumberChef        = pagedChef.PageNumber,
            TotalPagesChef        = pagedChef.TotalPages,
            TotalCountChef        = pagedChef.TotalCount,
            PageSizeChef          = pagedChef.PageSize,
            RechercheChef         = rechercheChef
        };
    }

    // ── Délégations DSI ───────────────────────────────────────────────────────
    public async Task<DelegationDetailsResult> GetDsiAsync(Guid id, Guid currentUserId, bool hasFullScope)
    {
        var d = await _db.DelegationsValidationDSI.FirstOrDefaultAsync(x => x.Id == id && !x.EstSupprime);
        if (d == null) return DelegationDetailsResult.NotFound();
        if (!hasFullScope && d.DSIId != currentUserId && d.DelegueId != currentUserId)
            return DelegationDetailsResult.Forbidden();

        return DelegationDetailsResult.Ok(new DelegationDsiDetailsDto(
            d.Id, d.DSIId.ToString(), d.DelegueId.ToString(),
            d.DateDebut.ToString("yyyy-MM-ddTHH:mm:ss"),
            d.DateFin.ToString("yyyy-MM-ddTHH:mm:ss"), d.EstActive));
    }

    public async Task<WorkflowResult> CreateDsiAsync(CreateDelegationDsiInput input)
    {
        var errors = new List<string>();
        var hasDsi    = Guid.TryParse(input.DSIId, out var dsiGuid);
        var hasDelegue = Guid.TryParse(input.DelegueId, out var delegueGuid);

        if (string.IsNullOrWhiteSpace(input.DSIId) || !hasDsi)        errors.Add("Le DSI est requis.");
        if (string.IsNullOrWhiteSpace(input.DelegueId) || !hasDelegue) errors.Add("Le délégué est requis.");
        if (input.DateDebut >= input.DateFin)                          errors.Add("La date de fin doit être postérieure à la date de début.");
        if (hasDsi && !input.HasFullScope && dsiGuid != input.CurrentUserId)
            errors.Add("Vous ne pouvez créer une délégation DSI que pour vous-même.");

        if (errors.Count > 0) return WorkflowResult.Error(errors[0]);

        var delegation = new DelegationValidationDSI
        {
            Id = Guid.NewGuid(), DSIId = dsiGuid, DelegueId = delegueGuid,
            DateDebut = input.DateDebut, DateFin = input.DateFin, EstActive = input.EstActive,
            DateCreation = DateTime.Now, CreePar = _currentUser.Matricule ?? "SYSTEM", EstSupprime = false
        };
        _db.DelegationsValidationDSI.Add(delegation);
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("CreationDelegationDSI", "DelegationValidationDSI", delegation.Id,
            null, new { delegation.DSIId, delegation.DelegueId, delegation.DateDebut, delegation.DateFin });

        return WorkflowResult.Success("Délégation DSI créée avec succès.");
    }

    public async Task<WorkflowResult> UpdateDsiAsync(UpdateDelegationDsiInput input)
    {
        var existing = await _db.DelegationsValidationDSI.FindAsync(input.Id);
        if (existing == null) return WorkflowResult.NotFound();
        if (!input.HasFullScope && existing.DSIId != input.CurrentUserId) return WorkflowResult.Forbidden();

        var errors = new List<string>();
        var hasDsi    = Guid.TryParse(input.DSIId, out var dsiGuid);
        var hasDelegue = Guid.TryParse(input.DelegueId, out var delegueGuid);
        if (string.IsNullOrWhiteSpace(input.DSIId) || !hasDsi)        errors.Add("Le DSI est requis.");
        if (string.IsNullOrWhiteSpace(input.DelegueId) || !hasDelegue) errors.Add("Le délégué est requis.");
        if (input.DateDebut >= input.DateFin)                          errors.Add("La date de fin doit être postérieure à la date de début.");

        if (errors.Count > 0) return WorkflowResult.Error(errors[0]);

        existing.DSIId            = dsiGuid;
        existing.DelegueId        = delegueGuid;
        existing.DateDebut        = input.DateDebut;
        existing.DateFin          = input.DateFin;
        existing.EstActive        = input.EstActive;
        existing.DateModification = DateTime.Now;
        existing.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("ModificationDelegationDSI", "DelegationValidationDSI", existing.Id);
        return WorkflowResult.Success("Délégation DSI modifiée avec succès.");
    }

    public async Task<WorkflowResult> DeleteDsiAsync(Guid id, Guid currentUserId, bool hasFullScope)
    {
        var delegation = await _db.DelegationsValidationDSI.FindAsync(id);
        if (delegation == null) return WorkflowResult.NotFound();
        if (!hasFullScope && delegation.DSIId != currentUserId) return WorkflowResult.Forbidden();

        delegation.EstSupprime      = true;
        delegation.EstActive        = false;
        delegation.DateModification = DateTime.Now;
        delegation.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("ClotureDelegationDSI", "DelegationValidationDSI", delegation.Id,
            new { delegation.DSIId, delegation.DelegueId, delegation.DateDebut, delegation.DateFin });

        return WorkflowResult.Success("Délégation DSI clôturée.");
    }

    // ── Délégations Chef de Projet ─────────────────────────────────────────────
    public async Task<DelegationsChefProjetPageViewModel> GetChefProjetPageAsync(Guid currentUserId, bool hasFullScope)
    {
        var query = _db.DelegationsChefProjet
            .Include(d => d.Delegant).Include(d => d.Delegue)
            .Where(d => !d.EstSupprime).AsQueryable();

        if (!hasFullScope)
            query = query.Where(d => d.DelegantId == currentUserId || d.DelegueId == currentUserId);

        return new DelegationsChefProjetPageViewModel
        {
            Delegations = await query.Include(d => d.Projet).OrderByDescending(d => d.DateDebut).ToListAsync(),
            Projets = await _db.Projets
                .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
                .OrderByDescending(p => p.DateCreation)
                .ToListAsync(),
            Delegants = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                                (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)) &&
                            (hasFullScope || u.Id == currentUserId))
                .OrderBy(u => u.Nom)
                .ToListAsync(),
            Delegues = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                .OrderBy(u => u.Nom)
                .ToListAsync(),
            CurrentUserId = currentUserId,
            CanAdminDelegations = hasFullScope
        };
    }

    public async Task<DelegationDetailsResult> GetChefAsync(Guid id, Guid currentUserId, bool hasFullScope)
    {
        var d = await _db.DelegationsChefProjet.FirstOrDefaultAsync(x => x.Id == id && !x.EstSupprime);
        if (d == null) return DelegationDetailsResult.NotFound();
        if (!hasFullScope && d.DelegantId != currentUserId) return DelegationDetailsResult.Forbidden();

        return DelegationDetailsResult.Ok(new DelegationChefDetailsDto(
            d.Id, d.ProjetId.ToString(), d.DelegantId.ToString(), d.DelegueId.ToString(),
            d.DateDebut.ToString("yyyy-MM-ddTHH:mm:ss"),
            d.DateFin?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "", d.EstActive));
    }

    public async Task<WorkflowResult> CreateChefAsync(CreateDelegationChefInput input)
    {
        var errors = new List<string>();
        var hasProjet   = Guid.TryParse(input.ProjetId, out var projetGuid);
        var hasDelegant = Guid.TryParse(input.DelegantId, out var delegantGuid);
        var hasDelegue  = Guid.TryParse(input.DelegueId, out var delegueGuid);

        if (string.IsNullOrWhiteSpace(input.ProjetId) || !hasProjet)     errors.Add("Le projet est requis.");
        if (string.IsNullOrWhiteSpace(input.DelegantId) || !hasDelegant) errors.Add("Le délégant est requis.");
        if (string.IsNullOrWhiteSpace(input.DelegueId) || !hasDelegue)   errors.Add("Le délégué est requis.");

        if (hasProjet)
        {
            var projet = await _db.Projets.FindAsync(projetGuid);
            if (projet == null || projet.EstSupprime)              errors.Add("Le projet sélectionné n'existe pas.");
            else if (projet.StatutProjet == StatutProjet.Cloture)  errors.Add("Impossible de créer une délégation pour un projet clôturé.");
        }

        if (hasDelegant)
        {
            if (!await EstDsiOuResponsableAsync(delegantGuid))
                errors.Add("Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
            if (!input.HasFullScope && delegantGuid != input.CurrentUserId)
                errors.Add("Vous ne pouvez créer une délégation que pour vous-même.");
        }

        if (errors.Count == 0 && hasProjet && hasDelegant && hasDelegue)
        {
            var existe = await _db.DelegationsChefProjet
                .AnyAsync(d => d.ProjetId == projetGuid && d.EstActive && d.DateFin == null && !d.EstSupprime);
            if (existe) errors.Add("Une délégation active existe déjà pour ce projet.");
        }

        if (errors.Count > 0) return WorkflowResult.Error(errors[0]);

        var delegation = new DelegationChefProjet
        {
            Id = Guid.NewGuid(), ProjetId = projetGuid, DelegantId = delegantGuid, DelegueId = delegueGuid,
            DateDebut = input.DateDebut, DateFin = null, EstActive = input.EstActive,
            DateCreation = DateTime.Now, CreePar = _currentUser.Matricule ?? "SYSTEM", EstSupprime = false
        };
        _db.DelegationsChefProjet.Add(delegation);
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("CREATION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);
        return WorkflowResult.Success("Délégation ChefProjet créée avec succès.");
    }

    public async Task<WorkflowResult> UpdateChefAsync(UpdateDelegationChefInput input)
    {
        var existing = await _db.DelegationsChefProjet.FindAsync(input.Id);
        if (existing == null) return WorkflowResult.NotFound();
        if (!input.HasFullScope && existing.DelegantId != input.CurrentUserId) return WorkflowResult.Forbidden();

        var errors = new List<string>();
        var hasProjet   = Guid.TryParse(input.ProjetId, out var projetGuid);
        var hasDelegant = Guid.TryParse(input.DelegantId, out var delegantGuid);
        var hasDelegue  = Guid.TryParse(input.DelegueId, out var delegueGuid);

        if (string.IsNullOrWhiteSpace(input.ProjetId) || !hasProjet)     errors.Add("Le projet est requis.");
        if (string.IsNullOrWhiteSpace(input.DelegantId) || !hasDelegant) errors.Add("Le délégant est requis.");
        if (string.IsNullOrWhiteSpace(input.DelegueId) || !hasDelegue)   errors.Add("Le délégué est requis.");

        if (hasProjet)
        {
            var projet = await _db.Projets.FindAsync(projetGuid);
            if (projet == null || projet.EstSupprime)              errors.Add("Le projet sélectionné n'existe pas.");
            else if (projet.StatutProjet == StatutProjet.Cloture)  errors.Add("Impossible de modifier une délégation pour un projet clôturé.");
        }

        if (hasDelegant)
        {
            if (!await EstDsiOuResponsableAsync(delegantGuid))
                errors.Add("Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
            if (!input.HasFullScope && delegantGuid != input.CurrentUserId)
                errors.Add("Vous ne pouvez modifier que vos propres délégations.");
        }

        if (errors.Count == 0 && hasProjet && hasDelegant && hasDelegue)
        {
            var autre = await _db.DelegationsChefProjet
                .AnyAsync(d => d.ProjetId == projetGuid && d.Id != input.Id && d.EstActive && d.DateFin == null && !d.EstSupprime);
            if (autre) errors.Add("Une autre délégation active existe déjà pour ce projet.");
        }

        if (errors.Count > 0) return WorkflowResult.Error(errors[0]);

        existing.ProjetId         = projetGuid;
        existing.DelegantId       = delegantGuid;
        existing.DelegueId        = delegueGuid;
        existing.DateDebut        = input.DateDebut;
        existing.EstActive        = input.EstActive;
        existing.DateModification = DateTime.Now;
        existing.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("MODIFICATION_DELEGATION_CHEFPROJET", "DelegationChefProjet", existing.Id);
        return WorkflowResult.Success("Délégation ChefProjet modifiée avec succès.");
    }

    public async Task<WorkflowResult> DeleteChefAsync(Guid id, Guid currentUserId, bool hasFullScope)
    {
        var delegation = await _db.DelegationsChefProjet.FindAsync(id);
        if (delegation == null) return WorkflowResult.NotFound();
        if (!hasFullScope && delegation.DelegantId != currentUserId) return WorkflowResult.Forbidden();

        delegation.EstSupprime = true;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("SUPPRESSION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);
        return WorkflowResult.Success("Délégation ChefProjet supprimée.");
    }

    private async Task<bool> EstDsiOuResponsableAsync(Guid utilisateurId)
    {
        var existe = await _db.Utilisateurs.FindAsync(utilisateurId);
        if (existe == null) return false;
        return await _db.UtilisateurRoles.AnyAsync(ur =>
            ur.UtilisateurId == utilisateurId && !ur.EstSupprime &&
            (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT));
    }
}
