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
    public class LivrableValidationServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ILivrableValidationService _service;

        public LivrableValidationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new LivrableValidationService(_context);
        }

        [Fact]
        public async Task ValiderLivrablesObligatoiresAsync_QuandLivrablesManquants_RetourneNonValide()
        {
            // Arrange
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                PhaseActuelle = PhaseProjet.AnalyseClarification,
                Titre = "Test Projet"
            };
            _context.Projets.Add(projet);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValiderLivrablesObligatoiresAsync(projet, PhaseProjet.PlanificationValidation);

            // Assert
            result.EstValide.Should().BeFalse();
            result.LivrablesManquants.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValiderLivrablesObligatoiresAsync_QuandTousLivrablesPresents_RetourneValide()
        {
            // Arrange
            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                PhaseActuelle = PhaseProjet.AnalyseClarification,
                Titre = "Test Projet"
            };
            _context.Projets.Add(projet);

            // Ajouter les livrables obligatoires
            var livrablesObligatoires = _service.GetLivrablesObligatoires(PhaseProjet.AnalyseClarification, PhaseProjet.PlanificationValidation);
            foreach (var typeLivrable in livrablesObligatoires)
            {
                _context.LivrablesProjets.Add(new LivrableProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    TypeLivrable = typeLivrable,
                    NomDocument = $"test_{typeLivrable}.pdf",
                    CheminRelatif = $"test_{typeLivrable}.pdf",
                    DateDepot = DateTime.Now,
                    Commentaire = "",
                    Version = "1.0"
                });
            }

            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValiderLivrablesObligatoiresAsync(projet, PhaseProjet.PlanificationValidation);

            // Assert
            result.EstValide.Should().BeTrue();
            result.LivrablesManquants.Should().BeEmpty();
        }

        [Fact]
        public void GetLivrablesObligatoires_QuandTransitionAnalyseVersPlanification_RetourneLivrablesCorrects()
        {
            // Act
            var livrables = _service.GetLivrablesObligatoires(
                PhaseProjet.AnalyseClarification,
                PhaseProjet.PlanificationValidation);

            // Assert
            livrables.Should().NotBeEmpty();
            livrables.Should().Contain(TypeLivrable.CharteProjet);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}

