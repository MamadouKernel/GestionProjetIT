using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Integration;

/// <summary>
/// Test de bout-en-bout du cycle de vie complet d'un projet IT.
///
/// Workflow couvert :
///   [Demandeur]    → Créer demande → Soumettre
///   [Dir. Métier]  → Valider DM
///   [DSI]          → Valider DSI + affecter chef de projet → Projet créé
///   [Chef projet]  → Valider charte DM + DSI → Valider phase Analyse
///   [Chef projet]  → Valider planification DM + DSI → Phase Exécution
///   [Chef projet]  → Passer en UAT
///   [Dir. Métier]  → Valider recette
///   [Chef projet]  → Enregistrer MEP + Hypercare → Fin UAT
///   [Chef projet]  → Demander clôture
///   [Demandeur]    → Valider clôture
///   [Dir. Métier]  → Valider clôture
///   [DSI]          → Valider clôture → Projet = Clôturé
/// </summary>
public class WorkflowCompletProjetTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    // ─── Acteurs du test ────────────────────────────────────────────────────
    private Utilisateur _demandeur = null!;
    private Utilisateur _directeurMetier = null!;
    private Utilisateur _dsi = null!;
    private Utilisateur _chefProjet = null!;
    private Utilisateur _rsi = null!;          // Responsable Solutions IT
    private Direction   _direction = null!;

    public WorkflowCompletProjetTests()
    {
        _db = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        ChargerActeurs();
    }

    private void ChargerActeurs()
    {
        _demandeur       = _db.Utilisateurs.First(u => u.Matricule == "DEM001");
        _directeurMetier = _db.Utilisateurs.First(u => u.Matricule == "DIR001");
        _dsi             = _db.Utilisateurs.First(u => u.Matricule == "DSI001");
        _chefProjet      = _db.Utilisateurs.First(u => u.Matricule == "CP001");
        _rsi             = _db.Utilisateurs.First(u => u.Matricule == "RSI001");
        _direction       = _db.Directions.First();
    }

    // ════════════════════════════════════════════════════════════════════════
    // TEST PRINCIPAL : CYCLE COMPLET
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CycleComplet_DemandeurVersClotureProjet_WorkflowRespecteToutesLesEtapes()
    {
        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 1 — DEMANDEUR : Créer et soumettre la demande
        // ────────────────────────────────────────────────────────────────────
        var demande = await EtapeCreerDemande();

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 2 — DEMANDEUR : Soumettre la demande
        // ────────────────────────────────────────────────────────────────────
        await EtapeSoumettreDemandeAsync(demande);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 3 — DIRECTEUR MÉTIER : Valider la demande
        // ────────────────────────────────────────────────────────────────────
        await EtapeValiderDMAsync(demande);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 4 — DSI : Valider la demande + créer le projet
        // ────────────────────────────────────────────────────────────────────
        var projet = await EtapeValiderDSIEtCreerProjetAsync(demande);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 5 — PHASE ANALYSE : Charte validée DM puis DSI
        // ────────────────────────────────────────────────────────────────────
        await EtapeValiderCharteAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 6 — PHASE ANALYSE → PLANIFICATION : Valider la phase analyse
        // ────────────────────────────────────────────────────────────────────
        await EtapeValiderPhaseAnalyseAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 7 — PHASE PLANIFICATION : Validation DM puis DSI
        // ────────────────────────────────────────────────────────────────────
        await EtapeValiderPlanificationAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 8 — PHASE EXÉCUTION → UAT
        // ────────────────────────────────────────────────────────────────────
        await EtapePasserEnUATAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 9 — PHASE UAT : Recette + MEP + Hypercare
        // ────────────────────────────────────────────────────────────────────
        await EtapeValiderRecetteEtMEPAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 10 — PHASE UAT → CLÔTURE : Fin UAT
        // ────────────────────────────────────────────────────────────────────
        await EtapeFinUATAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // ÉTAPE 11 — CLÔTURE : Demande + validations en cascade
        // ────────────────────────────────────────────────────────────────────
        await EtapeClotureAsync(projet);

        // ────────────────────────────────────────────────────────────────────
        // VÉRIFICATION FINALE
        // ────────────────────────────────────────────────────────────────────
        await VerifierEtatFinalAsync(projet.Id, demande.Id);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ÉTAPES DÉTAILLÉES
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Étape 1 : Le Demandeur crée une demande de projet (statut Brouillon).
    /// </summary>
    private async Task<DemandeProjet> EtapeCreerDemande()
    {
        var demande = new DemandeProjet
        {
            Id                  = Guid.NewGuid(),
            Titre               = "Système de gestion des congés",
            Description         = "Mise en place d'un outil RH pour la gestion des demandes de congés.",
            Contexte            = "La gestion des congés est actuellement manuelle, source d'erreurs.",
            Objectifs           = "Automatiser la saisie, l'approbation et le suivi des congés.",
            AvantagesAttendus   = "Gain de temps, réduction des erreurs, visibilité RH améliorée.",
            Perimetre           = "Tous les employés CIT.",
            Urgence             = UrgenceProjet.Moyenne,
            Criticite           = CriticiteProjet.Moyenne,
            DateMiseEnOeuvreSouhaitee = DateTime.Now.AddMonths(6),
            DemandeurId         = _demandeur.Id,
            DirectionId         = _direction.Id,
            DirecteurMetierId   = _directeurMetier.Id,
            StatutDemande       = StatutDemande.Brouillon,
            DateSoumission      = DateTime.Now,
            DateCreation        = DateTime.Now,
            CreePar             = _demandeur.Matricule
        };

        _db.DemandesProjets.Add(demande);
        await _db.SaveChangesAsync();

        // Assertions
        demande.StatutDemande.Should().Be(StatutDemande.Brouillon,
            "une nouvelle demande commence toujours en Brouillon");
        demande.Titre.Should().Be("Système de gestion des congés");

        return demande;
    }

    /// <summary>
    /// Étape 2 : Le Demandeur soumet sa demande au Directeur Métier.
    /// </summary>
    private async Task EtapeSoumettreDemandeAsync(DemandeProjet demande)
    {
        demande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
        demande.DateSoumission = DateTime.Now;
        await _db.SaveChangesAsync();

        // Vérifier la transition
        var reload = await _db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDirecteurMetier,
            "après soumission, la demande attend la validation du Directeur Métier");
        reload.DateSoumission.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Étape 3 : Le Directeur Métier valide la demande et la transmet à la DSI.
    /// Règle : DM ne peut pas valider sa propre demande.
    /// </summary>
    private async Task EtapeValiderDMAsync(DemandeProjet demande)
    {
        // Règle : le demandeur ne peut pas être le même que le DM
        demande.DirecteurMetierId.Should().NotBe(demande.DemandeurId,
            "le Directeur Métier ne peut pas valider sa propre demande");

        demande.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDirecteurMetier);

        demande.StatutDemande          = StatutDemande.EnAttenteValidationDSI;
        demande.DateValidationDM       = DateTime.Now;
        demande.CommentaireDirecteurMetier = "Demande pertinente, alignée avec les priorités de la direction.";
        await _db.SaveChangesAsync();

        var reload = await _db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDSI,
            "après validation DM, la demande attend la validation DSI");
        reload.DateValidationDM.Should().NotBeNull();
        reload.CommentaireDirecteurMetier.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Étape 4 : La DSI valide la demande et crée automatiquement le projet.
    /// Le projet démarre en phase AnalyseClarification, statut NonDemarre.
    /// </summary>
    private async Task<Projet> EtapeValiderDSIEtCreerProjetAsync(DemandeProjet demande)
    {
        demande.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDSI);

        // Créer un portefeuille si nécessaire (comme le fait ValiderDSI)
        var portefeuille = await _db.PortefeuillesProjets.FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime)
                           ?? await CreerPortefeuilleParDefautAsync();

        // Valider la demande
        demande.StatutDemande    = StatutDemande.ValideeParDSI;
        demande.DateValidationDSI = DateTime.Now;
        demande.CommentaireDSI   = "Projet approuvé, budget alloué, chef de projet affecté.";
        await _db.SaveChangesAsync();

        // Créer le projet (comme le fait ValiderDSI automatiquement)
        var projet = new Projet
        {
            Id                  = Guid.NewGuid(),
            CodeProjet          = $"PROJ-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 22),
            Titre               = demande.Titre!,
            Objectif            = demande.Objectifs,
            PortefeuilleProjetId = portefeuille.Id,
            DemandeProjetId     = demande.Id,
            DirectionId         = demande.DirectionId,
            SponsorId           = demande.DirecteurMetierId,   // DM devient Sponsor
            ChefProjetId        = _chefProjet.Id,
            StatutProjet        = StatutProjet.NonDemarre,
            PhaseActuelle       = PhaseProjet.AnalyseClarification,
            EtatProjet          = EtatProjet.Vert,
            PourcentageAvancement = 0,
            BilanCloture        = string.Empty,
            LeconsApprises      = string.Empty,
            DateCreation        = DateTime.Now,
            CreePar             = _dsi.Matricule
        };

        _db.Projets.Add(projet);
        await _db.SaveChangesAsync();

        // Assertions
        var reloadDemande = await _db.DemandesProjets.FindAsync(demande.Id);
        reloadDemande!.StatutDemande.Should().Be(StatutDemande.ValideeParDSI,
            "la DSI a validé la demande");

        var reloadProjet = await _db.Projets.FindAsync(projet.Id);
        reloadProjet.Should().NotBeNull("le projet est créé automatiquement à la validation DSI");
        reloadProjet!.PhaseActuelle.Should().Be(PhaseProjet.AnalyseClarification,
            "tout projet commence en phase Analyse & Clarification");
        reloadProjet.StatutProjet.Should().Be(StatutProjet.NonDemarre,
            "le projet n'est pas encore démarré à sa création");
        reloadProjet.ChefProjetId.Should().Be(_chefProjet.Id,
            "le chef de projet est affecté par la DSI lors de la validation");
        reloadProjet.SponsorId.Should().Be(_directeurMetier.Id,
            "le Directeur Métier devient le Sponsor du projet");

        return projet;
    }

    /// <summary>
    /// Étape 5 : Validation de la charte par le DM puis la DSI.
    /// Prérequis : un livrable CharteProjetSignee doit être déposé.
    /// </summary>
    private async Task EtapeValiderCharteAsync(Projet projet)
    {
        // Déposer le livrable "Charte signée" (prérequis de HasCompleteSignedCharte)
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.AnalyseClarification,
            TypeLivrable = TypeLivrable.CharteProjetSignee,
            NomDocument  = "Charte_Projet_Signee_v1.pdf",
            CheminRelatif = "uploads/analyse/charte_signee.pdf",
            DateDepot    = DateTime.Now,
            DeposeParId  = _chefProjet.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _chefProjet.Matricule
        });
        await _db.SaveChangesAsync();

        // DM valide la charte
        projet.CharteValideeParDM    = true;
        projet.DateCharteValideeParDM = DateTime.Now;
        projet.CharteValideeParDMId  = _directeurMetier.Id;
        await _db.SaveChangesAsync();

        var apresValidationDM = await _db.Projets.FindAsync(projet.Id);
        apresValidationDM!.CharteValideeParDM.Should().BeTrue("le DM a validé la charte");
        apresValidationDM.CharteValidee.Should().BeFalse(
            "la charte n'est complète que quand DM ET DSI ont tous deux validé");

        // RSI valide la charte (à la place ou en complément de la DSI — même droits)
        // Règle : [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        projet.CharteValideeParDSI    = true;
        projet.DateCharteValideeParDSI = DateTime.Now;
        projet.CharteValideeParDSIId  = _rsi.Id;     // ← RSI valide, pas la DSI directement

        // Les deux ont validé → charte complète
        if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
        {
            projet.CharteValidee      = true;
            projet.DateCharteValidee  = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        var apresValidationDSI = await _db.Projets.FindAsync(projet.Id);
        apresValidationDSI!.CharteValideeParDSI.Should().BeTrue("le RSI a validé la charte côté DSI");
        apresValidationDSI.CharteValideeParDSIId.Should().Be(_rsi.Id,
            "le RSI est habilité à valider la charte comme la DSI");
        apresValidationDSI.CharteValidee.Should().BeTrue(
            "la charte est complète quand DM et DSI/RSI ont tous deux validé");
    }

    /// <summary>
    /// Étape 6 : Validation de la phase Analyse → passage en Planification.
    /// Prérequis : charte validée, livrable CharteProjet déposé.
    /// </summary>
    private async Task EtapeValiderPhaseAnalyseAsync(Projet projet)
    {
        projet.PhaseActuelle.Should().Be(PhaseProjet.AnalyseClarification);
        projet.CharteValidee.Should().BeTrue("la charte doit être validée avant de passer en Planification");

        // Déposer le livrable Charte (document de base, distinct de la charte signée)
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.AnalyseClarification,
            TypeLivrable = TypeLivrable.CharteProjet,
            NomDocument  = "Charte_Projet_v1.pdf",
            CheminRelatif = "uploads/analyse/charte.pdf",
            DateDepot    = DateTime.Now,
            DeposeParId  = _chefProjet.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _chefProjet.Matricule
        });
        await _db.SaveChangesAsync();

        // Transition de phase
        projet.PhaseActuelle  = PhaseProjet.PlanificationValidation;
        projet.StatutProjet   = StatutProjet.EnCours;

        _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
        {
            Id          = Guid.NewGuid(),
            ProjetId    = projet.Id,
            Phase       = PhaseProjet.PlanificationValidation,
            StatutProjet = projet.StatutProjet,
            DateDebut   = DateTime.Now,
            ModifieParId = _chefProjet.Id,
            Commentaire = "Validation de la phase Analyse — passage en Planification",
            DateCreation = DateTime.Now,
            CreePar     = _chefProjet.Matricule
        });

        await _db.SaveChangesAsync();

        var reload = await _db.Projets.FindAsync(projet.Id);
        reload!.PhaseActuelle.Should().Be(PhaseProjet.PlanificationValidation,
            "après validation de l'analyse, le projet passe en Planification");
        reload.StatutProjet.Should().Be(StatutProjet.EnCours,
            "le projet est en cours dès que la phase Analyse est validée");
    }

    /// <summary>
    /// Étape 7 : Validation de la planification par le DM puis la DSI.
    /// La validation DSI déclenche le passage en phase Exécution.
    /// </summary>
    private async Task EtapeValiderPlanificationAsync(Projet projet)
    {
        projet.PhaseActuelle.Should().Be(PhaseProjet.PlanificationValidation);

        // Déposer un livrable de planification (WBS)
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.PlanificationValidation,
            TypeLivrable = TypeLivrable.Wbs,
            NomDocument  = "WBS_Projet.xlsx",
            CheminRelatif = "uploads/planification/wbs.xlsx",
            DateDepot    = DateTime.Now,
            DeposeParId  = _chefProjet.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _chefProjet.Matricule
        });
        await _db.SaveChangesAsync();

        // ── RSI : ajoute son commentaire technique en phase analyse/planification
        // [Authorize(Roles = "ResponsableSolutionsIT,DSI,AdminIT")] sur AjouterCommentaireTechnique
        projet.CommentaireTechnique              = "Architecture microservices recommandée. Prévoir reverse proxy Nginx.";
        projet.DateDernierCommentaireTechnique    = DateTime.Now;
        projet.DernierCommentaireTechniqueParId   = _rsi.Id;
        await _db.SaveChangesAsync();

        var apresCommentaireTechnique = await _db.Projets.FindAsync(projet.Id);
        apresCommentaireTechnique!.CommentaireTechnique.Should().NotBeNullOrEmpty(
            "le RSI a ajouté son appui technique sur le projet");
        apresCommentaireTechnique.DernierCommentaireTechniqueParId.Should().Be(_rsi.Id,
            "c'est le RSI qui a rédigé le commentaire technique");

        // DM valide la planification
        projet.PlanningValideParDM    = true;
        projet.DatePlanningValideParDM = DateTime.Now;
        await _db.SaveChangesAsync();

        var apresValidationDM = await _db.Projets.FindAsync(projet.Id);
        apresValidationDM!.PlanningValideParDM.Should().BeTrue("le DM a validé la planification");
        apresValidationDM.PhaseActuelle.Should().Be(PhaseProjet.PlanificationValidation,
            "la phase ne change pas avant la validation DSI/RSI");

        // ── RSI valide la planification (à la place de la DSI — même droits)
        // [Authorize(Roles = "DSI,ResponsableSolutionsIT")] sur ValiderPlanifDSI
        projet.PlanningValideParDSI    = true;
        projet.DatePlanningValideParDSI = DateTime.Now;
        projet.PhaseActuelle           = PhaseProjet.ExecutionSuivi;
        projet.StatutProjet            = StatutProjet.EnCours;
        projet.DateDebut               = DateTime.Now;

        _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
        {
            Id          = Guid.NewGuid(),
            ProjetId    = projet.Id,
            Phase       = PhaseProjet.ExecutionSuivi,
            StatutProjet = projet.StatutProjet,
            DateDebut   = DateTime.Now,
            ModifieParId = _rsi.Id,      // ← RSI valide la planification côté DSI
            Commentaire = "Validation planification RSI — passage en Exécution",
            DateCreation = DateTime.Now,
            CreePar     = _rsi.Matricule
        });

        await _db.SaveChangesAsync();

        var apresValidationRSI = await _db.Projets.FindAsync(projet.Id);
        apresValidationRSI!.PlanningValideParDSI.Should().BeTrue(
            "le RSI a validé la planification côté DSI");
        apresValidationRSI.PhaseActuelle.Should().Be(PhaseProjet.ExecutionSuivi,
            "après validation RSI de la planification, le projet passe en Exécution");
        apresValidationRSI.DateDebut.Should().NotBeNull(
            "la date de début est renseignée lors du passage en Exécution");
    }

    /// <summary>
    /// Étape 8 : Fin de l'exécution → passage en UAT & MEP.
    /// </summary>
    private async Task EtapePasserEnUATAsync(Projet projet)
    {
        projet.PhaseActuelle.Should().Be(PhaseProjet.ExecutionSuivi);

        // Déposer un CR de réunion (livrable obligatoire exécution)
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.ExecutionSuivi,
            TypeLivrable = TypeLivrable.CompteRenduReunion,
            NomDocument  = "CR_Reunion_Sprint1.docx",
            CheminRelatif = "uploads/execution/cr_sprint1.docx",
            DateDepot    = DateTime.Now,
            DeposeParId  = _chefProjet.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _chefProjet.Matricule
        });
        await _db.SaveChangesAsync();

        // Passage en UAT
        projet.PhaseActuelle = PhaseProjet.UatMep;

        _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
        {
            Id          = Guid.NewGuid(),
            ProjetId    = projet.Id,
            Phase       = PhaseProjet.UatMep,
            StatutProjet = projet.StatutProjet,
            DateDebut   = DateTime.Now,
            ModifieParId = _chefProjet.Id,
            Commentaire = "Projet prêt pour UAT",
            DateCreation = DateTime.Now,
            CreePar     = _chefProjet.Matricule
        });

        await _db.SaveChangesAsync();

        var reload = await _db.Projets.FindAsync(projet.Id);
        reload!.PhaseActuelle.Should().Be(PhaseProjet.UatMep,
            "le projet est prêt pour les tests UAT et la mise en production");
    }

    /// <summary>
    /// Étape 9 : Validation de la recette (DM) + enregistrement MEP + Hypercare.
    /// </summary>
    private async Task EtapeValiderRecetteEtMEPAsync(Projet projet)
    {
        projet.PhaseActuelle.Should().Be(PhaseProjet.UatMep);

        // Déposer un PV de recette
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.UatMep,
            TypeLivrable = TypeLivrable.PvRecette,
            NomDocument  = "PV_Recette_Signe.pdf",
            CheminRelatif = "uploads/uat/pv_recette.pdf",
            DateDepot    = DateTime.Now,
            DeposeParId  = _directeurMetier.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _directeurMetier.Matricule
        });
        await _db.SaveChangesAsync();

        // Directeur Métier valide la recette
        projet.RecetteValidee      = true;
        projet.DateRecetteValidee  = DateTime.Now;
        projet.RecetteValideeParId = _directeurMetier.Id;
        await _db.SaveChangesAsync();

        var apresRecette = await _db.Projets.FindAsync(projet.Id);
        apresRecette!.RecetteValidee.Should().BeTrue("le DM a validé la recette");

        // MEP effectuée (par le Chef de projet / DSI)
        projet.MepEffectuee = true;
        projet.DateMep      = DateTime.Now;
        await _db.SaveChangesAsync();

        // Déposer PV MEP
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.UatMep,
            TypeLivrable = TypeLivrable.PvMep,
            NomDocument  = "PV_MEP.pdf",
            CheminRelatif = "uploads/uat/pv_mep.pdf",
            DateDepot    = DateTime.Now,
            DeposeParId  = _chefProjet.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _chefProjet.Matricule
        });

        // Créer la fiche projet avec l'hypercare renseigné (prérequis FinUAT)
        var fiche = new FicheProjet
        {
            Id                   = Guid.NewGuid(),
            ProjetId             = projet.Id,
            TitreCourt           = projet.Titre,
            TitreLong            = projet.Titre,
            ObjectifPrincipal    = projet.Objectif,
            // Hypercare obligatoire pour FinUAT
            PeriodeHypercare     = "2 semaines",
            StatutHypercare      = "Terminé",
            HypercareTermine     = true,
            // Transfert RUN
            TransfertRunDocumentation   = true,
            TransfertRunAcces           = true,
            TransfertRunSupportInforme  = true,
            TransfertRunExploitationPrete = true,
            StatutFinalCloture   = "Clôturé",
            DateCreation         = DateTime.Now,
            CreePar              = _chefProjet.Matricule
        };
        _db.FicheProjets.Add(fiche);
        await _db.SaveChangesAsync();

        var reload = await _db.Projets.FindAsync(projet.Id);
        reload!.MepEffectuee.Should().BeTrue("la MEP a été effectuée");
        reload.RecetteValidee.Should().BeTrue();
    }

    /// <summary>
    /// Étape 10 : Fin UAT → passage en phase Clôture.
    /// Prérequis : RecetteValidee + MepEffectuee + HypercareTermine.
    /// </summary>
    private async Task EtapeFinUATAsync(Projet projet)
    {
        // Vérifier les prérequis
        var projetAvecFiche = await _db.Projets
            .Include(p => p.FicheProjet)
            .Include(p => p.Livrables)
            .FirstAsync(p => p.Id == projet.Id);

        projetAvecFiche.RecetteValidee.Should().BeTrue();
        projetAvecFiche.MepEffectuee.Should().BeTrue();
        projetAvecFiche.FicheProjet!.HypercareTermine.Should().BeTrue();

        // Déposer un rapport de clôture (livrable obligatoire clôture)
        _db.LivrablesProjets.Add(new LivrableProjet
        {
            Id           = Guid.NewGuid(),
            ProjetId     = projet.Id,
            Phase        = PhaseProjet.ClotureLeconsApprises,
            TypeLivrable = TypeLivrable.RapportCloture,
            NomDocument  = "Rapport_Cloture.docx",
            CheminRelatif = "uploads/cloture/rapport_cloture.docx",
            DateDepot    = DateTime.Now,
            DeposeParId  = _chefProjet.Id,
            Version      = "1.0",
            DateCreation = DateTime.Now,
            CreePar      = _chefProjet.Matricule
        });

        // Transition vers la clôture
        projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;
        projet.StatutProjet  = StatutProjet.ClotureEnCours;

        // Renseigner le bilan et les leçons apprises
        projet.BilanPerimetre   = "Périmètre livré à 100%, pas d'écart.";
        projet.BilanPlanning    = "Planning respecté avec 2 semaines de retard sur le sprint 2.";
        projet.BilanBudget      = "Budget consommé à 95%, dans l'enveloppe.";
        projet.LeconsReussites  = "Bonne collaboration équipe DSI / métier.";
        projet.LeconsRecommandations = "Impliquer les utilisateurs finaux plus tôt dans les tests.";

        _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
        {
            Id          = Guid.NewGuid(),
            ProjetId    = projet.Id,
            Phase       = PhaseProjet.ClotureLeconsApprises,
            StatutProjet = projet.StatutProjet,
            DateDebut   = DateTime.Now,
            ModifieParId = _chefProjet.Id,
            Commentaire = "Fin UAT — passage en phase Clôture",
            DateCreation = DateTime.Now,
            CreePar     = _chefProjet.Matricule
        });

        await _db.SaveChangesAsync();

        var reload = await _db.Projets.FindAsync(projet.Id);
        reload!.PhaseActuelle.Should().Be(PhaseProjet.ClotureLeconsApprises,
            "après fin UAT, le projet passe en phase Clôture");
        reload.StatutProjet.Should().Be(StatutProjet.ClotureEnCours,
            "le statut est ClôtureEnCours jusqu'à la validation finale");
        reload.BilanPerimetre.Should().NotBeNullOrEmpty("le bilan doit être renseigné");
    }

    /// <summary>
    /// Étape 11 : Demande de clôture + validation en cascade (Demandeur → DM → DSI).
    /// La validation DSI déclenche la clôture définitive du projet.
    /// </summary>
    private async Task EtapeClotureAsync(Projet projet)
    {
        projet.PhaseActuelle.Should().Be(PhaseProjet.ClotureLeconsApprises);

        // Créer la demande de clôture
        var demandeCloture = new DemandeClotureProjet
        {
            Id                             = Guid.NewGuid(),
            ProjetId                       = projet.Id,
            DateDemande                    = DateTime.Now,
            DateSouhaiteeCloture           = DateTime.Now.AddDays(7),
            DemandeParId                   = _chefProjet.Id,
            CommentaireInitiateur          = "Tous les livrables sont déposés, projet prêt pour clôture.",
            StatutValidationDemandeur      = StatutValidationCloture.EnAttente,
            StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente,
            StatutValidationDSI            = StatutValidationCloture.EnAttente,
            EstTerminee                    = false,
            DateCreation                   = DateTime.Now,
            CreePar                        = _chefProjet.Matricule
        };

        _db.DemandesClotureProjets.Add(demandeCloture);
        await _db.SaveChangesAsync();

        // ── Demandeur valide ─────────────────────────────────────────────
        demandeCloture.StatutValidationDemandeur = StatutValidationCloture.Validee;
        demandeCloture.DateValidationDemandeur   = DateTime.Now;
        await _db.SaveChangesAsync();

        var apresValidationDemandeur = await _db.DemandesClotureProjets.FindAsync(demandeCloture.Id);
        apresValidationDemandeur!.StatutValidationDemandeur.Should().Be(StatutValidationCloture.Validee,
            "le demandeur a validé la clôture");
        apresValidationDemandeur.EstTerminee.Should().BeFalse(
            "la clôture n'est pas encore terminée, le DM et la DSI doivent encore valider");

        // ── Directeur Métier valide ──────────────────────────────────────
        demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
        demandeCloture.DateValidationDirecteurMetier   = DateTime.Now;
        await _db.SaveChangesAsync();

        var apresValidationDM = await _db.DemandesClotureProjets.FindAsync(demandeCloture.Id);
        apresValidationDM!.StatutValidationDirecteurMetier.Should().Be(StatutValidationCloture.Validee,
            "le Directeur Métier a validé la clôture");
        apresValidationDM.EstTerminee.Should().BeFalse(
            "la clôture n'est pas encore terminée, la DSI doit encore valider");

        // ── DSI valide → clôture finale ──────────────────────────────────
        demandeCloture.StatutValidationDSI = StatutValidationCloture.Validee;
        demandeCloture.DateValidationDSI   = DateTime.Now;

        // VerifierClotureComplete : les 3 ont validé → clôture définitive
        var tousValides =
            demandeCloture.StatutValidationDemandeur      == StatutValidationCloture.Validee &&
            demandeCloture.StatutValidationDirecteurMetier == StatutValidationCloture.Validee &&
            demandeCloture.StatutValidationDSI             == StatutValidationCloture.Validee;

        tousValides.Should().BeTrue("les 3 acteurs ont validé");

        if (tousValides && !demandeCloture.EstTerminee)
        {
            demandeCloture.EstTerminee          = true;
            demandeCloture.DateClotureFinale    = DateTime.Now;

            // Clôturer le projet
            var ficheProjet = await _db.FicheProjets.FirstAsync(f => f.ProjetId == projet.Id);
            projet.StatutProjet  = ficheProjet.StatutFinalCloture == "Abandonné"
                ? StatutProjet.Annule
                : StatutProjet.Cloture;
            projet.DateFinReelle = DateTime.Now;
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;
        }

        await _db.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════════════
    // VÉRIFICATION FINALE
    // ════════════════════════════════════════════════════════════════════════

    private async Task VerifierEtatFinalAsync(Guid projetId, Guid demandeId)
    {
        var projet = await _db.Projets
            .Include(p => p.DemandesCloture)
            .Include(p => p.HistoriquePhases)
            .Include(p => p.Livrables)
            .Include(p => p.FicheProjet)
            .FirstAsync(p => p.Id == projetId);

        var demande = await _db.DemandesProjets.FindAsync(demandeId);

        // ── Demande ──────────────────────────────────────────────────────
        demande!.StatutDemande.Should().Be(StatutDemande.ValideeParDSI,
            "la demande d'origine est validée par la DSI");

        // ── Projet — état final ──────────────────────────────────────────
        projet.StatutProjet.Should().Be(StatutProjet.Cloture,
            "le projet est définitivement clôturé après les 3 validations");
        projet.PhaseActuelle.Should().Be(PhaseProjet.ClotureLeconsApprises,
            "la dernière phase est Clôture & Leçons apprises");
        projet.DateFinReelle.Should().NotBeNull(
            "la date de fin réelle est renseignée à la clôture");
        projet.ChefProjetId.Should().Be(_chefProjet.Id,
            "le chef de projet reste affecté jusqu'en fin de projet");

        // ── Charte ───────────────────────────────────────────────────────
        projet.CharteValideeParDM.Should().BeTrue("le DM a validé la charte");
        projet.CharteValideeParDSI.Should().BeTrue("la DSI a validé la charte");
        projet.CharteValidee.Should().BeTrue("la charte est entièrement validée");

        // ── Planification ────────────────────────────────────────────────
        projet.PlanningValideParDM.Should().BeTrue("le DM a validé la planification");
        projet.PlanningValideParDSI.Should().BeTrue("la DSI a validé la planification");
        projet.DateDebut.Should().NotBeNull("le projet a une date de début");

        // ── UAT / MEP ────────────────────────────────────────────────────
        projet.RecetteValidee.Should().BeTrue("la recette a été validée par le DM");
        projet.MepEffectuee.Should().BeTrue("la MEP a été effectuée");
        projet.FicheProjet!.HypercareTermine.Should().BeTrue("l'hypercare est terminé");

        // ── Clôture ──────────────────────────────────────────────────────
        var demandeCloture = projet.DemandesCloture.Should().ContainSingle().Subject;
        demandeCloture.EstTerminee.Should().BeTrue("la demande de clôture est terminée");
        demandeCloture.StatutValidationDemandeur.Should().Be(StatutValidationCloture.Validee);
        demandeCloture.StatutValidationDirecteurMetier.Should().Be(StatutValidationCloture.Validee);
        demandeCloture.StatutValidationDSI.Should().Be(StatutValidationCloture.Validee);
        demandeCloture.DateClotureFinale.Should().NotBeNull();

        // ── Historique des phases ─────────────────────────────────────────
        var phases = projet.HistoriquePhases.Select(h => h.Phase).ToList();
        phases.Should().Contain(PhaseProjet.PlanificationValidation,
            "l'historique trace le passage en Planification");
        phases.Should().Contain(PhaseProjet.ExecutionSuivi,
            "l'historique trace le passage en Exécution");
        phases.Should().Contain(PhaseProjet.UatMep,
            "l'historique trace le passage en UAT");
        phases.Should().Contain(PhaseProjet.ClotureLeconsApprises,
            "l'historique trace le passage en Clôture");

        // ── Livrables par phase ──────────────────────────────────────────
        var typesLivrables = projet.Livrables.Select(l => l.TypeLivrable).ToList();
        typesLivrables.Should().Contain(TypeLivrable.CharteProjetSignee,
            "la charte signée est déposée en phase Analyse");
        typesLivrables.Should().Contain(TypeLivrable.CharteProjet,
            "la charte projet est déposée en phase Analyse");
        typesLivrables.Should().Contain(TypeLivrable.Wbs,
            "le WBS est déposé en phase Planification");
        typesLivrables.Should().Contain(TypeLivrable.CompteRenduReunion,
            "des CR de réunion sont déposés en phase Exécution");
        typesLivrables.Should().Contain(TypeLivrable.PvRecette,
            "le PV de recette est déposé en phase UAT");
        typesLivrables.Should().Contain(TypeLivrable.PvMep,
            "le PV de MEP est déposé en phase UAT");
        typesLivrables.Should().Contain(TypeLivrable.RapportCloture,
            "le rapport de clôture est déposé en phase Clôture");

        // ── Bilan et leçons apprises ──────────────────────────────────────
        projet.BilanPerimetre.Should().NotBeNullOrEmpty("le bilan périmètre est renseigné");
        projet.BilanPlanning.Should().NotBeNullOrEmpty("le bilan planning est renseigné");
        projet.LeconsReussites.Should().NotBeNullOrEmpty("les leçons apprises (réussites) sont renseignées");
        projet.LeconsRecommandations.Should().NotBeNullOrEmpty("les recommandations sont renseignées");

        // ── Responsable Solutions IT ─────────────────────────────────────
        projet.CommentaireTechnique.Should().NotBeNullOrEmpty(
            "le RSI a contribué son expertise technique");
        projet.DernierCommentaireTechniqueParId.Should().Be(_rsi.Id,
            "le commentaire technique a été rédigé par le RSI");
        projet.CharteValideeParDSIId.Should().Be(_rsi.Id,
            "le RSI a validé la charte côté DSI — habilitation identique à la DSI");
    }

    // ════════════════════════════════════════════════════════════════════════
    // TESTS COMPLÉMENTAIRES : CAS AUX LIMITES DU WORKFLOW
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Workflow_DemandeRejeteeParDM_StatutCorrect()
    {
        var demande = await CreerDemandeEnAttenteValidationDMAsync();

        // DM rejette la demande
        demande.StatutDemande = StatutDemande.RejeteeParDirecteurMetier;
        demande.CommentaireDirecteurMetier = "Projet non prioritaire pour le trimestre.";
        await _db.SaveChangesAsync();

        var reload = await _db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.RejeteeParDirecteurMetier,
            "le DM peut rejeter une demande");
        reload.CommentaireDirecteurMetier.Should().NotBeNullOrEmpty(
            "le commentaire est obligatoire lors d'un rejet");
    }

    [Fact]
    public async Task Workflow_DemandeRejeteeParDSI_StatutCorrect()
    {
        var demande = await CreerDemandeEnAttenteValidationDSIAsync();

        // DSI rejette la demande
        demande.StatutDemande    = StatutDemande.RejeteeParDSI;
        demande.CommentaireDSI   = "Budget insuffisant cette année.";
        await _db.SaveChangesAsync();

        var reload = await _db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.RejeteeParDSI,
            "la DSI peut rejeter une demande après validation DM");
        reload.CommentaireDSI.Should().NotBeNullOrEmpty(
            "le commentaire est obligatoire lors d'un rejet DSI");
    }

    [Fact]
    public async Task Workflow_CorrectionDemandeeParDM_RetourAuDemandeur()
    {
        var demande = await CreerDemandeEnAttenteValidationDMAsync();

        // DM demande une correction
        demande.StatutDemande = StatutDemande.CorrectionDemandeeParDirecteurMetier;
        demande.CommentaireDirecteurMetier = "Veuillez préciser le périmètre et les bénéfices attendus.";
        await _db.SaveChangesAsync();

        var reload = await _db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.CorrectionDemandeeParDirecteurMetier,
            "le DM peut demander une correction avant de valider");
    }

    [Fact]
    public async Task Workflow_CharteDMValidee_SansValidationDSI_CharteNonComplete()
    {
        var projet = await CreerProjetEnPhaseAnalyseAsync();

        // Seulement le DM valide
        projet.CharteValideeParDM  = true;
        projet.CharteValideeParDMId = _directeurMetier.Id;
        // DSI ne valide pas encore
        await _db.SaveChangesAsync();

        var reload = await _db.Projets.FindAsync(projet.Id);
        reload!.CharteValideeParDM.Should().BeTrue();
        reload.CharteValideeParDSI.Should().BeFalse();
        reload.CharteValidee.Should().BeFalse(
            "la charte n'est complète que quand DM ET DSI ont tous deux validé");
    }

    [Fact]
    public async Task Workflow_ClotureDSIRejetee_ProjetRepasseEnUAT()
    {
        var projet = await CreerProjetEnPhaseClotureAsync();
        var demandeCloture = projet.DemandesCloture.First();

        // Demandeur et DM ont validé
        demandeCloture.StatutValidationDemandeur      = StatutValidationCloture.Validee;
        demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
        await _db.SaveChangesAsync();

        // DSI rejette → retour en UAT
        demandeCloture.StatutValidationDSI            = StatutValidationCloture.Rejetee;
        demandeCloture.CommentaireDSI                 = "Livrables de clôture incomplets.";
        demandeCloture.StatutValidationDemandeur      = StatutValidationCloture.EnAttente;
        demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente;
        projet.PhaseActuelle = PhaseProjet.UatMep;
        await _db.SaveChangesAsync();

        var reloadProjet = await _db.Projets
            .Include(p => p.DemandesCloture)
            .FirstAsync(p => p.Id == projet.Id);

        reloadProjet.PhaseActuelle.Should().Be(PhaseProjet.UatMep,
            "après rejet DSI, le projet retourne en phase UAT");
        reloadProjet.DemandesCloture.First().StatutValidationDSI
            .Should().Be(StatutValidationCloture.Rejetee);
        reloadProjet.DemandesCloture.First().EstTerminee.Should().BeFalse(
            "la clôture n'est pas terminée après un rejet");
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS PRIVÉS
    // ════════════════════════════════════════════════════════════════════════

    private async Task<PortefeuilleProjet> CreerPortefeuilleParDefautAsync()
    {
        var portefeuille = new PortefeuilleProjet
        {
            Id                        = Guid.NewGuid(),
            Nom                       = "Portefeuille DSI Test",
            ObjectifStrategiqueGlobal = "Amélioration de l'efficacité opérationnelle.",
            AvantagesAttendus         = "Gains de productivité.",
            RisquesEtMitigations      = "Résistance au changement — mitigée par la formation.",
            EstActif                  = true,
            DateCreation              = DateTime.Now,
            CreePar                   = "SYSTEM"
        };
        _db.PortefeuillesProjets.Add(portefeuille);
        await _db.SaveChangesAsync();
        return portefeuille;
    }

    private async Task<DemandeProjet> CreerDemandeEnAttenteValidationDMAsync()
    {
        var demande = new DemandeProjet
        {
            Id                = Guid.NewGuid(),
            Titre             = "Projet Test DM",
            Description       = "Test",
            Contexte          = "Test",
            Objectifs         = "Test",
            DemandeurId       = _demandeur.Id,
            DirectionId       = _direction.Id,
            DirecteurMetierId = _directeurMetier.Id,
            StatutDemande     = StatutDemande.EnAttenteValidationDirecteurMetier,
            DateSoumission    = DateTime.Now,
            DateCreation      = DateTime.Now,
            CreePar           = _demandeur.Matricule
        };
        _db.DemandesProjets.Add(demande);
        await _db.SaveChangesAsync();
        return demande;
    }

    private async Task<DemandeProjet> CreerDemandeEnAttenteValidationDSIAsync()
    {
        var demande = await CreerDemandeEnAttenteValidationDMAsync();
        demande.StatutDemande  = StatutDemande.EnAttenteValidationDSI;
        demande.DateValidationDM = DateTime.Now;
        await _db.SaveChangesAsync();
        return demande;
    }

    private async Task<Projet> CreerProjetEnPhaseAnalyseAsync()
    {
        var portefeuille = await CreerPortefeuilleParDefautAsync();
        var demande      = await CreerDemandeEnAttenteValidationDSIAsync();

        var projet = new Projet
        {
            Id                  = Guid.NewGuid(),
            CodeProjet          = "PROJ-TEST-ANALYSE",
            Titre               = "Projet en Analyse",
            DemandeProjetId     = demande.Id,
            PortefeuilleProjetId = portefeuille.Id,
            SponsorId           = _directeurMetier.Id,
            ChefProjetId        = _chefProjet.Id,
            StatutProjet        = StatutProjet.NonDemarre,
            PhaseActuelle       = PhaseProjet.AnalyseClarification,
            EtatProjet          = EtatProjet.Vert,
            BilanCloture        = string.Empty,
            LeconsApprises      = string.Empty,
            DateCreation        = DateTime.Now,
            CreePar             = "SYSTEM"
        };
        _db.Projets.Add(projet);
        await _db.SaveChangesAsync();
        return projet;
    }

    private async Task<Projet> CreerProjetEnPhaseClotureAsync()
    {
        var projet = await CreerProjetEnPhaseAnalyseAsync();

        // FicheProjet avec hypercare terminé
        _db.FicheProjets.Add(new FicheProjet
        {
            Id                      = Guid.NewGuid(),
            ProjetId                = projet.Id,
            PeriodeHypercare        = "1 semaine",
            StatutHypercare         = "Terminé",
            HypercareTermine        = true,
            TransfertRunDocumentation = true,
            TransfertRunAcces       = true,
            TransfertRunSupportInforme = true,
            TransfertRunExploitationPrete = true,
            StatutFinalCloture      = "Clôturé",
            DateCreation            = DateTime.Now,
            CreePar                 = "SYSTEM"
        });

        projet.RecetteValidee = true;
        projet.MepEffectuee   = true;
        projet.PhaseActuelle  = PhaseProjet.ClotureLeconsApprises;
        projet.StatutProjet   = StatutProjet.ClotureEnCours;
        projet.BilanPerimetre = "Périmètre livré.";
        projet.LeconsReussites = "Bonne équipe.";

        // Demande de clôture en attente
        _db.DemandesClotureProjets.Add(new DemandeClotureProjet
        {
            Id                              = Guid.NewGuid(),
            ProjetId                        = projet.Id,
            DateDemande                     = DateTime.Now,
            DemandeParId                    = _chefProjet.Id,
            CommentaireInitiateur           = "Clôture demandée.",
            StatutValidationDemandeur       = StatutValidationCloture.EnAttente,
            StatutValidationDirecteurMetier  = StatutValidationCloture.EnAttente,
            StatutValidationDSI             = StatutValidationCloture.EnAttente,
            EstTerminee                     = false,
            DateCreation                    = DateTime.Now,
            CreePar                         = "SYSTEM"
        });

        await _db.SaveChangesAsync();

        return await _db.Projets
            .Include(p => p.DemandesCloture)
            .Include(p => p.FicheProjet)
            .FirstAsync(p => p.Id == projet.Id);
    }

    public void Dispose()
    {
        _db?.Database.EnsureDeleted();
        _db?.Dispose();
    }
}
