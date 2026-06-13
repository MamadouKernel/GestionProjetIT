using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Helpers;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class DirectionAdminService : IDirectionAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public DirectionAdminService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<DirectionsListViewModel> GetListAsync(string? recherche, string? statut, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Directions
            .Include(d => d.DSI)
            .Where(d => !d.EstSupprime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(d => d.Libelle.Contains(recherche) || d.Code.Contains(recherche));

        if (statut == "active")
            query = query.Where(d => d.EstActive);
        else if (statut == "inactive")
            query = query.Where(d => !d.EstActive);

        query = query.OrderBy(d => d.Libelle);

        var paged = await query.ToPagedResultAsync(page, pageSize);

        var dsis = await _db.Utilisateurs
            .Where(u => !u.EstSupprime)
            .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
            .ToListAsync();

        return new DirectionsListViewModel
        {
            Directions = paged.Items,
            DSIs       = dsis,
            TotalCount = paged.TotalCount,
            PageNumber = paged.PageNumber,
            TotalPages = paged.TotalPages,
            PageSize   = paged.PageSize,
            Recherche  = recherche,
            Statut     = statut
        };
    }

    public async Task<string?> GetCodeAsync(Guid id)
    {
        var direction = await _db.Directions.FindAsync(id);
        return direction?.Code;
    }

    public async Task<OperationResult> CreateAsync(CreateDirectionInput input)
    {
        var code = string.IsNullOrWhiteSpace(input.Code)
            ? CodeGenerator.FromLibelle(input.Libelle)
            : input.Code.Trim();

        var effective  = input with { Code = code };
        var validation = await new CreateDirectionValidator().ValidateAsync(effective);
        var errors     = validation.Errors
            .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
            .ToList();

        if (!string.IsNullOrWhiteSpace(code) && validation.IsValid)
        {
            var exists = await _db.Directions.AnyAsync(d => d.Code == code && !d.EstSupprime);
            if (exists)
                errors.Add(new FieldError("Code", "Ce code de direction existe déjà."));
        }

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        var direction = new Direction
        {
            Id           = Guid.NewGuid(),
            Code         = code,
            Libelle      = input.Libelle!.Trim(),
            EstActive    = input.EstActive,
            DateCreation = DateTime.Now,
            CreePar      = _currentUser.Matricule ?? "SYSTEM",
            EstSupprime  = false,
            DSIId        = ParseDsiId(input.DSIId)
        };

        _db.Directions.Add(direction);
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("CreationDirection", "Direction", direction.Id,
            null, new { direction.Code, direction.Libelle });

        return OperationResult.Success("Direction créée avec succès.");
    }

    public async Task<OperationResult> UpdateAsync(UpdateDirectionInput input)
    {
        var existing = await _db.Directions.FindAsync(input.Id);
        if (existing == null)
            return OperationResult.NotFound();

        var validation = await new UpdateDirectionValidator().ValidateAsync(input);
        var errors     = validation.Errors
            .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
            .ToList();

        if (!string.IsNullOrWhiteSpace(input.Code) && input.Code != existing.Code && validation.IsValid)
        {
            var exists = await _db.Directions
                .AnyAsync(d => d.Code == input.Code && d.Id != input.Id && !d.EstSupprime);
            if (exists)
                errors.Add(new FieldError("Code", "Ce code de direction existe déjà."));
        }

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        // Capturer les anciennes valeurs AVANT mutation (le code original loggait
        // les nouvelles valeurs des deux côtés — corrigé ici).
        var ancienCode    = existing.Code;
        var ancienLibelle = existing.Libelle;

        existing.Code             = input.Code!.Trim();
        existing.Libelle          = input.Libelle!.Trim();
        existing.EstActive        = input.EstActive;
        existing.DSIId            = ParseDsiId(input.DSIId);
        existing.DateModification = DateTime.Now;
        existing.ModifiePar       = _currentUser.Matricule;

        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("ModificationDirection", "Direction", existing.Id,
            new { AncienCode = ancienCode, AncienLibelle = ancienLibelle },
            new { NouveauCode = existing.Code, NouveauLibelle = existing.Libelle });

        return OperationResult.Success("Direction modifiée avec succès.");
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        var direction = await _db.Directions.FindAsync(id);
        if (direction == null)
            return OperationResult.NotFound();

        direction.EstSupprime = true;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("SUPPRESSION_DIRECTION", "Direction", direction.Id);

        return OperationResult.Success("Direction supprimée.");
    }

    private static Guid? ParseDsiId(string? dsiId) =>
        !string.IsNullOrWhiteSpace(dsiId) && Guid.TryParse(dsiId, out var g) ? g : null;
}
