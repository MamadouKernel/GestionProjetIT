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
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                Titre = "Projet Test",
                PhaseActuelle = PhaseProjet.ExecutionSuivi,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert,
                DateDebut = DateTime.Now.AddDays(-30),
                DateFinPrevue = DateTime.Now.AddDays(30),
                PourcentageAvancement = 50
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
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                Titre = "Projet Test",
                PhaseActuelle = PhaseProjet.ExecutionSuivi,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert,
                DateDebut = DateTime.Now.AddDays(-30),
                DateFinPrevue = DateTime.Now.AddDays(30)
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
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                Titre = "Projet Test",
                PhaseActuelle = PhaseProjet.ExecutionSuivi,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert
            };
            _context.Projets.Add(projet);

            var risque = new RisqueProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Description = "Risque critique",
                Probabilite = ProbabiliteRisque.Elevee,
                Impact = ImpactRisque.Critique,
                Statut = StatutRisque.Identifie
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

