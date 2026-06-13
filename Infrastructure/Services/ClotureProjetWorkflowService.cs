using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class ClotureProjetWorkflowService : IClotureProjetWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public ClotureProjetWorkflowService(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<WorkflowResult> DemanderClotureAsync(
        Guid projetId,
        Guid userId,
        string? commentaire,
        DateTime? dateSouhaiteeCloture)
    {
        var projet = await _db.Projets
            .Include(p => p.DemandeProjet)
            .Include(p => p.FicheProjet)
            .FirstOrDefaultAsync(p => p.Id == projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (projet.PhaseActuelle != PhaseProjet.ClotureLeconsApprises || !projet.RecetteValidee || !projet.MepEffectuee)
            return WorkflowResult.Error("Le projet doit être en phase Clôture avec recette validée et MEP effectuée avant de demander la clôture.");

        var livrablesCloture = await _db.LivrablesProjets
            .Where(l => l.ProjetId == projet.Id && l.Phase == PhaseProjet.ClotureLeconsApprises)
            .AnyAsync();

        if (!livrablesCloture)
            return WorkflowResult.Error("Veuillez d'abord déposer les livrables de clôture avant de soumettre la demande.");

        if (string.IsNullOrWhiteSpace(projet.BilanPerimetre) ||
            string.IsNullOrWhiteSpace(projet.BilanPlanning) ||
            string.IsNullOrWhiteSpace(projet.BilanBudget) ||
            string.IsNullOrWhiteSpace(projet.LeconsReussites) ||
            string.IsNullOrWhiteSpace(projet.LeconsRecommandations))
        {
            return WorkflowResult.Error("Veuillez renseigner le bilan de clôture et les leçons apprises avant de soumettre la demande.");
        }

        if (projet.FicheProjet == null ||
            !projet.FicheProjet.TransfertRunDocumentation ||
            !projet.FicheProjet.TransfertRunSupportInforme ||
            !projet.FicheProjet.TransfertRunExploitationPrete)
        {
            return WorkflowResult.Error("Le transfert RUN doit être entièrement renseigné avant la soumission de la clôture.");
        }

        var demande = new DemandeClotureProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            DemandeParId = userId,
            DateDemande = DateTime.Now,
            DateSouhaiteeCloture = dateSouhaiteeCloture,
            StatutValidationDemandeur = StatutValidationCloture.EnAttente,
            StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente,
            StatutValidationDSI = StatutValidationCloture.EnAttente,
            EstTerminee = false,
            DateCreation = DateTime.Now,
            CreePar = _currentUserService.Matricule,
            CommentaireInitiateur = commentaire ?? string.Empty,
            CommentaireDemandeur = string.Empty,
            CommentaireDirecteurMetier = string.Empty,
            CommentaireDSI = string.Empty
        };

        _db.DemandesClotureProjets.Add(demande);

        projet.StatutProjet = StatutProjet.ClotureEnCours;
        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("DEMANDE_CLOTURE", "Projet", projet.Id);
        await _auditService.LogActionAsync("SOUMISSION_DEMANDE_CLOTURE", "DemandeClotureProjet", demande.Id,
            new { ProjetId = projet.Id, DemandeParId = userId },
            new { StatutValidationDemandeur = StatutValidationCloture.EnAttente, StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente, StatutValidationDSI = StatutValidationCloture.EnAttente });

        return WorkflowResult.Success("Demande de clôture créée.");
    }

    public async Task<WorkflowResult> ValiderClotureDemandeurAsync(Guid demandeClotureId)
    {
        var demande = await _db.DemandesClotureProjets
            .Include(d => d.Projet)
                .ThenInclude(p => p.DemandeProjet)
            .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

        if (demande == null)
            return WorkflowResult.NotFound();

        if (demande.StatutValidationDemandeur != StatutValidationCloture.EnAttente)
            return WorkflowResult.Error("Cette validation a déjà été effectuée.");

        demande.StatutValidationDemandeur = StatutValidationCloture.Validee;
        demande.DateValidationDemandeur = DateTime.Now;

        await VerifierClotureComplete(demande);

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CLOTURE_DEMANDEUR", "DemandeClotureProjet", demande.Id);

        return WorkflowResult.Success("Clôture validée par le demandeur.");
    }

    public async Task<WorkflowResult> ValiderClotureDmAsync(Guid demandeClotureId)
    {
        var demande = await _db.DemandesClotureProjets
            .Include(d => d.Projet)
            .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

        if (demande == null)
            return WorkflowResult.NotFound();

        if (demande.StatutValidationDirecteurMetier != StatutValidationCloture.EnAttente)
            return WorkflowResult.Error("Cette validation a déjà été effectuée.");

        demande.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
        demande.DateValidationDirecteurMetier = DateTime.Now;
        demande.DateModification = DateTime.Now;
        demande.ModifiePar = _currentUserService.Matricule;

        await VerifierClotureComplete(demande);

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CLOTURE_DM", "DemandeClotureProjet", demande.Id,
            new { Decision = "Validee", Commentaire = demande.CommentaireDirecteurMetier });

        return WorkflowResult.Success("Clôture validée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> RejeterClotureDmAsync(Guid demandeClotureId, string commentaire)
    {
        var demande = await _db.DemandesClotureProjets
            .Include(d => d.Projet)
            .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

        if (demande == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la clôture.");

        if (demande.StatutValidationDirecteurMetier != StatutValidationCloture.EnAttente)
            return WorkflowResult.Error("Cette validation a déjà été effectuée.");

        demande.StatutValidationDirecteurMetier = StatutValidationCloture.Rejetee;
        demande.DateValidationDirecteurMetier = DateTime.Now;
        demande.CommentaireDirecteurMetier = commentaire.Trim();
        demande.DateModification = DateTime.Now;
        demande.ModifiePar = _currentUserService.Matricule;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("REJET_CLOTURE_DM", "DemandeClotureProjet", demande.Id,
            new { Commentaire = commentaire, Decision = "Rejetee" });

        return WorkflowResult.Success("Clôture rejetée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> ValiderClotureDsiAsync(Guid demandeClotureId)
    {
        var demande = await _db.DemandesClotureProjets
            .Include(d => d.Projet)
            .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

        if (demande == null)
            return WorkflowResult.NotFound();

        if (demande.StatutValidationDSI != StatutValidationCloture.EnAttente)
            return WorkflowResult.Error("Cette validation a déjà été effectuée.");

        demande.StatutValidationDSI = StatutValidationCloture.Validee;
        demande.DateValidationDSI = DateTime.Now;

        await VerifierClotureComplete(demande);

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CLOTURE_DSI", "DemandeClotureProjet", demande.Id);

        return WorkflowResult.Success("Clôture validée par la DSI.");
    }

    public async Task<WorkflowResult> RejeterClotureDsiAsync(Guid demandeClotureId, string commentaire)
    {
        var demande = await _db.DemandesClotureProjets
            .Include(d => d.Projet)
            .FirstOrDefaultAsync(d => d.Id == demandeClotureId);

        if (demande == null)
            return WorkflowResult.NotFound();

        if (demande.StatutValidationDSI != StatutValidationCloture.EnAttente)
            return WorkflowResult.Error("Cette validation a déjà été effectuée.");

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la clôture.");

        demande.StatutValidationDSI = StatutValidationCloture.Rejetee;
        demande.CommentaireDSI = commentaire.Trim();
        demande.DateModification = DateTime.Now;
        demande.ModifiePar = _currentUserService.Matricule;

        demande.StatutValidationDemandeur = StatutValidationCloture.EnAttente;
        demande.StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente;
        demande.DateValidationDemandeur = null;
        demande.DateValidationDirecteurMetier = null;
        demande.CommentaireDemandeur = string.Empty;
        demande.CommentaireDirecteurMetier = string.Empty;

        if (demande.Projet.PhaseActuelle == PhaseProjet.ClotureLeconsApprises)
        {
            demande.Projet.PhaseActuelle = PhaseProjet.UatMep;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("REJET_CLOTURE_DSI", "DemandeClotureProjet", demande.Id,
            new { Commentaire = commentaire });

        return WorkflowResult.Success("Clôture rejetée. Le projet est retourné en attente de clôture.");
    }

    public async Task<WorkflowResult> AjouterCommentaireTechniqueAsync(
        Guid projetId,
        Guid userId,
        string? commentaireTechnique)
    {
        var projet = await _db.Projets.FindAsync(projetId);
        if (projet == null)
            return WorkflowResult.NotFound();

        projet.CommentaireTechnique = commentaireTechnique?.Trim() ?? string.Empty;
        projet.DateDernierCommentaireTechnique = DateTime.Now;
        projet.DernierCommentaireTechniqueParId = userId;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("AJOUT_COMMENTAIRE_TECHNIQUE", "Projet", projet.Id,
            null,
            new { CommentaireTechnique = commentaireTechnique });

        return WorkflowResult.Success("Commentaire technique enregistré avec succès.");
    }

    public async Task<WorkflowResult> ForcerStatutProjetAsync(Guid projetId, string actionType, string commentaire)
    {
        var projet = await _db.Projets.FindAsync(projetId);
        if (projet == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("L'action et le commentaire sont obligatoires.");

        var ancienStatut = projet.StatutProjet;
        var anciennePhase = projet.PhaseActuelle;

        if (actionType == "Cloture")
        {
            projet.StatutProjet = StatutProjet.Cloture;
            projet.DateFinReelle = DateTime.Now;
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            await CloturerDelegationsChefProjetActivesAsync(projet.Id);

            await _auditService.LogActionAsync("FORCER_CLOTURE_PROJET", "Projet", projet.Id,
                new { AncienStatut = ancienStatut, AnciennePhase = anciennePhase, Commentaire = commentaire },
                new { NouveauStatut = projet.StatutProjet, NouvellePhase = projet.PhaseActuelle });
        }
        else if (actionType == "Abandon")
        {
            projet.StatutProjet = StatutProjet.Annule;
            projet.DateFinReelle = DateTime.Now;

            await CloturerDelegationsChefProjetActivesAsync(projet.Id);

            await _auditService.LogActionAsync("FORCER_ABANDON_PROJET", "Projet", projet.Id,
                new { AncienStatut = ancienStatut, AnciennePhase = anciennePhase, Commentaire = commentaire },
                new { NouveauStatut = projet.StatutProjet });
        }
        else
        {
            return WorkflowResult.Error("Action invalide. Veuillez choisir Clôture ou Abandon.");
        }

        await _db.SaveChangesAsync();

        return WorkflowResult.Success($"Projet {actionType.ToLower()} avec succès.");
    }

    private async Task VerifierClotureComplete(DemandeClotureProjet demande)
    {
        if (demande.Projet.FicheProjet == null)
        {
            await _db.Entry(demande.Projet)
                .Reference(p => p.FicheProjet)
                .LoadAsync();
        }

        var validationComplete =
            demande.StatutValidationDemandeur == StatutValidationCloture.Validee &&
            demande.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
            demande.StatutValidationDSI == StatutValidationCloture.Validee;

        if (validationComplete && !demande.EstTerminee)
        {
            demande.EstTerminee = true;
            demande.DateClotureFinale = DateTime.Now;

            var projet = demande.Projet;
            var statutFinal = demande.Projet.FicheProjet?.StatutFinalCloture?.Trim();
            projet.StatutProjet = string.Equals(statutFinal, "Abandonné", StringComparison.OrdinalIgnoreCase)
                ? StatutProjet.Annule
                : StatutProjet.Cloture;
            projet.DateFinReelle = DateTime.Now;
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            await CloturerDelegationsChefProjetActivesAsync(projet.Id);

            await _auditService.LogActionAsync("CLOTURE_PROJET", "Projet", projet.Id);
        }
    }

    private async Task CloturerDelegationsChefProjetActivesAsync(Guid projetId)
    {
        var delegationsActives = await _db.DelegationsChefProjet
            .Where(d => d.ProjetId == projetId &&
                        d.EstActive &&
                        d.DateFin == null &&
                        !d.EstSupprime)
            .ToListAsync();

        foreach (var delegation in delegationsActives)
        {
            delegation.DateFin = DateTime.Now;
            delegation.EstActive = false;
            delegation.DateModification = DateTime.Now;
            delegation.ModifiePar = _currentUserService.Matricule;
        }
    }
}
