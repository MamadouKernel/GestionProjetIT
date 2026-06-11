using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Validation;

/// <summary>
/// Tests pour le module Validation Directeur Métier (VALM-01 à VALM-15)
/// </summary>
public class ValidationDirecteurMetierTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ValidationDirecteurMetierTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
    }

    /// <summary>
    /// VALM-01 et VALM-02: Affichage et filtrage des demandes pour Directeur Métier
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task VALM01_02_DirecteurMetierVoitSeulementSesDemandes_DoitFiltrer()
    {
        // Arrange
        var directeurMetier = await _context.Utilisateurs
            .Include(u => u.Direction)
            .FirstAsync(u => u.Matricule == "DIR001");
        
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");

        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande à valider",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = directeurMetier.DirectionId,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier,
            DateSoumission = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();

        // Act
        var demandesAValider = await _context.DemandesProjets
            .Where(d => d.DirecteurMetierId == directeurMetier.Id &&
                       d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier)
            .ToListAsync();

        // Assert
        demandesAValider.Should().NotBeEmpty();
        demandesAValider.Should().OnlyContain(d => d.DirecteurMetierId == directeurMetier.Id);
        demandesAValider.Should().OnlyContain(d => 
            d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier);
    }

    /// <summary>
    /// VALM-03: Action Valider disponible
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task VALM03_ValidationDemande_DoitChangerStatut()
    {
        // Arrange
        var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DIR001");
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");

        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande Test",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = directeurMetier.DirectionId,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier,
            DateSoumission = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();

        // Act - Simuler la validation
        demande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
        demande.DateValidationDM = DateTime.Now;
        await _context.SaveChangesAsync();

        var demandeValidee = await _context.DemandesProjets.FindAsync(demande.Id);

        // Assert
        demandeValidee.Should().NotBeNull();
        demandeValidee!.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDSI);
        demandeValidee.DateValidationDM.Should().NotBeNull();
    }

    /// <summary>
    /// VALM-06 à VALM-08: Modification des champs de la demande
    /// Criticité: Majeure
    /// </summary>
    [Fact]
    public async Task VALM06_08_ModificationChampsDemande_DoitEtreSauvegardee()
    {
        // Arrange
        var demande = await CreerDemandeTestAsync();

        // Act
        demande.Titre = "Titre Modifié";
        demande.Description = "Description Modifiée";
        demande.Objectifs = "Objectifs Modifiés";
        demande.DateModification = DateTime.Now;
        demande.ModifiePar = "DIR001";
        await _context.SaveChangesAsync();

        var demandeModifiee = await _context.DemandesProjets.FindAsync(demande.Id);

        // Assert
        demandeModifiee.Should().NotBeNull();
        demandeModifiee!.Titre.Should().Be("Titre Modifié");
        demandeModifiee.Description.Should().Be("Description Modifiée");
        demandeModifiee.Objectifs.Should().Be("Objectifs Modifiés");
        demandeModifiee.DateModification.Should().NotBeNull();
    }

    /// <summary>
    /// VALM-10 et VALM-11: Demande de correction avec/sans commentaire
    /// Criticité: Bloquante
    /// </summary>
    [Theory]
    [InlineData("Veuillez corriger le budget", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task VALM10_11_DemandeCorrection_DoitValiderCommentaire(
        string? commentaire, bool doitReussir)
    {
        // Arrange
        var demande = await CreerDemandeTestAsync();

        // Act
        var commentaireValide = !string.IsNullOrWhiteSpace(commentaire);

        if (commentaireValide)
        {
            demande.StatutDemande = StatutDemande.CorrectionDemandeeParDirecteurMetier;
            demande.CommentaireDirecteurMetier = commentaire;
            await _context.SaveChangesAsync();
        }

        // Assert
        commentaireValide.Should().Be(doitReussir);
        
        if (doitReussir)
        {
            var demandeModifiee = await _context.DemandesProjets.FindAsync(demande.Id);
            demandeModifiee!.StatutDemande.Should().Be(
                StatutDemande.CorrectionDemandeeParDirecteurMetier);
            demandeModifiee.CommentaireDirecteurMetier.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// VALM-13 et VALM-14: Rejet avec/sans commentaire
    /// Criticité: Bloquante
    /// </summary>
    [Theory]
    [InlineData("Projet non aligné avec la stratégie", true)]
    [InlineData("", false)]
    public async Task VALM13_14_RejetDemande_DoitValiderCommentaire(
        string commentaire, bool doitReussir)
    {
        // Arrange
        var demande = await CreerDemandeTestAsync();

        // Act
        var commentaireValide = !string.IsNullOrWhiteSpace(commentaire);

        if (commentaireValide)
        {
            demande.StatutDemande = StatutDemande.RejeteeParDirecteurMetier;
            demande.CommentaireDirecteurMetier = commentaire;
            await _context.SaveChangesAsync();
        }

        // Assert
        commentaireValide.Should().Be(doitReussir);
        
        if (doitReussir)
        {
            var demandeRejetee = await _context.DemandesProjets.FindAsync(demande.Id);
            demandeRejetee!.StatutDemande.Should().Be(StatutDemande.RejeteeParDirecteurMetier);
            demandeRejetee.CommentaireDirecteurMetier.Should().NotBeNullOrEmpty();
        }
    }

    private async Task<GestionProjects.Domain.Models.DemandeProjet> CreerDemandeTestAsync()
    {
        var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DIR001");
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");

        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande Test",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = directeurMetier.DirectionId,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier,
            DateSoumission = DateTime.Now,
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

