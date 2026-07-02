using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class DocumentAccessService : IDocumentAccessService
{
    private readonly ApplicationDbContext _db;
    private readonly IPermissionService _permissionService;

    public DocumentAccessService(ApplicationDbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<DocumentAccessResult> GetDemandeCahierAsync(Guid demandeId, Guid userId)
    {
        var demande = await _db.DemandesProjets
            .Include(d => d.Annexes)
            .FirstOrDefaultAsync(d => d.Id == demandeId);
        if (demande == null)
        {
            return DocumentAccessResult.NotFound();
        }

        if (!await CanAccessDemandeAsync(demande, userId))
        {
            return DocumentAccessResult.Forbidden();
        }

        var cahierDocument = GetCahierChargesDocument(demande);
        var relativePath = cahierDocument?.CheminRelatif ?? demande.CahierChargesPath;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return DocumentAccessResult.NotFound();
        }

        var fileName = string.IsNullOrWhiteSpace(cahierDocument?.NomFichier)
            ? Path.GetFileName(relativePath)
            : cahierDocument.NomFichier;

        return DocumentAccessResult.Success(relativePath, fileName, $"Lecture du cahier des charges - {demande.Titre}");
    }

    public async Task<DocumentAccessResult> GetDemandeAnnexeAsync(Guid demandeId, Guid documentId, Guid userId)
    {
        var demande = await _db.DemandesProjets
            .Include(d => d.Annexes)
            .FirstOrDefaultAsync(d => d.Id == demandeId);
        if (demande == null)
        {
            return DocumentAccessResult.NotFound();
        }

        if (!await CanAccessDemandeAsync(demande, userId))
        {
            return DocumentAccessResult.Forbidden();
        }

        var document = demande.Annexes.FirstOrDefault(a => a.Id == documentId &&
                                                            !a.EstSupprime &&
                                                            (string.IsNullOrWhiteSpace(demande.CahierChargesPath) ||
                                                             !string.Equals(a.CheminRelatif, demande.CahierChargesPath, StringComparison.OrdinalIgnoreCase)));
        if (document == null)
        {
            return DocumentAccessResult.NotFound();
        }

        return DocumentAccessResult.Success(
            document.CheminRelatif,
            document.NomFichier,
            $"Lecture du document joint - {document.NomFichier}");
    }

    public async Task<DocumentAccessResult> GetProjetLivrableAsync(Guid projetId, Guid livrableId, Guid userId)
    {
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet == null)
        {
            return DocumentAccessResult.NotFound();
        }

        if (!await CanAccessProjetAsync(projet, userId))
        {
            return DocumentAccessResult.Forbidden();
        }

        var livrable = projet.Livrables.FirstOrDefault(l => l.Id == livrableId && !l.EstSupprime);
        if (livrable == null)
        {
            return DocumentAccessResult.NotFound();
        }

        return DocumentAccessResult.Success(
            livrable.CheminRelatif,
            livrable.NomDocument,
            $"Lecture du document projet - {livrable.NomDocument}");
    }

    public async Task<DocumentAccessResult> GetDossierSignatureSourceAsync(Guid projetId, Guid dossierId, Guid userId)
    {
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet == null)
        {
            return DocumentAccessResult.NotFound();
        }

        if (!await CanAccessProjetAsync(projet, userId))
        {
            return DocumentAccessResult.Forbidden();
        }

        var dossier = await _db.DossiersSignatureProjets
            .FirstOrDefaultAsync(d => d.Id == dossierId && d.ProjetId == projetId);
        if (dossier == null || string.IsNullOrWhiteSpace(dossier.CheminDocumentSource))
        {
            return DocumentAccessResult.NotFound();
        }

        var displayName = string.IsNullOrWhiteSpace(dossier.NomDocumentSource)
            ? Path.GetFileName(dossier.CheminDocumentSource)
            : dossier.NomDocumentSource;

        return DocumentAccessResult.Success(
            dossier.CheminDocumentSource,
            displayName,
            $"Lecture du document source - {displayName}");
    }

    private static DocumentJointDemande? GetCahierChargesDocument(DemandeProjet demande)
    {
        return demande.Annexes.FirstOrDefault(a => !a.EstSupprime &&
                                                   !string.IsNullOrWhiteSpace(demande.CahierChargesPath) &&
                                                   string.Equals(a.CheminRelatif, demande.CahierChargesPath, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Guid?> GetCurrentUserDirectionIdAsync(Guid userId)
    {
        return await _db.Utilisateurs
            .Where(u => u.Id == userId)
            .Select(u => u.DirectionId)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> CanAccessDemandeAsync(DemandeProjet demande, Guid userId)
    {
        if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDSI"))
        {
            return true;
        }

        if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDM") &&
            demande.DirecteurMetierId == userId)
        {
            return true;
        }

        if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Details") &&
            demande.DemandeurId == userId)
        {
            return true;
        }

        return false;
    }

    private async Task<bool> CanAccessProjetAsync(Projet projet, Guid userId)
    {
        var directionId = await GetCurrentUserDirectionIdAsync(userId);
        var activePermissions = await _permissionService.GetCurrentUserActivePermissionsAsync();
        var permissionKeys = new HashSet<string>(
            activePermissions.Select(p => $"{p.Controleur}::{p.Action}"),
            StringComparer.OrdinalIgnoreCase);

        var isAssignedChefProjet = projet.ChefProjetId.HasValue && projet.ChefProjetId.Value == userId;
        var isDelegatedChefProjet = !isAssignedChefProjet &&
            await _permissionService.IsActiveChefProjetDelegateAsync(projet.Id, userId);
        var utilisateurRoles = await _db.UtilisateurRoles
            .Where(ur => ur.UtilisateurId == userId && !ur.EstSupprime)
            .Select(ur => ur.Role)
            .ToListAsync();

        var ui = new ProjetUiPermissions
        {
            UserId = userId,
            CurrentUserDirectionId = directionId,
            IsDemandeurProject = projet.DemandeProjet?.DemandeurId == userId,
            IsAssignedChefProjet = isAssignedChefProjet,
            IsDelegatedChefProjet = isDelegatedChefProjet,
            IsProjectSponsor = projet.SponsorId == userId,
            IsProjectInUserDirection = directionId.HasValue && projet.DirectionId == directionId.Value,
            IsAdminIT = utilisateurRoles.Contains(RoleUtilisateur.AdminIT),
            IsResponsableSolutionIT = utilisateurRoles.Contains(RoleUtilisateur.ResponsableSolutionsIT),
            ActivePermissionKeys = permissionKeys
        };

        return ui.CanViewProject;
    }
}
