using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Integration
{
    public class WorkflowDemandeProjetTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        public WorkflowDemandeProjetTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
        }

        [Fact]
        public async Task WorkflowComplet_Demande_Creation_Validation_Projet()
        {
            // Arrange - Créer les données de test
            var direction = new Direction
            {
                Id = Guid.NewGuid(),
                Code = "DIR001",
                Libelle = "Direction Test",
                EstActive = true,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Directions.Add(direction);

            var demandeur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "DEM001",
                Nom = "Demandeur",
                Prenoms = "Test",
                Email = "demandeur@test.com",
                MotDePasse = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                DirectionId = direction.Id,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(demandeur);

            var directeurMetier = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "DM001",
                Nom = "Directeur",
                Prenoms = "Metier",
                Email = "dm@test.com",
                MotDePasse = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                DirectionId = direction.Id,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(directeurMetier);

            var roleDM = new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = directeurMetier.Id,
                Role = RoleUtilisateur.DirecteurMetier,
                DateDebut = DateTime.Now,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.UtilisateurRoles.Add(roleDM);

            await _context.SaveChangesAsync();

            // Act & Assert - Créer une demande
            var demande = new DemandeProjet
            {
                Id = Guid.NewGuid(),
                Titre = "Projet Test",
                Description = "Description test",
                Objectifs = "Objectifs test",
                DemandeurId = demandeur.Id,
                DirecteurMetierId = directeurMetier.Id,
                DirectionId = direction.Id,
                StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier,
                Urgence = UrgenceProjet.Moyenne,
                Criticite = CriticiteProjet.Moyenne,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                EstSupprime = false
            };
            _context.DemandesProjets.Add(demande);
            await _context.SaveChangesAsync();

            demande.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDirecteurMetier);

            // Act & Assert - Valider par DM
            demande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
            demande.DateValidationDM = DateTime.Now;
            demande.CommentaireDirecteurMetier = "Validé par DM";
            await _context.SaveChangesAsync();

            demande.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDSI);

            // Act & Assert - Valider par DSI et créer projet
            var chefProjet = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "CP001",
                Nom = "Chef",
                Prenoms = "Projet",
                Email = "cp@test.com",
                MotDePasse = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(chefProjet);

            demande.StatutDemande = StatutDemande.ValideeParDSI;
            demande.DateValidationDSI = DateTime.Now;
            demande.CommentaireDSI = "Validé par DSI";

            var projet = new Projet
            {
                Id = Guid.NewGuid(),
                DemandeProjetId = demande.Id,
                Titre = demande.Titre,
                DirectionId = direction.Id,
                SponsorId = demandeur.Id,
                ChefProjetId = chefProjet.Id,
                PhaseActuelle = PhaseProjet.AnalyseClarification,
                StatutProjet = StatutProjet.EnCours,
                EtatProjet = EtatProjet.Vert,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                EstSupprime = false
            };
            _context.Projets.Add(projet);
            await _context.SaveChangesAsync();

            projet.Should().NotBeNull();
            projet.PhaseActuelle.Should().Be(PhaseProjet.AnalyseClarification);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
