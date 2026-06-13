using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class CharteProjetWorkflowService : ICharteProjetWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public CharteProjetWorkflowService(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<WorkflowResult> ValiderDmAsync(Guid projetId, Guid userId)
    {
        var projet = await _db.Projets
            .Include(p => p.CharteProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            return WorkflowResult.Error("La charte ne peut être validée qu'en phase Analyse.");

        if (!HasCompleteSignedCharte(projet))
            return WorkflowResult.Error("La charte signée complète doit être déposée avant la validation DM.");

        projet.CharteValideeParDM = true;
        projet.DateCharteValideeParDM = DateTime.Now;
        projet.CharteValideeParDMId = userId;
        projet.CommentaireRefusCharteDM = null;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
        {
            projet.CharteValidee = true;
            projet.DateCharteValidee = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CHARTE_DM", "Projet", projet.Id);

        return WorkflowResult.Success("Charte validée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> RejeterDmAsync(Guid projetId, string commentaire)
    {
        var projet = await _db.Projets.FindAsync(projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la charte.");

        projet.CharteValideeParDM = false;
        projet.DateCharteValideeParDM = null;
        projet.CharteValideeParDMId = null;
        projet.CommentaireRefusCharteDM = commentaire.Trim();
        projet.CharteValidee = false;
        projet.DateCharteValidee = null;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("REJET_CHARTE_DM", "Projet", projet.Id,
            new { Commentaire = commentaire });

        return WorkflowResult.Success("Charte rejetée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> ValiderDsiAsync(Guid projetId, Guid userId)
    {
        var projet = await _db.Projets
            .Include(p => p.CharteProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            return WorkflowResult.Error("La charte ne peut être validée qu'en phase Analyse.");

        if (!projet.CharteValideeParDM)
            return WorkflowResult.Error("La charte doit d'abord être validée par le Directeur Métier.");

        if (!HasCompleteSignedCharte(projet))
            return WorkflowResult.Error("La charte signée complète doit être déposée avant la validation DSI.");

        projet.CharteValideeParDSI = true;
        projet.DateCharteValideeParDSI = DateTime.Now;
        projet.CharteValideeParDSIId = userId;
        projet.CommentaireRefusCharteDSI = null;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
        {
            projet.CharteValidee = true;
            projet.DateCharteValidee = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CHARTE_DSI", "Projet", projet.Id);

        return WorkflowResult.Success("Charte validée par la DSI.");
    }

    public async Task<WorkflowResult> RejeterDsiAsync(Guid projetId, string commentaire)
    {
        var projet = await _db.Projets.FindAsync(projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la charte.");

        projet.CharteValideeParDSI = false;
        projet.DateCharteValideeParDSI = null;
        projet.CharteValideeParDSIId = null;
        projet.CommentaireRefusCharteDSI = commentaire.Trim();
        projet.CharteValidee = false;
        projet.DateCharteValidee = null;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("REJET_CHARTE_DSI", "Projet", projet.Id,
            new { Commentaire = commentaire });

        return WorkflowResult.Success("Charte rejetée par la DSI.");
    }

    private static bool HasCompleteSignedCharte(Projet projet)
    {
        var hasSignedLivrable = projet.Livrables.Any(l =>
            !l.EstSupprime &&
            l.TypeLivrable == TypeLivrable.CharteProjetSignee);

        return hasSignedLivrable &&
               projet.CharteProjet?.SignatureSponsor == true &&
               projet.CharteProjet?.SignatureChefProjet == true;
    }
}
