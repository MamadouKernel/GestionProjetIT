using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GestionProjects.Tests.Unit.Administration;

public sealed class DemandeCompteAdminServiceTests
{
    [Fact]
    public async Task RefuserDmAsync_DemandeDejaValideeParDm_DoitRefuserSansChangerLeStatut()
    {
        await using var db = CreateDbContext();
        var (direction, directeurMetier) = await SeedBaseDataAsync(db);
        var demande = CreateDemande(direction.Id, directeurMetier.Id, StatutDemandeCompte.ValideeParDM);
        db.DemandesCreationCompte.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.RefuserDmAsync(
            demande.Id,
            "Trop tard",
            directeurMetier.Id,
            hasFullScope: false,
            nomActeur: "DM");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        var reload = await db.DemandesCreationCompte.SingleAsync(d => d.Id == demande.Id);
        reload.Statut.Should().Be(StatutDemandeCompte.ValideeParDM);
        reload.CommentaireDM.Should().BeNull();

        fixture.Email.Verify(e => e.EnvoyerRefusCreationCompteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RefuserDsiAsync_DemandeNonValideeParDm_DoitRefuserSansChangerLeStatut()
    {
        await using var db = CreateDbContext();
        var (direction, directeurMetier) = await SeedBaseDataAsync(db);
        var demande = CreateDemande(direction.Id, directeurMetier.Id, StatutDemandeCompte.EnAttenteValidationDM);
        db.DemandesCreationCompte.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.RefuserDsiAsync(
            demande.Id,
            "Refus premature",
            nomActeur: "DSI");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        var reload = await db.DemandesCreationCompte.SingleAsync(d => d.Id == demande.Id);
        reload.Statut.Should().Be(StatutDemandeCompte.EnAttenteValidationDM);
        reload.CommentaireDSI.Should().BeNull();

        fixture.Email.Verify(e => e.EnvoyerRefusCreationCompteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    private static ServiceFixture CreateService(ApplicationDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.Matricule).Returns("ADMIN001");

        var audit = new Mock<IAuditService>();
        audit.SetReturnsDefault(Task.CompletedTask);

        var email = new Mock<IEmailService>();
        email.SetReturnsDefault(Task.CompletedTask);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmtpSettings:BaseUrl"] = "https://app.test"
            })
            .Build();

        var service = new DemandeCompteAdminService(
            db,
            currentUser.Object,
            audit.Object,
            email.Object,
            new PasswordSetupTokenService(db, configuration),
            configuration);

        return new ServiceFixture(service, email);
    }

    private static async Task<(Direction Direction, Utilisateur DirecteurMetier)> SeedBaseDataAsync(ApplicationDbContext db)
    {
        var direction = new Direction
        {
            Id = Guid.NewGuid(),
            Code = "FIN",
            Libelle = "Finance",
            EstActive = true,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        var directeurMetier = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DM001",
            Nom = "Dupont",
            Prenoms = "Marie",
            Email = "dm@cit.test",
            MotDePasse = "hash",
            DirectionId = direction.Id,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        db.Directions.Add(direction);
        db.Utilisateurs.Add(directeurMetier);
        await db.SaveChangesAsync();
        return (direction, directeurMetier);
    }

    private static DemandeCreationCompte CreateDemande(
        Guid directionId,
        Guid directeurMetierId,
        StatutDemandeCompte statut)
    {
        return new DemandeCreationCompte
        {
            Id = Guid.NewGuid(),
            Nom = "Kone",
            Prenoms = "Awa",
            Email = "awa.kone@cit.test",
            Service = "Comptabilite",
            DirectionId = directionId,
            DirecteurMetierId = directeurMetierId,
            Statut = statut,
            DateSoumission = DateTime.Now,
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

    private sealed record ServiceFixture(
        DemandeCompteAdminService Service,
        Mock<IEmailService> Email);
}
