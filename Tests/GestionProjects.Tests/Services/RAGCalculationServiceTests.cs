using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Services
{
    public class RAGCalculationServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IRAGCalculationService _service;

        public RAGCalculationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new RAGCalculationService(_context);
        }

        [Fact]
        public async Task CalculerRAGAsync_QuandProjetDansLesClous_RetourneVert()
        {
            // Arrange
            var demandeur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "TEST001",
                Nom = "Test",
                Prenoms = "User",
                Email = "test@test.com",
                MotDePasse = "test",
                DirectionId = Guid.NewGuid(),
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(demandeur);
            
            var demande = new DemandeProjet
            {
                Id = Guid.NewGuid(),
                Titre = "Demande Test",
                Description = "Test",
                Contexte = "Test",
                Objectifs = "Test",
                DemandeurId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                DirecteurMetierId = demandeur.Id,
                StatutDemande = StatutDemande.ValideeParDSI,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.DemandesProjets.Add(demande);
            
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                CodeProjet = "PRJ-TEST-001",
                Titre = "Projet Test",
                DemandeProjetId = demande.Id,
                SponsorId = demandeur.Id,
                ChefProjetId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                PhaseActuelle = PhaseProjet.ExecutionSuivi,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert,
                DateDebut = DateTime.Now.AddDays(-30),
                DateFinPrevue = DateTime.Now.AddDays(30),
                PourcentageAvancement = 50,
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            
            var ficheProjet = new FicheProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                BudgetPrevisionnel = 100000,
                BudgetConsomme = 95000 // 5% d'écart
            };
            _context.FicheProjets.Add(ficheProjet);
            _context.Projets.Add(projet);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.CalculerRAGAsync(projet);

            // Assert
            result.Should().Be(IndicateurRAG.Vert);
        }

        [Fact]
        public async Task CalculerRAGAsync_QuandBudgetDepasse_RetourneRouge()
        {
            // Arrange
            var demandeur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "TEST002",
                Nom = "Test",
                Prenoms = "User",
                Email = "test2@test.com",
                MotDePasse = "test",
                DirectionId = Guid.NewGuid(),
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(demandeur);
            
            var demande = new DemandeProjet
            {
                Id = Guid.NewGuid(),
                Titre = "Demande Test",
                Description = "Test",
                Contexte = "Test",
                Objectifs = "Test",
                DemandeurId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                DirecteurMetierId = demandeur.Id,
                StatutDemande = StatutDemande.ValideeParDSI,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.DemandesProjets.Add(demande);
            
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                CodeProjet = "PRJ-TEST-002",
                Titre = "Projet Test",
                DemandeProjetId = demande.Id,
                SponsorId = demandeur.Id,
                ChefProjetId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                PhaseActuelle = PhaseProjet.ExecutionSuivi,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert,
                DateDebut = DateTime.Now.AddDays(-30),
                DateFinPrevue = DateTime.Now.AddDays(30),
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            
            var ficheProjet = new FicheProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                BudgetPrevisionnel = 100000,
                BudgetConsomme = 120000 // 20% de dépassement
            };
            _context.FicheProjets.Add(ficheProjet);
            _context.Projets.Add(projet);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.CalculerRAGAsync(projet);

            // Assert
            result.Should().Be(IndicateurRAG.Rouge);
        }

        [Fact]
        public async Task CalculerRAGAsync_QuandRisqueCritique_RetourneRouge()
        {
            // Arrange
            var demandeur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "TEST003",
                Nom = "Test",
                Prenoms = "User",
                Email = "test3@test.com",
                MotDePasse = "test",
                DirectionId = Guid.NewGuid(),
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(demandeur);
            
            var demande = new DemandeProjet
            {
                Id = Guid.NewGuid(),
                Titre = "Demande Test",
                Description = "Test",
                Contexte = "Test",
                Objectifs = "Test",
                DemandeurId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                DirecteurMetierId = demandeur.Id,
                StatutDemande = StatutDemande.ValideeParDSI,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.DemandesProjets.Add(demande);
            
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                CodeProjet = "PRJ-TEST-003",
                Titre = "Projet Test",
                DemandeProjetId = demande.Id,
                SponsorId = demandeur.Id,
                ChefProjetId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                PhaseActuelle = PhaseProjet.ExecutionSuivi,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert,
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Projets.Add(projet);

            var risque = new RisqueProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Description = "Risque critique",
                Probabilite = ProbabiliteRisque.Elevee,
                Impact = ImpactRisque.Critique,
                Statut = StatutRisque.Identifie,
                PlanMitigation = "Plan",
                Responsable = "Test",
                DateCreationRisque = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.RisquesProjets.Add(risque);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.CalculerRAGAsync(projet);

            // Assert
            result.Should().Be(IndicateurRAG.Rouge);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}

