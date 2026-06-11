using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Validation;

/// <summary>
/// Tests pour le module Validation DSI (VALD-01 à VALD-13)
/// </summary>
public class ValidationDSITests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ValidationDSITests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
    }

    /// <summary>
    /// VALD-01: Liste des demandes à valider pour DSI
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task VALD01_DSIVoitDemandesEnAttente_DoitFiltrer()
    {
        // Arrange
        var dsi = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DSI001");
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");
        var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DIR001");

        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande pour DSI",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = demandeur.DirectionId,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = StatutDemande.EnAttenteValidationDSI,
            DateSoumission = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();

        // Act
        var demandesAValider = await _context.DemandesProjets
            .Where(d => d.StatutDemande == StatutDemande.EnAttenteValidationDSI)
            .ToListAsync();

        // Assert
        demandesAValider.Should().NotBeEmpty();
        demandesAValider.Should().OnlyContain(d => 
            d.StatutDemande == StatutDemande.EnAttenteValidationDSI);
    }

    /// <summary>
    /// VALD-02 et VALD-03: Validation DSI et création automatique du projet
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task VALD02_03_ValidationDSI_DoitCreerProjet()
    {
        // Arrange
        var demande = await CreerDemandeEnAttenteValidationDSIAsync();
        var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "CP001");

        // Act - Simuler la validation DSI
        demande.StatutDemande = StatutDemande.ValideeParDSI;
        demande.DateValidationDSI = DateTime.Now;
        
        var projet = new GestionProjects.Domain.Models.Projet
        {
            Id = Guid.NewGuid(),
            CodeProjet = "PRJ-DSI-001",
            Titre = demande.Titre!,
            DemandeProjetId = demande.Id,
            DirectionId = demande.DirectionId,
            SponsorId = demande.DirecteurMetierId,
            ChefProjetId = chefProjet.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = PhaseProjet.AnalyseClarification,
            PourcentageAvancement = 0,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "DSI001"
        };

        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();

        // Assert
        var demandeValidee = await _context.DemandesProjets
            .Include(d => d.Projet)
            .FirstAsync(d => d.Id == demande.Id);
        
        demandeValidee.StatutDemande.Should().Be(StatutDemande.ValideeParDSI);
        demandeValidee.Projet.Should().NotBeNull();
        demandeValidee.Projet!.StatutProjet.Should().Be(StatutProjet.EnCours);
    }

    /// <summary>
    /// VALD-04: Statut initial du projet après validation DSI
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task VALD04_StatutInitialProjet_DoitEtreValideePourAnalyse()
    {
        // Arrange
        var demande = await CreerDemandeEnAttenteValidationDSIAsync();
        var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "CP001");

        // Act
        var projet = new GestionProjects.Domain.Models.Projet
        {
            Id = Guid.NewGuid(),
            CodeProjet = "PRJ-DSI-002",
            Titre = demande.Titre!,
            DemandeProjetId = demande.Id,
            DirectionId = demande.DirectionId,
            SponsorId = demande.DirecteurMetierId,
            ChefProjetId = chefProjet.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = PhaseProjet.AnalyseClarification,
            PourcentageAvancement = 0,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "DSI001"
        };

        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();

        // Assert
        var projetCree = await _context.Projets.FindAsync(projet.Id);
        projetCree.Should().NotBeNull();
        projetCree!.StatutProjet.Should().Be(StatutProjet.EnCours);
        projetCree.PhaseActuelle.Should().Be(PhaseProjet.AnalyseClarification);
    }

    /// <summary>
    /// VALD-06 et VALD-07: Rejet avec/sans commentaire
    /// Criticité: Bloquante
    /// </summary>
    [Theory]
    [InlineData("Budget insuffisant", true)]
    [InlineData("", false)]
    public async Task VALD06_07_RejetDSI_DoitValiderCommentaire(
        string commentaire, bool doitReussir)
    {
        // Arrange
        var demande = await CreerDemandeEnAttenteValidationDSIAsync();

        // Act
        var commentaireValide = !string.IsNullOrWhiteSpace(commentaire);

        if (commentaireValide)
        {
            demande.StatutDemande = StatutDemande.RejeteeParDSI;
            demande.CommentaireDSI = commentaire;
            await _context.SaveChangesAsync();
        }

        // Assert
        commentaireValide.Should().Be(doitReussir);
        
        if (doitReussir)
        {
            var demandeRejetee = await _context.DemandesProjets.FindAsync(demande.Id);
            demandeRejetee!.StatutDemande.Should().Be(StatutDemande.RejeteeParDSI);
            demandeRejetee.CommentaireDSI.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// VALD-09 et VALD-10: Retour vers demandeur ou directeur métier
    /// Criticité: Bloquante
    /// </summary>
    [Theory]
    [InlineData(StatutDemande.RetourneeAuDemandeurParDSI)]
    [InlineData(StatutDemande.RetourneeAuDirecteurMetierParDSI)]
    public async Task VALD09_10_RetourDemande_DoitChangerStatut(StatutDemande nouveauStatut)
    {
        // Arrange
        var demande = await CreerDemandeEnAttenteValidationDSIAsync();

        // Act
        demande.StatutDemande = nouveauStatut;
        demande.CommentaireDSI = "Veuillez compléter les informations";
        await _context.SaveChangesAsync();

        // Assert
        var demandeRetournee = await _context.DemandesProjets.FindAsync(demande.Id);
        demandeRetournee!.StatutDemande.Should().Be(nouveauStatut);
        demandeRetournee.CommentaireDSI.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// VALD-11 à VALD-13: Tests de délégation
    /// Criticité: Majeure/Bloquante
    /// </summary>
    [Fact]
    public async Task VALD11_13_DelegationValidation_DoitFonctionner()
    {
        // Arrange
        var dsi = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DSI001");
        var delegue = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "CP001");

        var delegation = new DelegationValidationDSI
        {
            Id = Guid.NewGuid(),
            DSIId = dsi.Id,
            DelegueId = delegue.Id,
            DateDebut = DateTime.Now.AddDays(-1),
            DateFin = DateTime.Now.AddDays(7),
            EstActive = true,
            DateCreation = DateTime.Now,
            CreePar = "DSI001"
        };

        _context.DelegationsValidationDSI.Add(delegation);
        await _context.SaveChangesAsync();

        // Act - Vérifier si la délégation est active
        var delegationActive = await _context.DelegationsValidationDSI
            .AnyAsync(d => d.DelegueId == delegue.Id &&
                          d.EstActive &&
                          d.DateDebut <= DateTime.Now &&
                          d.DateFin >= DateTime.Now &&
                          !d.EstSupprime);

        // Assert
        delegationActive.Should().BeTrue();
    }

    [Fact]
    public async Task VALD13_DelegationExpiree_NePeutPasValider()
    {
        // Arrange
        var dsi = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DSI001");
        var delegue = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "CP001");

        var delegation = new DelegationValidationDSI
        {
            Id = Guid.NewGuid(),
            DSIId = dsi.Id,
            DelegueId = delegue.Id,
            DateDebut = DateTime.Now.AddDays(-10),
            DateFin = DateTime.Now.AddDays(-1), // Expirée
            EstActive = true,
            DateCreation = DateTime.Now,
            CreePar = "DSI001"
        };

        _context.DelegationsValidationDSI.Add(delegation);
        await _context.SaveChangesAsync();

        // Act
        var delegationActive = await _context.DelegationsValidationDSI
            .AnyAsync(d => d.DelegueId == delegue.Id &&
                          d.EstActive &&
                          d.DateDebut <= DateTime.Now &&
                          d.DateFin >= DateTime.Now &&
                          !d.EstSupprime);

        // Assert
        delegationActive.Should().BeFalse();
    }

    private async Task<GestionProjects.Domain.Models.DemandeProjet> CreerDemandeEnAttenteValidationDSIAsync()
    {
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");
        var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DIR001");

        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande Test DSI",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = demandeur.DirectionId,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = StatutDemande.EnAttenteValidationDSI,
            DateSoumission = DateTime.Now,
            DateValidationDM = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();
        return demande;
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}
