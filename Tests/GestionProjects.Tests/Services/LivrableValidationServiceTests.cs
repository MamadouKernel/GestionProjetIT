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
            var demandeur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "TEST004",
                Nom = "Test",
                Prenoms = "User",
                Email = "test4@test.com",
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
                CodeProjet = "PRJ-TEST-004",
                Titre = "Test Projet",
                DemandeProjetId = demande.Id,
                SponsorId = demandeur.Id,
                ChefProjetId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                PhaseActuelle = PhaseProjet.AnalyseClarification,
                StatutProjet = StatutProjet.EnCours,
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
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
            var demandeur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "TEST005",
                Nom = "Test",
                Prenoms = "User",
                Email = "test5@test.com",
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
                CodeProjet = "PRJ-TEST-005",
                Titre = "Test Projet",
                DemandeProjetId = demande.Id,
                SponsorId = demandeur.Id,
                ChefProjetId = demandeur.Id,
                DirectionId = demandeur.DirectionId,
                PhaseActuelle = PhaseProjet.AnalyseClarification,
                StatutProjet = StatutProjet.EnCours,
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
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
                    DeposeParId = demandeur.Id,
                    Commentaire = "",
                    Version = "1.0",
                    DateCreation = DateTime.Now,
                    CreePar = "SYSTEM"
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
            livrables.Should().Contain(TypeLivrable.CharteProjetSignee);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}

