using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.Validators.Admin;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class ParametreAdminService : IParametreAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ParametreAdminService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ParametresViewModel> GetViewModelAsync()
    {
        var parametres = await _db.ParametresSysteme
            .Where(p => !p.EstSupprime)
            .OrderBy(p => p.Cle)
            .ToListAsync();

        var utilisateursDsi = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Where(u => !u.EstSupprime &&
                        u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                            (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenoms)
            .ToListAsync();

        return new ParametresViewModel
        {
            Parametres                    = parametres,
            UtilisateursDsi               = utilisateursDsi,
            DSIPrincipalId                = parametres.FirstOrDefault(p => p.Cle == "DSIPrincipalId")?.Valeur,
            DSIDelegueId                  = parametres.FirstOrDefault(p => p.Cle == "DSIDelegueId")?.Valeur,
            DelaiInactiviteSessionMinutes = parametres.FirstOrDefault(p => p.Cle == "DelaiInactiviteSessionMinutes")?.Valeur,
            RepertoireStockageRacine      = parametres.FirstOrDefault(p => p.Cle == "RepertoireStockageRacine")?.Valeur,
            TypesLivrables                = parametres.FirstOrDefault(p => p.Cle == "TypesLivrables")?.Valeur
        };
    }

    public async Task SaveWorkflowAsync(ParametresWorkflowInput input)
    {
        await UpsertAsync("DSIPrincipalId", input.DsiPrincipalId?.Trim() ?? string.Empty, "Identifiant du DSI principal");
        await UpsertAsync("DSIDelegueId", input.DsiDelegueId?.Trim() ?? string.Empty, "Identifiant du délégué DSI");
        await UpsertAsync("DelaiInactiviteSessionMinutes", (input.DelaiInactiviteSessionMinutes ?? 30).ToString(), "Délai d'inactivité de session en minutes");
        await UpsertAsync("RepertoireStockageRacine", input.RepertoireStockageRacine?.Trim() ?? string.Empty, "Répertoire racine de stockage documentaire");
        await UpsertAsync("TypesLivrables", input.TypesLivrables?.Trim() ?? string.Empty, "Liste des types de livrables obligatoires");

        await _db.SaveChangesAsync();
        await _audit.LogActionAsync("ModificationParametre", "ParametreSysteme", Guid.Empty, null, new
        {
            input.DsiPrincipalId,
            input.DsiDelegueId,
            input.DelaiInactiviteSessionMinutes,
            input.RepertoireStockageRacine,
            input.TypesLivrables
        });
    }

    public async Task<ParametreDetailsDto?> GetDetailsAsync(Guid id)
    {
        var p = await _db.ParametresSysteme.FirstOrDefaultAsync(x => x.Id == id && !x.EstSupprime);
        return p is null ? null : new ParametreDetailsDto(p.Id, p.Cle, p.Valeur, p.Description ?? "");
    }

    public async Task<OperationResult> CreateAsync(CreateParametreInput input)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(input.Cle))    errors.Add(new("Cle", "La clé est requise."));
        if (string.IsNullOrWhiteSpace(input.Valeur)) errors.Add(new("Valeur", "La valeur est requise."));

        if (!string.IsNullOrWhiteSpace(input.Cle)
            && await _db.ParametresSysteme.AnyAsync(p => p.Cle == input.Cle && !p.EstSupprime))
            errors.Add(new("Cle", "Cette clé existe déjà."));

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        var parametre = new ParametreSysteme
        {
            Id           = Guid.NewGuid(),
            Cle          = input.Cle!.Trim(),
            Valeur       = input.Valeur?.Trim() ?? string.Empty,
            Description  = input.Description?.Trim() ?? string.Empty,
            DateCreation = DateTime.UtcNow,
            CreePar      = _currentUser.Matricule ?? "SYSTEM",
            EstSupprime  = false
        };

        _db.ParametresSysteme.Add(parametre);
        await _db.SaveChangesAsync();
        await _audit.LogActionAsync("ModificationParametre", "ParametreSysteme", parametre.Id);

        return OperationResult.Success("Paramètre créé avec succès.");
    }

    public async Task<OperationResult> UpdateAsync(UpdateParametreInput input)
    {
        var existing = await _db.ParametresSysteme.FindAsync(input.Id);
        if (existing == null)
            return OperationResult.NotFound();

        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(input.Cle))    errors.Add(new("Cle", "La clé est requise."));
        if (string.IsNullOrWhiteSpace(input.Valeur)) errors.Add(new("Valeur", "La valeur est requise."));

        if (!string.IsNullOrWhiteSpace(input.Cle) && input.Cle != existing.Cle
            && await _db.ParametresSysteme.AnyAsync(p => p.Cle == input.Cle && p.Id != input.Id && !p.EstSupprime))
            errors.Add(new("Cle", "Cette clé existe déjà."));

        if (errors.Count > 0)
            return OperationResult.Invalid(errors);

        var ancienneCle    = existing.Cle;
        var ancienneValeur = existing.Valeur;

        existing.Cle              = input.Cle!.Trim();
        existing.Valeur           = input.Valeur?.Trim() ?? string.Empty;
        existing.Description      = input.Description?.Trim() ?? string.Empty;
        existing.DateModification = DateTime.UtcNow;
        existing.ModifiePar       = _currentUser.Matricule;

        await _db.SaveChangesAsync();
        await _audit.LogActionAsync("ModificationParametre", "ParametreSysteme", existing.Id,
            new { AncienneCle = ancienneCle, AncienneValeur = ancienneValeur },
            new { NouvelleCle = existing.Cle, NouvelleValeur = existing.Valeur });

        return OperationResult.Success("Paramètre modifié avec succès.");
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        var parametre = await _db.ParametresSysteme.FindAsync(id);
        if (parametre == null)
            return OperationResult.NotFound();

        parametre.EstSupprime = true;
        await _db.SaveChangesAsync();
        await _audit.LogActionAsync("SUPPRESSION_PARAMETRE", "ParametreSysteme", parametre.Id);

        return OperationResult.Success("Paramètre supprimé.");
    }

    public async Task<string> SaveTeamsWebhookAsync(Guid? parametreId, string? webhookUrl)
    {
        var url = webhookUrl?.Trim() ?? string.Empty;

        if (parametreId.HasValue)
        {
            var param = await _db.ParametresSysteme.FindAsync(parametreId.Value);
            if (param != null)
            {
                param.Valeur           = url;
                param.DateModification = DateTime.UtcNow;
                param.ModifiePar       = _currentUser.Matricule;
                await _db.SaveChangesAsync();
                await _audit.LogActionAsync("MAJ_TEAMS_WEBHOOK", "ParametreSysteme", param.Id);
            }
        }
        else
        {
            var param = new ParametreSysteme
            {
                Id           = Guid.NewGuid(),
                Cle          = "TeamsWebhookUrl",
                Valeur       = url,
                Description  = "URL du webhook entrant Microsoft Teams pour les notifications",
                DateCreation = DateTime.UtcNow,
                CreePar      = _currentUser.Matricule ?? "SYSTEM",
                EstSupprime  = false
            };
            _db.ParametresSysteme.Add(param);
            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("CREATION_TEAMS_WEBHOOK", "ParametreSysteme", param.Id);
        }

        return string.IsNullOrWhiteSpace(url)
            ? "Webhook Teams supprimé."
            : "Webhook Teams enregistré avec succès.";
    }

    private async Task UpsertAsync(string cle, string valeur, string description)
    {
        var parametre = await _db.ParametresSysteme.FirstOrDefaultAsync(p => p.Cle == cle && !p.EstSupprime);
        if (parametre == null)
        {
            _db.ParametresSysteme.Add(new ParametreSysteme
            {
                Id = Guid.NewGuid(), Cle = cle, Valeur = valeur, Description = description,
                DateCreation = DateTime.UtcNow, CreePar = _currentUser.Matricule ?? "SYSTEM", EstSupprime = false
            });
        }
        else
        {
            parametre.Valeur           = valeur;
            parametre.Description      = description;
            parametre.DateModification = DateTime.UtcNow;
            parametre.ModifiePar       = _currentUser.Matricule;
        }
    }
}
