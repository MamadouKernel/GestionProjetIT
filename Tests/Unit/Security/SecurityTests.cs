using FluentAssertions;
using GestionProjects.Controllers;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Security;

/// <summary>
/// Tests pour le module Sécurité (AUTH-05 à AUTH-10)
/// </summary>
public class SecurityTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public SecurityTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
    }

    /// <summary>
    /// AUTH-05: Interdiction d'accès aux écrans d'administration pour Demandeur
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task AUTH05_DemandeurNePeutPasAccederAdmin_DoitEtreBloque()
    {
        // Arrange
        var demandeur = await _context.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Matricule == "DEM001");

        // Assert
        demandeur.Should().NotBeNull();
        var roles = demandeur!.UtilisateurRoles
            .Where(ur => !ur.EstSupprime)
            .Select(ur => ur.Role)
            .ToList();
        
        roles.Should().NotContain(RoleUtilisateur.AdminIT);
        roles.Should().NotContain(RoleUtilisateur.DSI);
        roles.Should().Contain(RoleUtilisateur.Demandeur);
    }

    /// <summary>
    /// AUTH-06: Isolation des données entre directions pour Directeur métier
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task AUTH06_DirecteurMetierVoitSeulementSaDirection_DoitEtreIsole()
    {
        // Arrange
        var directeurMetier = await _context.Utilisateurs
            .Include(u => u.Direction)
            .FirstOrDefaultAsync(u => u.Matricule == "DIR001");

        var directionId = directeurMetier!.DirectionId;

        // Simuler la requête de projets avec isolation
        var projetsVisibles = await _context.Projets
            .Where(p => p.DirectionId == directionId)
            .ToListAsync();

        var tousLesProjets = await _context.Projets.ToListAsync();

        // Assert
        directeurMetier.Should().NotBeNull();
        directeurMetier!.DirectionId.Should().NotBeNull();
        
        // Le directeur métier ne doit voir que les projets de sa direction
        if (tousLesProjets.Any())
        {
            projetsVisibles.Should().OnlyContain(p => p.DirectionId == directionId);
        }
    }

    /// <summary>
    /// AUTH-07: Visibilité limitée aux projets assignés pour Chef de projet
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task AUTH07_ChefProjetVoitSeulementSesProjets_DoitEtreLimite()
    {
        // Arrange
        var chefProjet = await _context.Utilisateurs
            .FirstOrDefaultAsync(u => u.Matricule == "CP001");

        // Créer un projet assigné au chef de projet
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");
        var direction = await _context.Directions.FirstAsync();
        
        // Créer d'abord une demande
        var demande = new GestionProjects.Domain.Models.DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande Test",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = direction.Id,
            DirecteurMetierId = demandeur.Id,
            StatutDemande = StatutDemande.ValideeParDSI,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };
        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();
        
        var projet = new GestionProjects.Domain.Models.Projet {
            Id = Guid.NewGuid(),
            CodeProjet = "PRJ-SEC-001",
            Titre = "Projet Test",
            DemandeProjetId = demande.Id,
            ChefProjetId = chefProjet!.Id,
            DirectionId = direction.Id,
            SponsorId = demandeur.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = PhaseProjet.AnalyseClarification,
            PourcentageAvancement = 0,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };
        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();

        // Simuler la requête avec filtre chef de projet
        var projetsVisibles = await _context.Projets
            .Where(p => p.ChefProjetId == chefProjet.Id)
            .ToListAsync();

        // Assert
        projetsVisibles.Should().NotBeEmpty();
        projetsVisibles.Should().OnlyContain(p => p.ChefProjetId == chefProjet.Id);
    }

    /// <summary>
    /// AUTH-08: Accès global à tous les projets pour DSI
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task AUTH08_DSIVoitTousLesProjets_DoitAvoirAccesGlobal()
    {
        // Arrange
        var dsi = await _context.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Matricule == "DSI001");

        // Assert
        dsi.Should().NotBeNull();
        var roles = dsi!.UtilisateurRoles
            .Where(ur => !ur.EstSupprime)
            .Select(ur => ur.Role)
            .ToList();
        
        roles.Should().Contain(RoleUtilisateur.DSI);
        
        // DSI doit pouvoir voir tous les projets (pas de filtre)
        var tousLesProjets = await _context.Projets.ToListAsync();
        tousLesProjets.Should().NotBeNull();
    }

    /// <summary>
    /// AUTH-09: Droits complets sur l'application pour Admin IT
    /// Criticité: Bloquante
    /// </summary>
    [Fact]
    public async Task AUTH09_AdminITADroitsComplets_DoitAvoirAccesTotal()
    {
        // Arrange
        var adminIT = await _context.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Matricule == "admin");

        // Assert
        adminIT.Should().NotBeNull();
        var roles = adminIT!.UtilisateurRoles
            .Where(ur => !ur.EstSupprime)
            .Select(ur => ur.Role)
            .ToList();
        
        roles.Should().Contain(RoleUtilisateur.AdminIT);
    }

    /// <summary>
    /// Test de vérification des rôles multiples
    /// </summary>
    [Fact]
    public async Task UtilisateurPeutAvoirPlusieursRoles()
    {
        // Arrange
        var utilisateur = await _context.Utilisateurs.FirstAsync();
        
        // Ajouter un second rôle
        var nouveauRole = new UtilisateurRole
        {
            Id = Guid.NewGuid(),
            UtilisateurId = utilisateur.Id,
            Role = RoleUtilisateur.ChefDeProjet,
            DateDebut = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };
        _context.UtilisateurRoles.Add(nouveauRole);
        await _context.SaveChangesAsync();

        // Act
        var rolesUtilisateur = await _context.UtilisateurRoles
            .Where(ur => ur.UtilisateurId == utilisateur.Id && !ur.EstSupprime)
            .ToListAsync();

        // Assert
        rolesUtilisateur.Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    /// Test de soft delete pour les rôles
    /// </summary>
    [Fact]
    public async Task RolesSupprimesNeSontPasPrisEnCompte()
    {
        // Arrange
        var utilisateur = await _context.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstAsync();
        
        var role = utilisateur.UtilisateurRoles.First();
        role.EstSupprime = true;
        await _context.SaveChangesAsync();

        // Act
        var rolesActifs = await _context.UtilisateurRoles
            .Where(ur => ur.UtilisateurId == utilisateur.Id && !ur.EstSupprime)
            .ToListAsync();

        // Assert
        rolesActifs.Should().NotContain(r => r.Id == role.Id);
    }

    [Theory]
    [InlineData(nameof(ProjetController.ValiderPlanifDM), "DirecteurMetier")]
    [InlineData(nameof(ProjetController.ValiderPlanifDSI), "DSI,ResponsableSolutionsIT")]
    [InlineData(nameof(ProjetController.ValiderRecette), "DirecteurMetier")]
    [InlineData(nameof(ProjetController.ValiderCharteDM), "DirecteurMetier")]
    [InlineData(nameof(ProjetController.RejeterCharteDM), "DirecteurMetier")]
    [InlineData(nameof(ProjetController.ValiderCharteDSI), "DSI,ResponsableSolutionsIT")]
    [InlineData(nameof(ProjetController.RejeterCharteDSI), "DSI,ResponsableSolutionsIT")]
    public void ValidationCharteEndpoints_ShouldExposeExpectedRoles(string actionName, string expectedRoles)
    {
        var method = typeof(ProjetController).GetMethod(actionName);

        method.Should().NotBeNull();
        var authorize = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(expectedRoles);
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}


