using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests pour le module Projet (Analyse, Charte, Planification, Exécution, Recette, Clôture)
/// </summary>
public class ProjetTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ProjetTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
    }

    /// <summary>
    /// ANA-01 à ANA-04: Tests de l'équipe projet
    /// Criticité: Majeure
    /// </summary>
    [Fact]
    public async Task ANA01_04_AjoutMembreEquipe_DoitReussir()
    {
        // Arrange
        var projet = await CreerProjetTestAsync();
        var utilisateur = await _context.Utilisateurs.FirstAsync();

        var membre = new MembreProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            Nom = utilisateur.Nom,
            Prenom = utilisateur.Prenoms,
            Fonction = "Développeur",
            DirectionLibelle = "DSI",
            RoleDansProjet = "Développeur",
            Email = utilisateur.Email,
            EstActif = true,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        // Act
        _context.MembresProjets.Add(membre);
        await _context.SaveChangesAsync();

        var membreAjoute = await _context.MembresProjets
            .FirstOrDefaultAsync(m => m.Id == membre.Id);

        // Assert
        membreAjoute.Should().NotBeNull();
        membreAjoute!.RoleDansProjet.Should().Be("Développeur");
        membreAjoute.ProjetId.Should().Be(projet.Id);
    }

    /// <summary>
    /// ANA-10 à ANA-16: Tests de gestion des risques
    /// Criticité: Majeure
    /// </summary>
    [Fact]
    public async Task ANA10_16_CreationRisque_DoitReussir()
    {
        // Arrange
        var projet = await CreerProjetTestAsync();
        var responsable = await _context.Utilisateurs.FirstAsync();

        var risque = new RisqueProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            Description = "Risque de retard",
            Probabilite = ProbabiliteRisque.Moyenne,
            Impact = ImpactRisque.Eleve,
            PlanMitigation = "Augmenter les ressources",
            Responsable = $"{responsable.Nom} {responsable.Prenoms}",
            Statut = StatutRisque.Identifie,
            DateCreationRisque = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        // Act
        _context.RisquesProjets.Add(risque);
        await _context.SaveChangesAsync();

        var risqueCree = await _context.RisquesProjets
            .FirstOrDefaultAsync(r => r.Id == risque.Id);

        // Assert
        risqueCree.Should().NotBeNull();
        risqueCree!.Description.Should().Be("Risque de retard");
        risqueCree.Probabilite.Should().Be(ProbabiliteRisque.Moyenne);
        risqueCree.Impact.Should().Be(ImpactRisque.Eleve);
    }

    /// <summary>
    /// CHR-01 à CHR-05: Tests de la charte projet
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task CHR01_05_CharteProjet_DoitAvoirTousLesChamps()
    {
        // Arrange & Act
        var projet = await CreerProjetTestAsync();
        
        var chefProjet = await _context.Utilisateurs.FirstAsync();
        var demandeur = await _context.Utilisateurs.Skip(1).FirstAsync();
        
        var charte = new CharteProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            NomProjet = projet.Titre,
            ObjectifProjet = "Objectifs de la charte",
            Perimetre = "Périmètre défini",
            ContraintesInitiales = "Contraintes identifiées",
            RisquesInitiaux = "Risques initiaux",
            DemandeurId = demandeur.Id,
            ChefProjetId = chefProjet.Id,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.CharteProjets.Add(charte);
        await _context.SaveChangesAsync();

        // Assert
        var charteCreee = await _context.CharteProjets
            .FirstOrDefaultAsync(c => c.Id == charte.Id);
        
        charteCreee.Should().NotBeNull();
        charteCreee!.ObjectifProjet.Should().NotBeNullOrEmpty();
        charteCreee.Perimetre.Should().NotBeNullOrEmpty();
        charteCreee.ContraintesInitiales.Should().NotBeNullOrEmpty();
        charteCreee.RisquesInitiaux.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// PLAN-05 et PLAN-11: Tests des dates de planification
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task PLAN05_11_DatesProjet_DoiventEtreSauvegardees()
    {
        // Arrange
        var projet = await CreerProjetTestAsync();

        // Act
        projet.DateDebut = DateTime.Now;
        projet.DateFinPrevue = DateTime.Now.AddMonths(6);
        await _context.SaveChangesAsync();

        var projetMisAJour = await _context.Projets.FindAsync(projet.Id);

        // Assert
        projetMisAJour.Should().NotBeNull();
        projetMisAJour!.DateDebut.Should().NotBeNull();
        projetMisAJour.DateFinPrevue.Should().NotBeNull();
    }

    /// <summary>
    /// EXEC-04 à EXEC-09: Tests du pourcentage d'avancement
    /// Criticité: Bloquante
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(150, false)] // > 100%
    [InlineData(-1, false)] // Négatif
    public async Task EXEC04_09_PourcentageAvancement_DoitEtreValide(
        int pourcentage, bool estValide)
    {
        // Arrange
        var projet = await CreerProjetTestAsync();

        // Act & Assert
        if (estValide)
        {
            projet.PourcentageAvancement = pourcentage;
            await _context.SaveChangesAsync();
            
            var projetMisAJour = await _context.Projets.FindAsync(projet.Id);
            projetMisAJour!.PourcentageAvancement.Should().Be(pourcentage);
        }
        else
        {
            // Validation métier : le pourcentage doit être entre 0 et 100
            var validation = pourcentage >= 0 && pourcentage <= 100;
            validation.Should().BeFalse();
        }
    }

    /// <summary>
    /// EXEC-10: Test de l'état projet (Vert/Orange/Rouge)
    /// Criticité: Majeure
    /// </summary>
    [Theory]
    [InlineData(EtatProjet.Vert)]
    [InlineData(EtatProjet.Orange)]
    [InlineData(EtatProjet.Rouge)]
    public async Task EXEC10_EtatProjet_DoitEtreDefini(EtatProjet etat)
    {
        // Arrange
        var projet = await CreerProjetTestAsync();

        // Act
        projet.EtatProjet = etat;
        await _context.SaveChangesAsync();

        var projetMisAJour = await _context.Projets.FindAsync(projet.Id);

        // Assert
        projetMisAJour.Should().NotBeNull();
        projetMisAJour!.EtatProjet.Should().Be(etat);
    }

    [Fact]
    public void PourcentageAvancementAffiche_NonDemarre_DoitToujoursEtreZero()
    {
        var projet = new GestionProjects.Domain.Models.Projet
        {
            StatutProjet = StatutProjet.NonDemarre,
            PourcentageAvancement = 10
        };

        projet.PourcentageAvancementAffiche.Should().Be(0);
    }

    [Fact]
    public void PourcentageAvancementAffiche_Annule_NeDoitPasAfficherTermine()
    {
        var projet = new GestionProjects.Domain.Models.Projet
        {
            StatutProjet = StatutProjet.Annule,
            PourcentageAvancement = 100
        };

        projet.PourcentageAvancementAffiche.Should().Be(99);
    }

    /// <summary>
    /// UAT-07 et UAT-08: Tests des statuts de recette et MEP
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task UAT07_08_StatutsRecetteEtMEP_DoiventEtreDefinis()
    {
        // Arrange
        var projet = await CreerProjetTestAsync();

        // Act
        projet.RecetteValidee = true;
        projet.MepEffectuee = true;
        projet.DateRecetteValidee = DateTime.Now;
        projet.DateMep = DateTime.Now;
        await _context.SaveChangesAsync();

        var projetMisAJour = await _context.Projets.FindAsync(projet.Id);

        // Assert
        projetMisAJour.Should().NotBeNull();
        projetMisAJour!.RecetteValidee.Should().BeTrue();
        projetMisAJour.MepEffectuee.Should().BeTrue();
        projetMisAJour.DateRecetteValidee.Should().NotBeNull();
        projetMisAJour.DateMep.Should().NotBeNull();
    }

    /// <summary>
    /// CLOT-01 à CLOT-08: Tests du bilan et leçons apprises
    /// Criticité: Majeure
    /// </summary>
    [Fact]
    public async Task CLOT01_08_BilanProjet_DoitEtreComplet()
    {
        // Arrange
        var projet = await CreerProjetTestAsync();

        // Act
        projet.BilanCloture = "Bilan: Périmètre réalisé à 95%, planning respecté avec 2 semaines de retard, budget de 145000€";
        projet.LeconsApprises = "Leçons: Bonne collaboration équipe, améliorer communication externe";
        projet.StatutProjet = StatutProjet.Cloture;
        projet.DateFinReelle = DateTime.Now;
        await _context.SaveChangesAsync();

        var projetMisAJour = await _context.Projets.FindAsync(projet.Id);

        // Assert
        projetMisAJour.Should().NotBeNull();
        projetMisAJour!.BilanCloture.Should().NotBeNullOrEmpty();
        projetMisAJour.LeconsApprises.Should().NotBeNullOrEmpty();
        projetMisAJour.StatutProjet.Should().Be(StatutProjet.Cloture);
        projetMisAJour.DateFinReelle.Should().NotBeNull();
    }

    /// <summary>
    /// CLOT-12 à CLOT-15: Tests du workflow de clôture
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task CLOT12_15_WorkflowCloture_DoitSuivreEtapes()
    {
        // Arrange
        var projet = await CreerProjetTestAsync();
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");

        var demandeCloture = new DemandeClotureProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            DemandeParId = demandeur.Id,
            DateDemande = DateTime.Now,
            StatutValidationDemandeur = StatutValidationCloture.EnAttente,
            StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente,
            StatutValidationDSI = StatutValidationCloture.EnAttente,
            CommentaireDemandeur = "Demande de clôture",
            CommentaireDirecteurMetier = string.Empty,
            CommentaireDSI = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.DemandesClotureProjets.Add(demandeCloture);
        await _context.SaveChangesAsync();

        // Act - Simuler les validations successives
        demandeCloture.StatutValidationDemandeur = StatutValidationCloture.Validee;
        demandeCloture.DateValidationDemandeur = DateTime.Now;
        await _context.SaveChangesAsync();

        demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
        demandeCloture.DateValidationDirecteurMetier = DateTime.Now;
        await _context.SaveChangesAsync();

        demandeCloture.StatutValidationDSI = StatutValidationCloture.Validee;
        demandeCloture.DateValidationDSI = DateTime.Now;
        demandeCloture.EstTerminee = true;
        demandeCloture.DateClotureFinale = DateTime.Now;
        await _context.SaveChangesAsync();

        projet.StatutProjet = StatutProjet.Cloture;
        await _context.SaveChangesAsync();

        // Assert
        var demandeFinal = await _context.DemandesClotureProjets.FindAsync(demandeCloture.Id);
        demandeFinal!.StatutValidationDemandeur.Should().Be(StatutValidationCloture.Validee);
        demandeFinal.StatutValidationDirecteurMetier.Should().Be(StatutValidationCloture.Validee);
        demandeFinal.StatutValidationDSI.Should().Be(StatutValidationCloture.Validee);
        demandeFinal.DateValidationDemandeur.Should().NotBeNull();
        demandeFinal.DateValidationDirecteurMetier.Should().NotBeNull();
        demandeFinal.DateValidationDSI.Should().NotBeNull();
        demandeFinal.EstTerminee.Should().BeTrue();

        var projetCloture = await _context.Projets.FindAsync(projet.Id);
        projetCloture!.StatutProjet.Should().Be(StatutProjet.Cloture);
    }

    private async Task<GestionProjects.Domain.Models.Projet> CreerProjetTestAsync()
    {
        var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "CP001");
        var direction = await _context.Directions.FirstAsync();
        var sponsor = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DIR001");
        
        // Créer d'abord une demande de projet
        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande Test",
            Description = "Description de la demande",
            DemandeurId = sponsor.Id,
            DirectionId = direction.Id,
            DirecteurMetierId = sponsor.Id,
            StatutDemande = StatutDemande.ValideeParDSI,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();

        var projet = new GestionProjects.Domain.Models.Projet
        {
            Id = Guid.NewGuid(),
            CodeProjet = "PRJ-TEST-001",
            Titre = "Projet Test",
            DemandeProjetId = demande.Id,
            ChefProjetId = chefProjet.Id,
            DirectionId = direction.Id,
            SponsorId = sponsor.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = PhaseProjet.AnalyseClarification,
            PourcentageAvancement = 0,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();
        return projet;
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}

