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

public class ServiceAdminService : IServiceAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ServiceAdminService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ServicesListViewModel> GetListAsync(string? recherche, Guid? directionId, string? statut, int page, int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Services
            .Include(s => s.Direction)
            .Where(s => !s.EstSupprime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
            query = query.Where(s => s.Libelle.Contains(recherche) || s.Code.Contains(recherche));

        if (directionId.HasValue)
            query = query.Where(s => s.DirectionId == directionId.Value);

        if (statut == "active")
            query = query.Where(s => s.EstActive);
        else if (statut == "inactive")
            query = query.Where(s => !s.EstActive);

        query = query.OrderBy(s => s.Libelle);

        var paged = await query.ToPagedResultAsync(page, pageSize);

        var directions = await _db.Directions
            .Where(d => !d.EstSupprime && d.EstActive)
            .OrderBy(d => d.Libelle)
            .ToListAsync();

        return new ServicesListViewModel
        {
            Services            = paged.Items,
            Directions          = directions,
            TotalCount          = paged.TotalCount,
            PageNumber          = paged.PageNumber,
            TotalPages          = paged.TotalPages,
            PageSize            = paged.PageSize,
            Recherche           = recherche,
            SelectedDirectionId = directionId,
            Statut              = statut
        };
    }

    public async Task<OperationResult> CreateAsync(CreateServiceInput input)
    {
        Guid.TryParse(input.DirectionId, out var directionGuid);

        var code = string.IsNullOrWhiteSpace(input.Code)
            ? await GenerateCodeAsync(input.Libelle, directionGuid == Guid.Empty ? null : directionGuid)
            : input.Code.Trim();

        var effective  = input with { Code = code };
        var validation = await new CreateServiceValidator().ValidateAsync(effective);
        var errors     = validation.Errors
            .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
            .ToList();

        if (!string.IsNullOrWhiteSpace(code) && validation.IsValid)
        {
            var exists = await _db.Services.AnyAsync(s => s.Code == code && !s.EstSupprime);
            if (exists)
                errors.Add(new FieldError("Code", "Ce code de service existe déjà."));
        }

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        var service = new Service
        {
            Id           = Guid.NewGuid(),
            Code         = code,
            Libelle      = input.Libelle!.Trim(),
            DirectionId  = directionGuid,
            EstActive    = input.EstActive,
            DateCreation = DateTime.UtcNow,
            CreePar      = _currentUser.Matricule ?? "SYSTEM",
            EstSupprime  = false
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("CREATION_SERVICE", "Service", service.Id,
            null, new { service.Code, service.Libelle, DirectionId = directionGuid });

        return OperationResult.Success("Service créé avec succès.");
    }

    public async Task<OperationResult> UpdateAsync(UpdateServiceInput input)
    {
        var existing = await _db.Services.FindAsync(input.Id);
        if (existing == null)
            return OperationResult.NotFound();

        var validation = await new UpdateServiceValidator().ValidateAsync(input);
        var errors     = validation.Errors
            .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
            .ToList();

        if (!string.IsNullOrWhiteSpace(input.Code) && input.Code != existing.Code && validation.IsValid)
        {
            var exists = await _db.Services
                .AnyAsync(s => s.Code == input.Code && s.Id != input.Id && !s.EstSupprime);
            if (exists)
                errors.Add(new FieldError("Code", "Ce code de service existe déjà."));
        }

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        Guid.TryParse(input.DirectionId, out var directionGuid);

        // Capturer les anciennes valeurs AVANT mutation (corrige le bug d'audit
        // d'origine qui loggait les nouvelles valeurs des deux côtés).
        var ancienCode    = existing.Code;
        var ancienLibelle = existing.Libelle;

        existing.Code             = input.Code!.Trim();
        existing.Libelle          = input.Libelle!.Trim();
        existing.DirectionId      = directionGuid;
        existing.DateModification = DateTime.UtcNow;
        existing.ModifiePar       = _currentUser.Matricule;

        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("MODIFICATION_SERVICE", "Service", existing.Id,
            new { AncienCode = ancienCode, AncienLibelle = ancienLibelle },
            new { NouveauCode = existing.Code, NouveauLibelle = existing.Libelle });

        return OperationResult.Success("Service modifié avec succès.");
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        var service = await _db.Services.FindAsync(id);
        if (service == null)
            return OperationResult.NotFound();

        service.EstSupprime = true;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("SUPPRESSION_SERVICE", "Service", service.Id);

        return OperationResult.Success("Service supprimé.");
    }

    private async Task<string> GenerateCodeAsync(string? libelle, Guid? directionId)
    {
        if (string.IsNullOrWhiteSpace(libelle) || !directionId.HasValue)
            return string.Empty;

        var direction = await _db.Directions.FindAsync(directionId.Value);
        if (direction == null || string.IsNullOrWhiteSpace(direction.Code))
            return string.Empty;

        return $"{direction.Code}-{CodeGenerator.FromLibelle(libelle)}";
    }
}
