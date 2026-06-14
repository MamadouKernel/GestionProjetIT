using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GestionProjects.Tests.Unit.Administration;

public sealed class DemandeCreationCompteWorkflowServiceTests
{
    [Fact]
    public async Task SoumettreAsync_DoitCreerDemandeEtNotifierDirecteurMetier()
    {
        await using var db = CreateDbContext();
        var (direction, directeurMetier) = await SeedDirectionAndDmAsync(db);
        var email = new Mock<IEmailService>();
        email.SetReturnsDefault(Task.CompletedTask);
        var service = new DemandeCreationCompteWorkflowService(db, email.Object);

        var result = await service.SoumettreAsync(new SoumettreDemandeCreationCompteInput(
            "  Kone  ",
            "  Awa  ",
            "  awa.kone@cit.test  ",
            direction.Id,
            "  Comptabilite  ",
            directeurMetier.Id));

        result.Succeeded.Should().BeTrue();

        var demande = await db.DemandesCreationCompte.SingleAsync();
        demande.Nom.Should().Be("Kone");
        demande.Prenoms.Should().Be("Awa");
        demande.Email.Should().Be("awa.kone@cit.test");
        demande.Service.Should().Be("Comptabilite");
        demande.DirectionId.Should().Be(direction.Id);
        demande.DirecteurMetierId.Should().Be(directeurMetier.Id);
        demande.Statut.Should().Be(StatutDemandeCompte.EnAttenteValidationDM);
        demande.CreePar.Should().Be("ANONYMOUS");

        email.Verify(e => e.EnvoyerDemandeCreationCompteAuDMAsync(
            directeurMetier.Email,
            "Dupont Marie",
            "Kone Awa",
            direction.Libelle,
            "Comptabilite",
            "awa.kone@cit.test"), Times.Once);
    }

    [Fact]
    public async Task SoumettreAsync_EmailUtilisateurExistant_DoitRefuserSansCreerDeDemande()
    {
        await using var db = CreateDbContext();
        var (direction, directeurMetier) = await SeedDirectionAndDmAsync(db);
        db.Utilisateurs.Add(new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "AWA001",
            Nom = "Kone",
            Prenoms = "Awa",
            Email = "awa.kone@cit.test",
            MotDePasse = "hash",
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        });
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.SetReturnsDefault(Task.CompletedTask);
        var service = new DemandeCreationCompteWorkflowService(db, email.Object);

        var result = await service.SoumettreAsync(new SoumettreDemandeCreationCompteInput(
            "Kone",
            "Awa",
            "AWA.KONE@cit.test",
            direction.Id,
            "Comptabilite",
            directeurMetier.Id));

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("email");
        (await db.DemandesCreationCompte.CountAsync()).Should().Be(0);

        email.Verify(e => e.EnvoyerDemandeCreationCompteAuDMAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SoumettreAsync_DirecteurMetierHorsDirection_DoitRefuserSansCreerDeDemande()
    {
        await using var db = CreateDbContext();
        var directionDemandee = CreateDirection("FIN", "Finance");
        var autreDirection = CreateDirection("DSI", "Systemes d'information");
        var directeurMetier = CreateUtilisateurDm(autreDirection.Id);
        db.Directions.AddRange(directionDemandee, autreDirection);
        db.Utilisateurs.Add(directeurMetier);
        db.UtilisateurRoles.Add(CreateRole(directeurMetier.Id, RoleUtilisateur.DirecteurMetier));
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.SetReturnsDefault(Task.CompletedTask);
        var service = new DemandeCreationCompteWorkflowService(db, email.Object);

        var result = await service.SoumettreAsync(new SoumettreDemandeCreationCompteInput(
            "Kone",
            "Awa",
            "awa.kone@cit.test",
            directionDemandee.Id,
            "Comptabilite",
            directeurMetier.Id));

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Directeur Metier");
        (await db.DemandesCreationCompte.CountAsync()).Should().Be(0);
    }

    private static async Task<(Direction Direction, Utilisateur DirecteurMetier)> SeedDirectionAndDmAsync(ApplicationDbContext db)
    {
        var direction = CreateDirection("FIN", "Finance");
        var directeurMetier = CreateUtilisateurDm(direction.Id);
        db.Directions.Add(direction);
        db.Utilisateurs.Add(directeurMetier);
        db.UtilisateurRoles.Add(CreateRole(directeurMetier.Id, RoleUtilisateur.DirecteurMetier));
        await db.SaveChangesAsync();
        return (direction, directeurMetier);
    }

    private static Direction CreateDirection(string code, string libelle)
    {
        return new Direction
        {
            Id = Guid.NewGuid(),
            Code = code,
            Libelle = libelle,
            EstActive = true,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
    }

    private static Utilisateur CreateUtilisateurDm(Guid directionId)
    {
        return new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DM001",
            Nom = "Dupont",
            Prenoms = "Marie",
            Email = "dm@cit.test",
            MotDePasse = "hash",
            DirectionId = directionId,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
    }

    private static UtilisateurRole CreateRole(Guid utilisateurId, RoleUtilisateur role)
    {
        return new UtilisateurRole
        {
            Id = Guid.NewGuid(),
            UtilisateurId = utilisateurId,
            Role = role,
            DateDebut = DateTime.Now.AddDays(-1),
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
