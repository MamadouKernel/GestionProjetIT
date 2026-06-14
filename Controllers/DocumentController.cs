using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Web.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers;

[Authorize]
public class DocumentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentPreviewService _documentPreviewService;
    private readonly IPermissionService _permissionService;

    public DocumentController(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        IDocumentPreviewService documentPreviewService,
        IPermissionService permissionService)
    {
        _db = db;
        _fileStorage = fileStorage;
        _documentPreviewService = documentPreviewService;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> PreviewDemandeCahier(Guid demandeId)
    {
        var userId = User.GetUserIdOrThrow();
        var demande = await _db.DemandesProjets
            .Include(d => d.Annexes)
            .FirstOrDefaultAsync(d => d.Id == demandeId);
        if (demande == null)
            return NotFound();

        if (!await CanAccessDemandeAsync(demande, userId))
            return Forbid();

        var cahierDocument = GetCahierChargesDocument(demande);
        var relativePath = cahierDocument?.CheminRelatif ?? demande.CahierChargesPath;
        if (string.IsNullOrWhiteSpace(relativePath))
            return NotFound();

        var fileName = string.IsNullOrWhiteSpace(cahierDocument?.NomFichier)
            ? Path.GetFileName(relativePath)
            : cahierDocument.NomFichier;
        var model = await _documentPreviewService.BuildPreviewAsync(
            relativePath,
            fileName,
            $"Lecture du cahier des charges - {demande.Titre}",
            Url.Action(nameof(StreamDemandeCahier), new { demandeId }) ?? string.Empty,
            Url.Action(nameof(StreamDemandeCahier), new { demandeId, download = true }) ?? string.Empty);

        return View("Preview", model);
    }

    public async Task<IActionResult> StreamDemandeCahier(Guid demandeId, bool download = false)
    {
        var userId = User.GetUserIdOrThrow();
        var demande = await _db.DemandesProjets
            .Include(d => d.Annexes)
            .FirstOrDefaultAsync(d => d.Id == demandeId);
        if (demande == null)
            return NotFound();

        if (!await CanAccessDemandeAsync(demande, userId))
            return Forbid();

        var cahierDocument = GetCahierChargesDocument(demande);
        var relativePath = cahierDocument?.CheminRelatif ?? demande.CahierChargesPath;
        if (string.IsNullOrWhiteSpace(relativePath))
            return NotFound();

        var fileName = string.IsNullOrWhiteSpace(cahierDocument?.NomFichier)
            ? Path.GetFileName(relativePath)
            : cahierDocument.NomFichier;

        return BuildFileResult(relativePath, fileName, download);
    }

    public async Task<IActionResult> PreviewDemandeAnnexe(Guid demandeId, Guid documentId)
    {
        var userId = User.GetUserIdOrThrow();
        var demande = await _db.DemandesProjets
            .Include(d => d.Annexes)
            .FirstOrDefaultAsync(d => d.Id == demandeId);
        if (demande == null)
            return NotFound();

        if (!await CanAccessDemandeAsync(demande, userId))
            return Forbid();

        var document = demande.Annexes.FirstOrDefault(a => a.Id == documentId &&
                                                            !a.EstSupprime &&
                                                            (string.IsNullOrWhiteSpace(demande.CahierChargesPath) ||
                                                             !string.Equals(a.CheminRelatif, demande.CahierChargesPath, StringComparison.OrdinalIgnoreCase)));
        if (document == null)
            return NotFound();

        var model = await _documentPreviewService.BuildPreviewAsync(
            document.CheminRelatif,
            document.NomFichier,
            $"Lecture du document joint - {document.NomFichier}",
            Url.Action(nameof(StreamDemandeAnnexe), new { demandeId, documentId }) ?? string.Empty,
            Url.Action(nameof(StreamDemandeAnnexe), new { demandeId, documentId, download = true }) ?? string.Empty);

        return View("Preview", model);
    }

    public async Task<IActionResult> StreamDemandeAnnexe(Guid demandeId, Guid documentId, bool download = false)
    {
        var userId = User.GetUserIdOrThrow();
        var demande = await _db.DemandesProjets
            .Include(d => d.Annexes)
            .FirstOrDefaultAsync(d => d.Id == demandeId);
        if (demande == null)
            return NotFound();

        if (!await CanAccessDemandeAsync(demande, userId))
            return Forbid();

        var document = demande.Annexes.FirstOrDefault(a => a.Id == documentId &&
                                                            !a.EstSupprime &&
                                                            (string.IsNullOrWhiteSpace(demande.CahierChargesPath) ||
                                                             !string.Equals(a.CheminRelatif, demande.CahierChargesPath, StringComparison.OrdinalIgnoreCase)));
        if (document == null)
            return NotFound();

        return BuildFileResult(document.CheminRelatif, document.NomFichier, download);
    }

    public async Task<IActionResult> PreviewProjetLivrable(Guid projetId, Guid livrableId)
    {
        var userId = User.GetUserIdOrThrow();
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet == null)
            return NotFound();

        if (!await CanAccessProjetAsync(projet, userId))
            return Forbid();

        var livrable = projet.Livrables.FirstOrDefault(l => l.Id == livrableId && !l.EstSupprime);
        if (livrable == null)
            return NotFound();

        var model = await _documentPreviewService.BuildPreviewAsync(
            livrable.CheminRelatif,
            livrable.NomDocument,
            $"Lecture du document projet - {livrable.NomDocument}",
            Url.Action(nameof(StreamProjetLivrable), new { projetId, livrableId }) ?? string.Empty,
            Url.Action(nameof(StreamProjetLivrable), new { projetId, livrableId, download = true }) ?? string.Empty);

        return View("Preview", model);
    }

    public async Task<IActionResult> StreamProjetLivrable(Guid projetId, Guid livrableId, bool download = false)
    {
        var userId = User.GetUserIdOrThrow();
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet == null)
            return NotFound();

        if (!await CanAccessProjetAsync(projet, userId))
            return Forbid();

        var livrable = projet.Livrables.FirstOrDefault(l => l.Id == livrableId && !l.EstSupprime);
        if (livrable == null)
            return NotFound();

        return BuildFileResult(livrable.CheminRelatif, livrable.NomDocument, download);
    }

    public async Task<IActionResult> PreviewDossierSignatureSource(Guid projetId, Guid dossierId)
    {
        var userId = User.GetUserIdOrThrow();
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet == null)
            return NotFound();

        if (!await CanAccessProjetAsync(projet, userId))
            return Forbid();

        var dossier = await _db.DossiersSignatureProjets
            .FirstOrDefaultAsync(d => d.Id == dossierId && d.ProjetId == projetId);
        if (dossier == null || string.IsNullOrWhiteSpace(dossier.CheminDocumentSource))
            return NotFound();

        var displayName = string.IsNullOrWhiteSpace(dossier.NomDocumentSource)
            ? Path.GetFileName(dossier.CheminDocumentSource)
            : dossier.NomDocumentSource;

        var model = await _documentPreviewService.BuildPreviewAsync(
            dossier.CheminDocumentSource,
            displayName,
            $"Lecture du document source - {displayName}",
            Url.Action(nameof(StreamDossierSignatureSource), new { projetId, dossierId }) ?? string.Empty,
            Url.Action(nameof(StreamDossierSignatureSource), new { projetId, dossierId, download = true }) ?? string.Empty);

        return View("Preview", model);
    }

    public async Task<IActionResult> StreamDossierSignatureSource(Guid projetId, Guid dossierId, bool download = false)
    {
        var userId = User.GetUserIdOrThrow();
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet == null)
            return NotFound();

        if (!await CanAccessProjetAsync(projet, userId))
            return Forbid();

        var dossier = await _db.DossiersSignatureProjets
            .FirstOrDefaultAsync(d => d.Id == dossierId && d.ProjetId == projetId);
        if (dossier == null || string.IsNullOrWhiteSpace(dossier.CheminDocumentSource))
            return NotFound();

        var displayName = string.IsNullOrWhiteSpace(dossier.NomDocumentSource)
            ? Path.GetFileName(dossier.CheminDocumentSource)
            : dossier.NomDocumentSource;

        return BuildFileResult(dossier.CheminDocumentSource, displayName, download);
    }

    private IActionResult BuildFileResult(string relativePath, string fileName, bool download)
    {
        var absolutePath = _fileStorage.GetAbsolutePath(relativePath);
        if (!System.IO.File.Exists(absolutePath))
            return NotFound();

        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; font-src 'self' https://cdn.jsdelivr.net;";
        Response.Headers["Content-Disposition"] = $"{(download ? "attachment" : "inline")}; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";

        return PhysicalFile(
            absolutePath,
            _documentPreviewService.GetMimeType(fileName),
            enableRangeProcessing: true);
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
            return true;

        if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDM") &&
            demande.DirecteurMetierId == userId)
            return true;

        if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Details") &&
            demande.DemandeurId == userId)
            return true;

        return false;
    }

    private async Task<bool> CanAccessProjetAsync(Projet projet, Guid userId)
    {
        var directionId = await GetCurrentUserDirectionIdAsync(userId);
        var ui = await ProjetUiPermissionBuilder.BuildAsync(
            _permissionService,
            User,
            projet,
            isDemandeurProject: projet.DemandeProjet?.DemandeurId == userId,
            currentUserDirectionId: directionId);

        return ui.CanViewProject;
    }
}
