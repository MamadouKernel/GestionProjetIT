using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GestionProjects.Tests.Services
{
    public class UtilisateurServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        private readonly UtilisateurService _service;

        public UtilisateurServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _currentUserMock = new Mock<ICurrentUserService>();
            _currentUserMock.Setup(x => x.Matricule).Returns("ADMIN001");

            _service = new UtilisateurService(_context, _currentUserMock.Object);
        }

        // ---------------------------------------------------------------
        // CreateUserAsync
        // ---------------------------------------------------------------

        [Fact]
        public async Task CreateUserAsync_MotDePasseFaible_LanceException()
        {
            // Arrange — mot de passe sans majuscule
            var motDePasse = "motdepasse123"; // pas de majuscule

            // Act
            var act = async () => await _service.CreateUserAsync(
                "MAT001", "Dupont", "Jean", "jean@test.com",
                motDePasse, null, new[] { RoleUtilisateur.Demandeur });

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("motDePasse");
        }

        [Fact]
        public async Task CreateUserAsync_MatriculeExistant_LanceException()
        {
            // Arrange — ajouter un utilisateur avec le même matricule
            _context.Utilisateurs.Add(new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "MAT001",
                Nom = "Existant",
                Prenoms = "User",
                Email = "existant@test.com",
                MotDePasse = "hash",
                DirectionId = null,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                EstSupprime = false
            });
            await _context.SaveChangesAsync();

            // Act
            var act = async () => await _service.CreateUserAsync(
                "MAT001", "Nouveau", "User", "nouveau@test.com",
                "MotDePasse1Fort", null, new[] { RoleUtilisateur.Demandeur });

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("matricule");
        }

        [Fact]
        public async Task CreateUserAsync_EmailExistant_LanceException()
        {
            // Arrange — ajouter un utilisateur avec le même email
            _context.Utilisateurs.Add(new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "MAT002",
                Nom = "Existant",
                Prenoms = "User",
                Email = "doublon@test.com",
                MotDePasse = "hash",
                DirectionId = null,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                EstSupprime = false
            });
            await _context.SaveChangesAsync();

            // Act
            var act = async () => await _service.CreateUserAsync(
                "MAT999", "Nouveau", "User", "doublon@test.com",
                "MotDePasse1Fort", null, new[] { RoleUtilisateur.Demandeur });

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("email");
        }

        [Fact]
        public async Task CreateUserAsync_Nominal_CreeUtilisateurAvecRoles()
        {
            // Arrange
            var roles = new[] { RoleUtilisateur.Demandeur, RoleUtilisateur.ChefDeProjet };

            // Act
            var utilisateur = await _service.CreateUserAsync(
                "MAT010", "Koné", "Amadou", "amadou@test.com",
                "MotDePasse1Fort", null, roles);

            await _context.SaveChangesAsync();

            // Assert — utilisateur bien ajouté au contexte
            utilisateur.Should().NotBeNull();
            utilisateur.Matricule.Should().Be("MAT010");

            // Les rôles doivent être présents
            utilisateur.UtilisateurRoles.Should().NotBeEmpty();
            var rolesActifs = utilisateur.UtilisateurRoles
                .Where(ur => !ur.EstSupprime)
                .Select(ur => ur.Role)
                .ToList();
            rolesActifs.Should().Contain(RoleUtilisateur.Demandeur);
            rolesActifs.Should().Contain(RoleUtilisateur.ChefDeProjet);
        }

        // ---------------------------------------------------------------
        // ParseSelectedRoles
        // ---------------------------------------------------------------

        [Fact]
        public void ParseSelectedRoles_AdminITExclusif_RetourneSeulementAdminIT()
        {
            // Arrange — AdminIT (6) + Demandeur (1)
            var rolesStr = "6,1";

            // Act
            var result = _service.ParseSelectedRoles(rolesStr);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be(RoleUtilisateur.AdminIT);
        }

        [Fact]
        public void ParseSelectedRoles_ListeVide_RetourneDemandeur()
        {
            // Act
            var result = _service.ParseSelectedRoles(null);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be(RoleUtilisateur.Demandeur);
        }

        [Fact]
        public void ParseSelectedRoles_MultipleRoles_RetourneTous()
        {
            // Arrange — Demandeur (1), DSI (3), ChefDeProjet (5)
            var rolesStr = "1,3,5";

            // Act
            var result = _service.ParseSelectedRoles(rolesStr);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(RoleUtilisateur.Demandeur);
            result.Should().Contain(RoleUtilisateur.DSI);
            result.Should().Contain(RoleUtilisateur.ChefDeProjet);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
