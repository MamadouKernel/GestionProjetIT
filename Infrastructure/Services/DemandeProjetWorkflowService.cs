using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Models;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class DemandeProjetWorkflowService : IDemandeProjetWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ITeamsNotificationService _teams;
    private readonly IEmailService _email;
    private readonly IFileStorageService _fileStorage;
    private readonly IDemandeProjetQueryService _demandeQuery;

    public DemandeProjetWorkflowService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        ITeamsNotificationService teams,
        IEmailService email,
        IFileStorageService fileStorage,
        IDemandeProjetQueryService demandeQuery)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _teams = teams;
        _email = email;
        _fileStorage = fileStorage;
        _demandeQuery = demandeQuery;
    }

    public async Task<WorkflowResult> ValiderDmAsync(
        Guid id, string? commentaire,
        string? titre, string? description, string? objectifs, string? avantagesAttendus,
        Guid currentUserId, bool hasAdminScope, string nomActeur)
    {
        var demande = await _db.DemandesProjets.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        var isDelegue = !hasAdminScope && demande.DirecteurMetierId != currentUserId &&
            await IsActiveDmDelegateAsync(demande.DirecteurMetierId, currentUserId);

        if (!hasAdminScope && !isDelegue && demande.DirecteurMetierId != currentUserId)
            return WorkflowResult.Forbidden();

        if (!hasAdminScope && demande.DemandeurId == currentUserId)
            return WorkflowResult.Error("Vous ne pouvez pas valider votre propre demande.");

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
            demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation.");

        if (string.IsNullOrWhiteSpace(demande.CahierChargesPath))
            return WorkflowResult.Error("Impossible de valider : aucun cahier des charges n'a été déposé.");

        var ancienStatut = demande.StatutDemande;
        var avant = new { demande.Titre, demande.Description, demande.Objectifs, demande.AvantagesAttendus, StatutDemande = ancienStatut.ToString() };

        if (!string.IsNullOrWhiteSpace(titre))       demande.Titre             = titre.Trim();
        if (!string.IsNullOrWhiteSpace(description)) demande.Description       = description.Trim();
        if (!string.IsNullOrWhiteSpace(objectifs))   demande.Objectifs         = objectifs.Trim();
        if (avantagesAttendus != null)               demande.AvantagesAttendus = avantagesAttendus.Trim();

        demande.StatutDemande              = StatutDemande.EnAttenteValidationDSI;
        demande.DateValidationDM           = DateTime.Now;
        demande.CommentaireDirecteurMetier = commentaire ?? string.Empty;
        demande.DateModification           = DateTime.Now;
        demande.ModifiePar                 = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        var apres = new { demande.Titre, demande.Description, demande.Objectifs, demande.AvantagesAttendus, StatutDemande = demande.StatutDemande.ToString(), Commentaire = commentaire };
        var actionLogDm = isDelegue ? "VALIDATION_DM_PAR_DELEGUE" : "VALIDATION_DM";
        await _audit.LogActionAsync(actionLogDm, "DemandeProjet", demande.Id, avant,
            isDelegue ? await AvecTitulaireInitialAsync(apres, demande.DirecteurMetierId) : apres);

        await _teams.EnvoyerValidationDMAsync(demande.Titre ?? string.Empty, nomActeur, true, commentaire, demande.Id);

        var dsi = await _db.Utilisateurs
            .Where(u => !u.EstSupprime)
            .Join(_db.UtilisateurRoles.Where(r => r.Role == RoleUtilisateur.DSI && !r.EstSupprime),
                  u => u.Id, r => r.UtilisateurId, (u, r) => u)
            .FirstOrDefaultAsync();
        if (dsi?.Email != null)
            await _email.EnvoyerValidationDMVersdsIAsync(dsi.Email, demande.Titre ?? string.Empty, nomActeur, commentaire);

        return WorkflowResult.Success("Demande validée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> RejeterDmAsync(
        Guid id, string commentaire, Guid currentUserId, bool hasAdminScope, string nomActeur)
    {
        var demande = await _db.DemandesProjets.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        var isDelegue = !hasAdminScope && demande.DirecteurMetierId != currentUserId &&
            await IsActiveDmDelegateAsync(demande.DirecteurMetierId, currentUserId);

        if (!hasAdminScope && !isDelegue && demande.DirecteurMetierId != currentUserId)
            return WorkflowResult.Forbidden();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le motif du rejet est obligatoire.");

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
            demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation.");

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande              = StatutDemande.RejeteeParDirecteurMetier;
        demande.CommentaireDirecteurMetier = commentaire.Trim();
        demande.DateModification           = DateTime.Now;
        demande.ModifiePar                 = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        var apresRejetDm = new { demande.StatutDemande, Commentaire = commentaire };
        await _audit.LogActionAsync(isDelegue ? "REJET_DM_PAR_DELEGUE" : "REJET_DM", "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut },
            isDelegue ? await AvecTitulaireInitialAsync(apresRejetDm, demande.DirecteurMetierId) : apresRejetDm);

        await _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, nomActeur, "Rejet par Directeur métier", commentaire, demande.Id);
        var demandeur = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
        if (demandeur?.Email != null)
            await _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                demandeur.Email, $"{demandeur.Nom} {demandeur.Prenoms}".Trim(),
                demande.Titre ?? string.Empty, nomActeur, "Rejet par le Directeur Métier", commentaire);

        return WorkflowResult.Success("Demande rejetée.");
    }

    public async Task<WorkflowResult> DemanderCorrectionDmAsync(
        Guid id, string commentaire, Guid currentUserId, bool hasAdminScope, string nomActeur)
    {
        var demande = await _db.DemandesProjets.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        var isDelegue = !hasAdminScope && demande.DirecteurMetierId != currentUserId &&
            await IsActiveDmDelegateAsync(demande.DirecteurMetierId, currentUserId);

        if (!hasAdminScope && !isDelegue && demande.DirecteurMetierId != currentUserId)
            return WorkflowResult.Forbidden();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour demander une correction.");

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
            demande.StatutDemande != StatutDemande.RetourneeAuDirecteurMetierParDSI)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation.");

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande              = StatutDemande.CorrectionDemandeeParDirecteurMetier;
        demande.CommentaireDirecteurMetier = commentaire.Trim();
        demande.DateModification           = DateTime.Now;
        demande.ModifiePar                 = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        var apresCorrectionDm = new { demande.StatutDemande, Commentaire = commentaire };
        await _audit.LogActionAsync(isDelegue ? "CORRECTION_DM_PAR_DELEGUE" : "CORRECTION_DM", "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut },
            isDelegue ? await AvecTitulaireInitialAsync(apresCorrectionDm, demande.DirecteurMetierId) : apresCorrectionDm);

        await _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, nomActeur, "Correction demandée par le Directeur métier", commentaire, demande.Id);
        var demandeur = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
        if (demandeur?.Email != null)
            await _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                demandeur.Email, $"{demandeur.Nom} {demandeur.Prenoms}".Trim(),
                demande.Titre ?? string.Empty, nomActeur, "Correction demandée par le Directeur Métier", commentaire);

        return WorkflowResult.Success("Correction demandée au demandeur.");
    }

    // ── Côté DSI ───────────────────────────────────────────────────────────────
    public async Task<ValiderDsiResult> ValiderDsiAsync(
        Guid id, string? commentaire, Guid? chefProjetId, Guid currentUserId, bool isDelegue, string nomActeur)
    {
        var demande = await _db.DemandesProjets.Include(d => d.Direction).FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return ValiderDsiResult.NotFound();

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            return ValiderDsiResult.Error("Cette demande n'est pas en attente de validation DSI.");

        if (string.IsNullOrWhiteSpace(demande.CahierChargesPath))
            return ValiderDsiResult.Error("Impossible de valider : aucun cahier des charges n'a été déposé.");

        var portefeuilleActif = await _db.PortefeuillesProjets.FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);
        if (portefeuilleActif == null)
            return ValiderDsiResult.Error("Aucun portefeuille actif n'est configure. Creez ou activez un portefeuille avant de valider la demande.");

        var effectiveChefProjetId = chefProjetId;
        if (!effectiveChefProjetId.HasValue)
        {
            var responsableSolutionIT = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .Join(_db.UtilisateurRoles.Where(r => r.Role == RoleUtilisateur.ResponsableSolutionsIT && !r.EstSupprime),
                      u => u.Id, r => r.UtilisateurId, (u, r) => u)
                .OrderBy(u => u.Nom).ThenBy(u => u.Prenoms)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync();
            effectiveChefProjetId = responsableSolutionIT;
        }

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande     = StatutDemande.ValideeParDSI;
        demande.DateValidationDSI = DateTime.Now;
        demande.CommentaireDSI    = commentaire ?? string.Empty;

        var projet = new Projet
        {
            Id                    = Guid.NewGuid(),
            CodeProjet            = $"PROJ-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
            Titre                 = demande.Titre ?? string.Empty,
            Objectif              = demande.Objectifs,
            PortefeuilleProjetId  = portefeuilleActif.Id,
            DemandeProjetId       = demande.Id,
            DirectionId           = demande.DirectionId,
            SponsorId             = demande.DirecteurMetierId,
            ChefProjetId          = effectiveChefProjetId,
            StatutProjet          = StatutProjet.NonDemarre,
            PhaseActuelle         = PhaseProjet.AnalyseClarification,
            EtatProjet            = EtatProjet.Vert,
            PourcentageAvancement = 0,
            BilanCloture          = string.Empty,
            LeconsApprises        = string.Empty,
            DateCreation          = DateTime.Now,
            CreePar               = _currentUser.Matricule ?? string.Empty
        };
        _db.Projets.Add(projet);
        await _db.SaveChangesAsync();

        var actionLog = isDelegue ? "VALIDATION_DSI_PAR_DELEGUE" : "VALIDATION_DSI";
        var apresValidationDsi = new { demande.StatutDemande, Commentaire = commentaire };
        await _audit.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut },
            isDelegue ? await AvecTitulaireInitialDsiAsync(apresValidationDsi, currentUserId) : apresValidationDsi);
        await _audit.LogActionAsync("CreationProjet", "Projet", projet.Id, null,
            new { projet.CodeProjet, projet.Titre, projet.StatutProjet, projet.PhaseActuelle });

        await _teams.EnvoyerValidationDSIAsync(demande.Titre ?? string.Empty, nomActeur, true, commentaire, demande.Id);
        var demandeur = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
        var dm        = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
        await _email.EnvoyerValidationDSIProjetCreeAsync(
            demandeur?.Email ?? string.Empty, dm?.Email, demande.Titre ?? string.Empty, projet.CodeProjet);

        return ValiderDsiResult.Success($"Demande validée et projet {projet.CodeProjet} créé automatiquement.", projet.Id);
    }

    public async Task<WorkflowResult> RejeterDsiAsync(Guid id, string? commentaire, Guid currentUserId, bool isDelegue, string nomActeur)
    {
        var demande = await _db.DemandesProjets.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation DSI.");

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la demande.");

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande    = StatutDemande.RejeteeParDSI;
        demande.CommentaireDSI   = commentaire.Trim();
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        var actionLog = isDelegue ? "REJET_DSI_PAR_DELEGUE" : "REJET_DSI";
        var apresRejetDsi = new { demande.StatutDemande, Commentaire = commentaire };
        await _audit.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut },
            isDelegue ? await AvecTitulaireInitialDsiAsync(apresRejetDsi, currentUserId) : apresRejetDsi);

        await _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, nomActeur, "Rejet par la DSI", commentaire, demande.Id);
        var demandeur = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
        var dm        = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
        await _email.EnvoyerRejetDSIAsync(
            demandeur?.Email ?? string.Empty, dm?.Email, demande.Titre ?? string.Empty, nomActeur, commentaire);

        return WorkflowResult.Success("Demande rejetée par la DSI.");
    }

    public async Task<WorkflowResult> RenvoyerAuDemandeurDsiAsync(Guid id, string? commentaire, Guid currentUserId, bool isDelegue, string nomActeur)
    {
        var demande = await _db.DemandesProjets.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation DSI.");

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour renvoyer la demande au demandeur.");

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande    = StatutDemande.RetourneeAuDemandeurParDSI;
        demande.CommentaireDSI   = commentaire.Trim();
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        var actionLog = isDelegue ? "CORRECTION_DSI_DEMANDEUR_PAR_DELEGUE" : "CORRECTION_DSI_DEMANDEUR";
        var apresCorrectionDsiDemandeur = new { demande.StatutDemande, Commentaire = commentaire };
        await _audit.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut },
            isDelegue ? await AvecTitulaireInitialDsiAsync(apresCorrectionDsiDemandeur, currentUserId) : apresCorrectionDsiDemandeur);

        await _teams.EnvoyerRejetOuRenvoiAsync(demande.Titre ?? string.Empty, nomActeur, "Renvoi au demandeur par la DSI", commentaire, demande.Id);
        var demandeur = await _db.Utilisateurs.FindAsync(demande.DemandeurId);
        if (demandeur?.Email != null)
            await _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                demandeur.Email, $"{demandeur.Nom} {demandeur.Prenoms}".Trim(),
                demande.Titre ?? string.Empty, nomActeur, "Renvoi pour correction par la DSI", commentaire);

        return WorkflowResult.Success("Demande renvoyée au demandeur pour correction.");
    }

    public async Task<WorkflowResult> RenvoyerAuDmDsiAsync(Guid id, string commentaire, Guid currentUserId, bool isDelegue, string nomActeur)
    {
        var demande = await _db.DemandesProjets.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        if (demande.StatutDemande != StatutDemande.EnAttenteValidationDSI)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation DSI.");

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour renvoyer la demande au Directeur Métier.");

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande    = StatutDemande.RetourneeAuDirecteurMetierParDSI;
        demande.CommentaireDSI   = commentaire.Trim();
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        var actionLog = isDelegue ? "CORRECTION_DSI_DM_PAR_DELEGUE" : "CORRECTION_DSI_DM";
        var apresCorrectionDsiDm = new { demande.StatutDemande, Commentaire = commentaire };
        await _audit.LogActionAsync(actionLog, "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut },
            isDelegue ? await AvecTitulaireInitialDsiAsync(apresCorrectionDsiDm, currentUserId) : apresCorrectionDsiDm);

        var dm = await _db.Utilisateurs.FindAsync(demande.DirecteurMetierId);
        if (dm?.Email != null)
            await _email.EnvoyerRejetOuRenvoiAuDemandeurAsync(
                dm.Email, $"{dm.Nom} {dm.Prenoms}".Trim(),
                demande.Titre ?? string.Empty, nomActeur, "Renvoi au Directeur Métier par la DSI", commentaire);

        return WorkflowResult.Success("Demande renvoyée au Directeur Métier.");
    }

    // ── Traçabilité des délégations ─────────────────────────────────────────────
    // Enrichit le log d'audit d'une action réalisée au titre d'une délégation avec
    // le nom du titulaire initial du rôle, conformément à l'exigence de traçabilité
    // ("Validation effectuée par X (délégation de Y)").
    private async Task<object> AvecTitulaireInitialAsync(object valeurs, Guid titulaireInitialId)
    {
        var titulaire = await _db.Utilisateurs.FindAsync(titulaireInitialId);
        var nomTitulaire = titulaire != null ? $"{titulaire.Nom} {titulaire.Prenoms}".Trim() : null;
        return new { TitulaireInitialDuRole = nomTitulaire, Details = valeurs };
    }

    private async Task<object> AvecTitulaireInitialDsiAsync(object valeurs, Guid delegueId)
    {
        var dsiTitulaireId = await _db.DelegationsValidationDSI
            .Where(d => d.DelegueId == delegueId && d.EstActive &&
                        d.DateDebut <= DateTime.Now && d.DateFin >= DateTime.Now && !d.EstSupprime)
            .OrderByDescending(d => d.DateDebut)
            .Select(d => (Guid?)d.DSIId)
            .FirstOrDefaultAsync();

        return dsiTitulaireId.HasValue
            ? await AvecTitulaireInitialAsync(valeurs, dsiTitulaireId.Value)
            : valeurs;
    }

    private async Task<bool> IsActiveDmDelegateAsync(Guid directeurMetierId, Guid delegueId)
    {
        return await _db.DelegationsValidationDM.AnyAsync(d =>
            d.DirecteurMetierId == directeurMetierId &&
            d.DelegueId == delegueId &&
            d.EstActive &&
            d.DateDebut <= DateTime.Now &&
            d.DateFin >= DateTime.Now &&
            !d.EstSupprime);
    }

    // ── Documents / duplication ────────────────────────────────────────────────
    public async Task<WorkflowResult> AjouterDocumentsComplementairesAsync(
        Guid id, List<UploadedFileInput>? documents, Guid currentUserId, bool canManageDemandes)
    {
        var demande = await _db.DemandesProjets.Include(d => d.Annexes).FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return WorkflowResult.NotFound();

        if (demande.DemandeurId != currentUserId && !canManageDemandes)
            return WorkflowResult.Forbidden();

        if (demande.StatutDemande != StatutDemande.CorrectionDemandeeParDirecteurMetier &&
            demande.StatutDemande != StatutDemande.RetourneeAuDemandeurParDSI &&
            demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
            demande.StatutDemande != StatutDemande.EnAttenteValidationDSI &&
            !canManageDemandes)
            return WorkflowResult.Error("Vous ne pouvez ajouter des documents que lorsque la demande est en attente de compléments ou en cours de traitement.");

        if (documents == null || !documents.Any())
            return WorkflowResult.Error("Veuillez sélectionner au moins un document.");

        var documentsAjoutes = 0;
        foreach (var document in documents)
        {
            if (document.Length <= 0) continue;

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
            var maxSize = 10 * 1024 * 1024;
            var path = await _fileStorage.SaveFileAsync(document, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);

            _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
            {
                Id              = Guid.NewGuid(),
                DemandeProjetId = demande.Id,
                NomFichier      = document.FileName,
                CheminRelatif   = path,
                DateDepot       = DateTime.Now,
                DeposeParId     = currentUserId,
                DateCreation    = DateTime.Now,
                CreePar         = _currentUser.Matricule
            });
            documentsAjoutes++;
        }

        if (documentsAjoutes == 0)
            return WorkflowResult.Error("Aucun document valide n'a été ajouté.");

        if (demande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
        {
            demande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
            await _audit.LogActionAsync("COMPLEMENT_INFORMATION_AJOUTE", "DemandeProjet", demande.Id,
                new { AncienStatut = StatutDemande.CorrectionDemandeeParDirecteurMetier, NouveauStatut = demande.StatutDemande, NombreDocuments = documentsAjoutes });
        }
        else if (demande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
        {
            demande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
            await _audit.LogActionAsync("COMPLEMENT_INFORMATION_AJOUTE", "DemandeProjet", demande.Id,
                new { AncienStatut = StatutDemande.RetourneeAuDemandeurParDSI, NouveauStatut = demande.StatutDemande, NombreDocuments = documentsAjoutes });
        }
        else
        {
            await _audit.LogActionAsync("AJOUT_DOCUMENTS_COMPLEMENTAIRES", "DemandeProjet", demande.Id,
                new { NombreDocuments = documentsAjoutes });
        }

        await _db.SaveChangesAsync();
        return WorkflowResult.Success($"{documentsAjoutes} document(s) complémentaire(s) ajouté(s) avec succès. La demande a été remise en validation.");
    }

    public async Task<DuplicationResult> DupliquerDemandeAsync(Guid id, Guid currentUserId, bool canManageDemandes)
    {
        var originale = await _db.DemandesProjets.Include(d => d.Annexes).FirstOrDefaultAsync(d => d.Id == id);
        if (originale == null) return DuplicationResult.NotFound();

        if (originale.DemandeurId != currentUserId && !canManageDemandes)
            return DuplicationResult.Forbidden();

        if (originale.StatutDemande != StatutDemande.RejeteeParDirecteurMetier &&
            originale.StatutDemande != StatutDemande.RejeteeParDSI)
            return DuplicationResult.Error("Seules les demandes refusées peuvent être dupliquées.");

        var nouvelle = new DemandeProjet
        {
            Id                         = Guid.NewGuid(),
            DemandeurId                = originale.DemandeurId,
            DirectionId                = originale.DirectionId,
            DirecteurMetierId          = originale.DirecteurMetierId,
            Titre                      = originale.Titre,
            Description                = originale.Description,
            Contexte                   = originale.Contexte,
            Objectifs                  = originale.Objectifs,
            AvantagesAttendus          = originale.AvantagesAttendus,
            Perimetre                  = originale.Perimetre,
            Urgence                    = originale.Urgence,
            Criticite                  = originale.Criticite,
            DateMiseEnOeuvreSouhaitee  = originale.DateMiseEnOeuvreSouhaitee,
            StatutDemande              = StatutDemande.Brouillon,
            DateSoumission             = DateTime.Now,
            DateCreation               = DateTime.Now,
            CreePar                    = _currentUser.Matricule,
            CahierChargesPath          = string.Empty,
            CommentaireDirecteurMetier = string.Empty,
            CommentaireDSI             = string.Empty
        };
        _db.DemandesProjets.Add(nouvelle);
        await _db.SaveChangesAsync();

        if (originale.Annexes != null && originale.Annexes.Any())
        {
            foreach (var annexe in originale.Annexes)
            {
                _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
                {
                    Id              = Guid.NewGuid(),
                    DemandeProjetId = nouvelle.Id,
                    NomFichier      = annexe.NomFichier,
                    CheminRelatif   = annexe.CheminRelatif,
                    DateDepot       = DateTime.Now,
                    DeposeParId     = currentUserId,
                    DateCreation    = DateTime.Now,
                    CreePar         = _currentUser.Matricule
                });
            }
            await _db.SaveChangesAsync();
        }

        await _audit.LogActionAsync("RELANCE_DEMANDE_PROJET", "DemandeProjet", nouvelle.Id,
            new { DemandeOriginaleId = originale.Id, StatutOriginal = originale.StatutDemande });

        return DuplicationResult.Success("Demande dupliquée avec succès. Vous pouvez maintenant la modifier et la soumettre.", nouvelle.Id);
    }

    // S'assure qu'un portefeuille actif existe (dupliqué temporairement dans
    // DemandeProjetController.Soumission.cs jusqu'à l'extraction de la soumission).
    public async Task<SoumissionResult> SoumettreAsync(Guid id, Guid currentUserId, bool hasAdminScope, bool ignorerDoublons)
    {
        var demande = await _db.DemandesProjets
            .Include(d => d.Direction)
            .Include(d => d.Demandeur)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (demande == null) return SoumissionResult.NotFound();

        if (demande.DemandeurId != currentUserId && !hasAdminScope)
            return SoumissionResult.Forbidden();

        if (demande.StatutDemande != StatutDemande.Brouillon)
            return SoumissionResult.Error("Cette demande ne peut plus etre modifiee.");

        if (string.IsNullOrWhiteSpace(demande.CahierChargesPath))
            return SoumissionResult.Error("Le cahier des charges est obligatoire avant la soumission.");

        if (!ignorerDoublons)
        {
            var doublons = await _demandeQuery.DetecterDoublonsAsync(demande);
            if (doublons != null && doublons.DemandesSimilaires.Any())
                return SoumissionResult.DoublonsDetectes(doublons);
        }

        var ancienStatut = demande.StatutDemande;
        demande.StatutDemande  = StatutDemande.EnAttenteValidationDirecteurMetier;
        demande.DateSoumission = DateTime.Now;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("SOUMISSION_DEMANDE", "DemandeProjet", demande.Id,
            new { StatutDemande = ancienStatut, IgnorerDoublons = ignorerDoublons },
            new { demande.StatutDemande });

        return SoumissionResult.Success("Demande soumise avec succes.");
    }

    // ── Corbeille (réservé AdminIT — gating fait par le contrôleur) ────────────
    // IgnoreQueryFilters() : le filtre global "EstSupprime == false" (voir
    // ApplicationDbContext.AppliquerFiltreSoftDelete) masquerait sinon l'entité
    // ciblée dès qu'elle est déjà supprimée (FindAsync applique aussi ce filtre).
    public async Task<WorkflowResult> SupprimerAsync(Guid id, string nomActeur)
    {
        var demande = await _db.DemandesProjets.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return WorkflowResult.NotFound();
        if (demande.EstSupprime) return WorkflowResult.Error("Cette demande est déjà dans la corbeille.");

        demande.EstSupprime      = true;
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("SUPPRESSION_DEMANDE", "DemandeProjet", demande.Id,
            null, new { demande.Titre, ActeurSuppression = nomActeur });

        return WorkflowResult.Success("Demande envoyée à la corbeille.");
    }

    public async Task<WorkflowResult> RestaurerAsync(Guid id, string nomActeur)
    {
        var demande = await _db.DemandesProjets.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return WorkflowResult.NotFound();
        if (!demande.EstSupprime) return WorkflowResult.Error("Cette demande n'est pas dans la corbeille.");

        demande.EstSupprime      = false;
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("RESTAURATION_DEMANDE", "DemandeProjet", demande.Id,
            null, new { demande.Titre, ActeurRestauration = nomActeur });

        return WorkflowResult.Success("Demande restaurée avec succès.");
    }
}
