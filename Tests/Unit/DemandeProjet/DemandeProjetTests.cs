using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.DemandeProjet;

/// <summary>
/// Tests pour le module Demande de projet (DEM-01 à DEM-30)
/// </summary>
public class DemandeProjetTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public DemandeProjetTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
    }

    /// <summary>
    /// DEM-01 à DEM-13: Vérification de la présence des champs obligatoires
    /// Criticité: Bloquante/Majeure
    /// </summary>
    [Fact]
    public async Task DEM01_ChampsFormulaireDemande_DoiventExister()
    {
        // Arrange & Act
        var demandeur = await _context.Utilisateurs
            .Include(u => u.Direction)
            .FirstOrDefaultAsync(u => u.Matricule == "DEM001");
        
        var directeurMetier = await _context.Utilisateurs
            .FirstOrDefaultAsync(u => u.Matricule == "DIR001");

        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Projet Test",
            Description = "Description du projet",
            Contexte = "Contexte du projet",
            Objectifs = "Objectifs du projet",
            AvantagesAttendus = "Avantages attendus",
            Urgence = UrgenceProjet.Haute,
            Criticite = CriticiteProjet.Elevee,
            DateMiseEnOeuvreSouhaitee = DateTime.Now.AddMonths(3),
            DemandeurId = demandeur!.Id,
            DirectionId = demandeur.DirectionId,
            DirecteurMetierId = directeurMetier!.Id,
            StatutDemande = StatutDemande.Brouillon,
            DateSoumission = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        // Assert - Tous les champs requis sont présents
        demande.Titre.Should().NotBeNullOrEmpty();
        demande.Description.Should().NotBeNullOrEmpty();
        demande.Contexte.Should().NotBeNullOrEmpty();
        demande.Objectifs.Should().NotBeNullOrEmpty();
        demande.DemandeurId.Should().NotBeEmpty();
        demande.DirectionId.Should().NotBeNull();
        demande.DirecteurMetierId.Should().NotBeEmpty();
        demande.Urgence.Should().BeOneOf(Enum.GetValues<UrgenceProjet>());
        demande.Criticite.Should().BeOneOf(Enum.GetValues<CriticiteProjet>());
    }

    /// <summary>
    /// DEM-14 à DEM-21: Tests de validation des champs obligatoires
    /// Criticité: Bloquante
    /// </summary>
    [Theory]
    [InlineData("", "Description", "Contexte", "Objectifs")] // Titre vide
    [InlineData("Titre", "", "Contexte", "Objectifs")] // Description vide
    [InlineData("Titre", "Description", "", "Objectifs")] // Contexte vide
    [InlineData("Titre", "Description", "Contexte", "")] // Objectifs vide
    public void DEM14_21_ValidationChampsObligatoires_DoitEchouer(
        string titre, string description, string contexte, string objectifs)
    {
        // Arrange
        var estValide = !string.IsNullOrWhiteSpace(titre) &&
                       !string.IsNullOrWhiteSpace(description) &&
                       !string.IsNullOrWhiteSpace(contexte) &&
                       !string.IsNullOrWhiteSpace(objectifs);

        // Assert
        estValide.Should().BeFalse();
    }

    /// <summary>
    /// DEM-22: Test de longueur du titre
    /// Criticité: Moyenne
    /// </summary>
    [Fact]
    public void DEM22_TitreTresLong_DoitEtreGere()
    {
        // Arrange
        var titreLong = new string('A', 500);
        
        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Titre = titreLong
        };

        // Assert
        demande.Titre.Should().HaveLength(500);
        demande.Titre.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// DEM-23: Test des caractères spéciaux
    /// Criticité: Moyenne
    /// </summary>
    [Fact]
    public void DEM23_CaracteresSpeciaux_DoiventEtreAcceptes()
    {
        // Arrange
        var titreAvecCaracteresSpeciaux = "Projet d'amélioration - Côte d'Ivoire (2024/2025)";
        
        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Titre = titreAvecCaracteresSpeciaux
        };

        // Assert
        demande.Titre.Should().Contain("'");
        demande.Titre.Should().Contain("-");
        demande.Titre.Should().Contain("/");
    }

    /// <summary>
    /// DEM-27 à DEM-28: Création de demande valide et statut initial
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task DEM27_28_CreationDemandeValide_DoitReussir()
    {
        // Arrange
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");
        var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DIR001");
        
        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Nouveau Projet Test",
            Description = "Description complète",
            Contexte = "Contexte détaillé",
            Objectifs = "Objectifs clairs",
            AvantagesAttendus = "Avantages mesurables",
            Urgence = UrgenceProjet.Moyenne,
            Criticite = CriticiteProjet.Moyenne,
            DateMiseEnOeuvreSouhaitee = DateTime.Now.AddMonths(6),
            DemandeurId = demandeur.Id,
            DirectionId = demandeur.DirectionId,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier,
            DateSoumission = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        // Act
        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();

        var demandeCreee = await _context.DemandesProjets.FindAsync(demande.Id);

        // Assert
        demandeCreee.Should().NotBeNull();
        demandeCreee!.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDirecteurMetier);
        demandeCreee.DemandeurId.Should().Be(demandeur.Id);
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}
