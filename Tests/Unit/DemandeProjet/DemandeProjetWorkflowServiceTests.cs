using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DemandeProjetEntity = GestionProjects.Domain.Models.DemandeProjet;

namespace GestionProjects.Tests.Unit.DemandeProjet;

public class DemandeProjetWorkflowServiceTests
{
    [Fact]
    public async Task SoumettreAsync_NeDoitPasCreerPortefeuilleAutomatiquement()
    {
        await using var db = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString());
        var demande = await CreerDemandeAsync(db, StatutDemande.Brouillon);
        var service = CreerService(db);

        var result = await service.SoumettreAsync(demande.Id, demande.DemandeurId, hasAdminScope: false, ignorerDoublons: true);

        result.ErrorMessage.Should().BeNull();
        result.SuccessMessage.Should().NotBeNullOrWhiteSpace();
        (await db.PortefeuillesProjets.CountAsync()).Should().Be(0);

        var reload = await db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDirecteurMetier);
    }

    [Fact]
    public async Task ValiderDsiAsync_SansPortefeuilleActif_DoitRefuserSansModifierLaDemande()
    {
        await using var db = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString());
        var demande = await CreerDemandeAsync(db, StatutDemande.EnAttenteValidationDSI);
        var service = CreerService(db);

        var result = await service.ValiderDsiAsync(demande.Id, commentaire: null, chefProjetId: null, currentUserId: Guid.NewGuid(), isDelegue: false, nomActeur: "DSI");

        result.ErrorMessage.Should().Contain("Aucun portefeuille actif");
        result.ProjetId.Should().BeNull();
        (await db.Projets.CountAsync()).Should().Be(0);

        var reload = await db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.EnAttenteValidationDSI);
        reload.DateValidationDSI.Should().BeNull();
    }

    [Fact]
    public async Task ValiderDsiAsync_AvecPortefeuilleActif_DoitCreerProjetDansCePortefeuille()
    {
        await using var db = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString());
        var demande = await CreerDemandeAsync(db, StatutDemande.EnAttenteValidationDSI);
        var portefeuille = new PortefeuilleProjet
        {
            Id = Guid.NewGuid(),
            Nom = "Portefeuille actif",
            ObjectifStrategiqueGlobal = "Objectif",
            AvantagesAttendus = "Avantages",
            RisquesEtMitigations = "Risques",
            EstActif = true,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        db.PortefeuillesProjets.Add(portefeuille);
        await db.SaveChangesAsync();
        var service = CreerService(db);

        var result = await service.ValiderDsiAsync(demande.Id, commentaire: "OK", chefProjetId: null, currentUserId: Guid.NewGuid(), isDelegue: false, nomActeur: "DSI");

        result.ErrorMessage.Should().BeNull();
        result.ProjetId.Should().NotBeNull();

        var projet = await db.Projets.SingleAsync();
        projet.PortefeuilleProjetId.Should().Be(portefeuille.Id);
        projet.StatutProjet.Should().Be(StatutProjet.NonDemarre);
        projet.PhaseActuelle.Should().Be(PhaseProjet.AnalyseClarification);

        var reload = await db.DemandesProjets.FindAsync(demande.Id);
        reload!.StatutDemande.Should().Be(StatutDemande.ValideeParDSI);
        reload.DateValidationDSI.Should().NotBeNull();
    }

    private static DemandeProjetWorkflowService CreerService(ApplicationDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.Matricule).Returns("DSI001");
        currentUser.SetupGet(u => u.Roles).Returns(Array.Empty<string>());

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .Returns(Task.CompletedTask);

        var teams = new Mock<ITeamsNotificationService>();
        teams.SetReturnsDefault<Task>(Task.CompletedTask);

        var email = new Mock<IEmailService>();
        email.SetReturnsDefault<Task>(Task.CompletedTask);

        return new DemandeProjetWorkflowService(
            db,
            currentUser.Object,
            audit.Object,
            teams.Object,
            email.Object,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IDemandeProjetQueryService>());
    }

    private static async Task<DemandeProjetEntity> CreerDemandeAsync(ApplicationDbContext db, StatutDemande statut)
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

        var demandeur = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DEM001",
            Nom = "Kouassi",
            Prenoms = "Jean",
            Email = "jean.kouassi@cit.ci",
            DirectionId = direction.Id,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        var directeurMetier = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DM001",
            Nom = "Yao",
            Prenoms = "Marie",
            Email = "marie.yao@cit.ci",
            DirectionId = direction.Id,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        var demande = new DemandeProjetEntity
        {
            Id = Guid.NewGuid(),
            Titre = "Nouvelle demande",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            DemandeurId = demandeur.Id,
            DirectionId = direction.Id,
            DirecteurMetierId = directeurMetier.Id,
            StatutDemande = statut,
            CahierChargesPath = "demandes/cahier-des-charges-test.pdf",
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        db.Directions.Add(direction);
        db.Utilisateurs.AddRange(demandeur, directeurMetier);
        db.DemandesProjets.Add(demande);
        await db.SaveChangesAsync();
        return demande;
    }
}
