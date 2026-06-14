using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers;

[Authorize]
public class DocumentController : Controller
{
    private readonly IDocumentAccessService _documentAccess;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentPreviewService _documentPreviewService;

    public DocumentController(
        IDocumentAccessService documentAccess,
        IFileStorageService fileStorage,
        IDocumentPreviewService documentPreviewService)
    {
        _documentAccess = documentAccess;
        _fileStorage = fileStorage;
        _documentPreviewService = documentPreviewService;
    }

    public async Task<IActionResult> PreviewDemandeCahier(Guid demandeId)
    {
        var result = await _documentAccess.GetDemandeCahierAsync(demandeId, User.GetUserIdOrThrow());
        return await BuildPreviewResultAsync(
            result,
            Url.Action(nameof(StreamDemandeCahier), new { demandeId }) ?? string.Empty,
            Url.Action(nameof(StreamDemandeCahier), new { demandeId, download = true }) ?? string.Empty);
    }

    public async Task<IActionResult> StreamDemandeCahier(Guid demandeId, bool download = false)
    {
        var result = await _documentAccess.GetDemandeCahierAsync(demandeId, User.GetUserIdOrThrow());
        return BuildFileResult(result, download);
    }

    public async Task<IActionResult> PreviewDemandeAnnexe(Guid demandeId, Guid documentId)
    {
        var result = await _documentAccess.GetDemandeAnnexeAsync(demandeId, documentId, User.GetUserIdOrThrow());
        return await BuildPreviewResultAsync(
            result,
            Url.Action(nameof(StreamDemandeAnnexe), new { demandeId, documentId }) ?? string.Empty,
            Url.Action(nameof(StreamDemandeAnnexe), new { demandeId, documentId, download = true }) ?? string.Empty);
    }

    public async Task<IActionResult> StreamDemandeAnnexe(Guid demandeId, Guid documentId, bool download = false)
    {
        var result = await _documentAccess.GetDemandeAnnexeAsync(demandeId, documentId, User.GetUserIdOrThrow());
        return BuildFileResult(result, download);
    }

    public async Task<IActionResult> PreviewProjetLivrable(Guid projetId, Guid livrableId)
    {
        var result = await _documentAccess.GetProjetLivrableAsync(projetId, livrableId, User.GetUserIdOrThrow());
        return await BuildPreviewResultAsync(
            result,
            Url.Action(nameof(StreamProjetLivrable), new { projetId, livrableId }) ?? string.Empty,
            Url.Action(nameof(StreamProjetLivrable), new { projetId, livrableId, download = true }) ?? string.Empty);
    }

    public async Task<IActionResult> StreamProjetLivrable(Guid projetId, Guid livrableId, bool download = false)
    {
        var result = await _documentAccess.GetProjetLivrableAsync(projetId, livrableId, User.GetUserIdOrThrow());
        return BuildFileResult(result, download);
    }

    public async Task<IActionResult> PreviewDossierSignatureSource(Guid projetId, Guid dossierId)
    {
        var result = await _documentAccess.GetDossierSignatureSourceAsync(projetId, dossierId, User.GetUserIdOrThrow());
        return await BuildPreviewResultAsync(
            result,
            Url.Action(nameof(StreamDossierSignatureSource), new { projetId, dossierId }) ?? string.Empty,
            Url.Action(nameof(StreamDossierSignatureSource), new { projetId, dossierId, download = true }) ?? string.Empty);
    }

    public async Task<IActionResult> StreamDossierSignatureSource(Guid projetId, Guid dossierId, bool download = false)
    {
        var result = await _documentAccess.GetDossierSignatureSourceAsync(projetId, dossierId, User.GetUserIdOrThrow());
        return BuildFileResult(result, download);
    }

    private async Task<IActionResult> BuildPreviewResultAsync(
        DocumentAccessResult result,
        string streamUrl,
        string downloadUrl)
    {
        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (result.IsForbidden)
        {
            return Forbid();
        }

        var model = await _documentPreviewService.BuildPreviewAsync(
            result.RelativePath!,
            result.FileName!,
            result.Title!,
            streamUrl,
            downloadUrl);

        return View("Preview", model);
    }

    private IActionResult BuildFileResult(DocumentAccessResult result, bool download)
    {
        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (result.IsForbidden)
        {
            return Forbid();
        }

        var absolutePath = _fileStorage.GetAbsolutePath(result.RelativePath!);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound();
        }

        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; font-src 'self' https://cdn.jsdelivr.net;";
        Response.Headers["Content-Disposition"] = $"{(download ? "attachment" : "inline")}; filename*=UTF-8''{Uri.EscapeDataString(result.FileName!)}";

        return PhysicalFile(
            absolutePath,
            _documentPreviewService.GetMimeType(result.FileName!),
            enableRangeProcessing: true);
    }
}
