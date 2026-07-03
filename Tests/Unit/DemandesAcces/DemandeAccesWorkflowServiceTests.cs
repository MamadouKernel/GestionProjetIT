using FluentAssertions;
using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GestionProjects.Tests.Unit.DemandesAcces;

public sealed class DemandeAccesWorkflowServiceTests
{
    private static async Task<Guid> SeedDirectionAvecDmAsync(ApplicationDbContext db, string code = "DIR")
    {
        var directionId = Guid.NewGuid();
        db.Directions.Add(new Direction { Id = directionId, Code = code, Libelle = $"Direction {code}", EstActive = true });
        var dmId = Guid.NewGuid();
        db.Utilisateurs.Add(new Utilisateur
        {
            Id = dmId, Matricule = $"DM-{code}", MotDePasse = "x",
            Nom = "DM", Prenoms = code, Email = $"dm-{code}@cit.test",
            DirectionId = directionId
        });
        db.UtilisateurRoles.Add(new UtilisateurRole
        {
            Id = Guid.NewGuid(), UtilisateurId = dmId, Role = RoleUtilisateur.DirecteurMetier,
            DateDebut = DateTime.Now
        });
        await db.SaveChangesAsync();
        return directionId;
    }

    [Fact]
    public async Task SoumettreDemandeLocaleAsync_DoitCreerDemandeEtNotifierDmSeul()
    {
        await using var db = CreateDbContext();
        var directionId = await SeedDirectionAvecDmAsync(db);
        var fixture = CreateService(db);

        var result = await fixture.Service.SoumettreDemandeLocaleAsync(new SoumettreDemandeAccesLocaleInput(
            "  Kouadio  ",
            "  Raissa  ",
            "  raissa.kouadio@cit.test  ",
            "  2414  ",
            directionId,
            "  Demandeur  ",
            "  Besoin d'acces au portail  "));

        result.Succeeded.Should().BeTrue();
        result.FocusId.Should().NotBeNull();

        var demande = await db.DemandesAccesAzureAd.SingleAsync();
        demande.Nom.Should().Be("Kouadio");
        demande.Prenoms.Should().Be("Raissa");
        demande.Email.Should().Be("raissa.kouadio@cit.test");
        demande.Matricule.Should().Be("2414");
        demande.DirectionDetecteeId.Should().Be(directionId);
        demande.AzureDepartment.Should().Be(AccessRequestConstants.LocalAzureDepartment);
        demande.Statut.Should().Be(StatutDemandeAcces.EnAttente); // = en attente DM
        demande.CreePar.Should().Be("ANONYMOUS");
        demande.Justification.Should().Contain("Demandeur");
        demande.Justification.Should().Contain("Besoin d'acces au portail");

        // Nouveau workflow : a la soumission, seul le DM est notifie (pas l'AdminIT).
        fixture.Notification.Verify(n => n.NotifierUtilisateurAsync(
            It.IsAny<Guid>(),
            TypeNotification.DemandeSupportTechnique,
            It.Is<string>(titre => titre.Contains("Validation d'accès")),
            It.IsAny<string>(),
            DomainEntityTypes.DemandeAccesAzureAd,
            demande.Id,
            It.IsAny<object?>()), Times.AtLeastOnce);
        fixture.Notification.Verify(n => n.NotifierRoleAsync(
            RoleUtilisateur.AdminIT,
            It.IsAny<TypeNotification>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(),
            It.IsAny<object?>()), Times.Never);
    }

    [Fact]
    public async Task SoumettreDemandeLocaleAsync_DirectionSansDm_DoitRefuser()
    {
        await using var db = CreateDbContext();
        var directionSansDmId = Guid.NewGuid();
        db.Directions.Add(new Direction { Id = directionSansDmId, Code = "ORPHAN", Libelle = "Direction Orpheline", EstActive = true });
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.SoumettreDemandeLocaleAsync(new SoumettreDemandeAccesLocaleInput(
            "Test", "Test", "test@cit.test", "T1", directionSansDmId, "Demandeur", null));

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Directeur Métier");
        (await db.DemandesAccesAzureAd.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SoumettreDemandeLocaleAsync_DoublonEnAttente_DoitRetournerInfoSansNotifier()
    {
        await using var db = CreateDbContext();
        var directionId = await SeedDirectionAvecDmAsync(db);
        db.DemandesAccesAzureAd.Add(CreateDemandeAcces(
            email: "raissa.kouadio@cit.test",
            matricule: "2414",
            azureDepartment: AccessRequestConstants.LocalAzureDepartment));
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.SoumettreDemandeLocaleAsync(new SoumettreDemandeAccesLocaleInput(
            "Kouadio",
            "Raissa",
            "raissa.kouadio@cit.test",
            "9999",
            directionId,
            "Demandeur",
            null));

        result.Succeeded.Should().BeFalse();
        result.InfoMessage.Should().NotBeNullOrWhiteSpace();
        (await db.DemandesAccesAzureAd.CountAsync()).Should().Be(1);

        fixture.Notification.Verify(n => n.NotifierRoleAsync(
            It.IsAny<RoleUtilisateur>(),
            It.IsAny<TypeNotification>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>()), Times.Never);
    }

    [Fact]
    public async Task ApprouverAsync_DemandeLocaleNouvelUtilisateur_DoitCreerCompteRoleTokenEtEmailActivation()
    {
        await using var db = CreateDbContext();
        var direction = CreateDirection();
        var traiteParId = Guid.NewGuid();
        var demande = CreateDemandeAcces(
            email: "raissa.kouadio@cit.test",
            matricule: "2414",
            azureDepartment: AccessRequestConstants.LocalAzureDepartment,
            directionId: direction.Id,
            statut: StatutDemandeAcces.ApprouveeParDm);
        db.Directions.Add(direction);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();

        var tokenExpiration = DateTime.Now.AddHours(2);
        var fixture = CreateService(db, token: new PasswordSetupTokenCreation("token-activation", tokenExpiration));

        var result = await fixture.Service.ApprouverAsync(new ApprouverDemandeAccesInput(
            demande.Id,
            direction.Id,
            "OK",
            RoleUtilisateur.ChefDeProjet,
            traiteParId));

        result.Succeeded.Should().BeTrue();

        var utilisateur = await db.Utilisateurs.SingleAsync(u => u.Matricule == "2414");
        utilisateur.Email.Should().Be("raissa.kouadio@cit.test");
        utilisateur.DirectionId.Should().Be(direction.Id);
        utilisateur.MotDePasse.Should().BeEmpty();
        utilisateur.PeutCreerDemandeProjet.Should().BeTrue();

        (await db.UtilisateurRoles.AnyAsync(ur =>
            ur.UtilisateurId == utilisateur.Id &&
            ur.Role == RoleUtilisateur.ChefDeProjet &&
            !ur.EstSupprime)).Should().BeTrue();

        var demandeTraitee = await db.DemandesAccesAzureAd.SingleAsync(d => d.Id == demande.Id);
        demandeTraitee.Statut.Should().Be(StatutDemandeAcces.Approuvee);
        demandeTraitee.UtilisateurCreeId.Should().Be(utilisateur.Id);
        demandeTraitee.TraiteParId.Should().Be(traiteParId);

        fixture.PasswordSetupToken.Verify(p => p.CreerAsync(utilisateur.Id, "ADMIN001", false), Times.Once);
        fixture.Email.Verify(e => e.EnvoyerActivationCompteAsync(
            "raissa.kouadio@cit.test",
            It.IsAny<string>(),
            "2414",
            It.Is<string>(lien => lien.Contains("https://app.test") && lien.Contains("token-activation")),
            tokenExpiration), Times.Once);
        fixture.Audit.Verify(a => a.LogActionAsync(
            "APPROBATION_DEMANDE_ACCES_AZURE_AD",
            DomainEntityTypes.DemandeAccesAzureAd,
            demande.Id,
            null,
            It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task ApprouverAsync_DemandeMicrosoftAvecMatriculeExistant_DoitReutiliserUtilisateurSansActivationLocale()
    {
        await using var db = CreateDbContext();
        var utilisateurExistant = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "2414",
            Nom = "Ancien",
            Prenoms = "Compte",
            Email = "ancienne.adresse@cit.test",
            MotDePasse = "hash",
            CreePar = "TEST"
        };
        var demande = CreateDemandeAcces(
            email: "raissa.kouadio@cit.test",
            matricule: "2414",
            azureDepartment: "DSI",
            statut: StatutDemandeAcces.ApprouveeParDm);
        db.Utilisateurs.Add(utilisateurExistant);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.ApprouverAsync(new ApprouverDemandeAccesInput(
            demande.Id,
            null,
            "OK",
            RoleUtilisateur.Demandeur,
            Guid.NewGuid()));

        result.Succeeded.Should().BeTrue();
        (await db.Utilisateurs.CountAsync(u => u.Matricule == "2414")).Should().Be(1);

        var demandeTraitee = await db.DemandesAccesAzureAd.SingleAsync(d => d.Id == demande.Id);
        demandeTraitee.Statut.Should().Be(StatutDemandeAcces.Approuvee);
        demandeTraitee.UtilisateurCreeId.Should().Be(utilisateurExistant.Id);

        fixture.PasswordSetupToken.Verify(p => p.CreerAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        fixture.Email.Verify(e => e.EnvoyerActivationCompteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>()), Times.Never);
        fixture.Email.Verify(e => e.SendEmailAsync(
            "raissa.kouadio@cit.test",
            It.Is<string>(subject => subject.Contains("approuv")),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RejeterAsync_SansCommentaire_DoitRetournerErreurSansModifierLaDemande()
    {
        await using var db = CreateDbContext();
        var demande = CreateDemandeAcces(
            email: "raissa.kouadio@cit.test",
            matricule: "2414",
            azureDepartment: AccessRequestConstants.LocalAzureDepartment);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.RejeterAsync(new RejeterDemandeAccesInput(
            demande.Id,
            "   ",
            Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("obligatoire");

        var reload = await db.DemandesAccesAzureAd.SingleAsync(d => d.Id == demande.Id);
        reload.Statut.Should().Be(StatutDemandeAcces.EnAttente);
        reload.DateTraitement.Should().BeNull();

        fixture.Audit.Verify(a => a.LogActionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>()), Times.Never);
        fixture.Email.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RejeterAsync_AvecCommentaire_DoitMarquerRejeteeEtEnvoyerEmail()
    {
        await using var db = CreateDbContext();
        var traiteParId = Guid.NewGuid();
        var demande = CreateDemandeAcces(
            email: "raissa.kouadio@cit.test",
            matricule: "2414",
            azureDepartment: AccessRequestConstants.LocalAzureDepartment);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.RejeterAsync(new RejeterDemandeAccesInput(
            demande.Id,
            "Profil non eligible",
            traiteParId));

        result.Succeeded.Should().BeTrue();

        var reload = await db.DemandesAccesAzureAd.SingleAsync(d => d.Id == demande.Id);
        reload.Statut.Should().Be(StatutDemandeAcces.Rejetee);
        reload.CommentaireTraitement.Should().Be("Profil non eligible");
        reload.TraiteParId.Should().Be(traiteParId);
        reload.DateTraitement.Should().NotBeNull();

        fixture.Audit.Verify(a => a.LogActionAsync(
            "REJET_DEMANDE_ACCES_AZURE_AD",
            DomainEntityTypes.DemandeAccesAzureAd,
            demande.Id,
            null,
            It.IsAny<object?>()), Times.Once);
        fixture.Email.Verify(e => e.SendEmailAsync(
            "raissa.kouadio@cit.test",
            It.Is<string>(subject => subject.Contains("refus")),
            It.Is<string>(body => body.Contains("Profil non eligible")),
            It.IsAny<string?>()), Times.Once);
    }

    // ─────────────── Validation par DM (workflow premier rang) ───────────────

    [Fact]
    public async Task ValiderParDmAsync_DmDeLaDirection_DoitPasserEnApprouveeParDm()
    {
        await using var db = CreateDbContext();
        var directionId = await SeedDirectionAvecDmAsync(db);
        var dmId = await db.Utilisateurs.Where(u => u.DirectionId == directionId).Select(u => u.Id).FirstAsync();
        var demande = CreateDemandeAcces("anne.mobio@cit.test", "2518", AccessRequestConstants.LocalAzureDepartment, directionId);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.ValiderParDmAsync(
            new ValiderDemandeAccesParDmInput(demande.Id, dmId, RoleUtilisateur.ChefDeProjet, "OK"));

        result.Succeeded.Should().BeTrue();
        var reload = await db.DemandesAccesAzureAd.SingleAsync();
        reload.Statut.Should().Be(StatutDemandeAcces.ApprouveeParDm);
        reload.ValideeParDmId.Should().Be(dmId);
        reload.RoleConfirmeParDm.Should().Be("ChefDeProjet");
    }

    [Fact]
    public async Task ValiderParDmAsync_DmDuneAutreDirection_DoitRefuser()
    {
        await using var db = CreateDbContext();
        var directionDemande = await SeedDirectionAvecDmAsync(db, "DIR1");
        var directionAutre   = await SeedDirectionAvecDmAsync(db, "DIR2");
        var dmAutreId = await db.Utilisateurs.Where(u => u.DirectionId == directionAutre).Select(u => u.Id).FirstAsync();
        var demande = CreateDemandeAcces("x@cit.test", "X1", AccessRequestConstants.LocalAzureDepartment, directionDemande);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.ValiderParDmAsync(
            new ValiderDemandeAccesParDmInput(demande.Id, dmAutreId, RoleUtilisateur.Demandeur, null));

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Directeur Metier");
        (await db.DemandesAccesAzureAd.SingleAsync()).Statut.Should().Be(StatutDemandeAcces.EnAttente);
    }

    [Fact]
    public async Task ValiderParDmAsync_VerrouSiDejaTraiteeParUnAutreDm()
    {
        await using var db = CreateDbContext();
        var directionId = await SeedDirectionAvecDmAsync(db);
        // 2e DM rattache a la meme direction
        var dm1Id = await db.Utilisateurs.Where(u => u.DirectionId == directionId).Select(u => u.Id).FirstAsync();
        var dm2Id = Guid.NewGuid();
        db.Utilisateurs.Add(new Utilisateur { Id = dm2Id, Matricule = "DM2", MotDePasse = "x", Nom = "DM2", Prenoms = "X", Email = "dm2@cit.test", DirectionId = directionId });
        db.UtilisateurRoles.Add(new UtilisateurRole { Id = Guid.NewGuid(), UtilisateurId = dm2Id, Role = RoleUtilisateur.DirecteurMetier, DateDebut = DateTime.Now });
        var demande = CreateDemandeAcces("y@cit.test", "Y1", AccessRequestConstants.LocalAzureDepartment, directionId);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        // DM1 valide en premier
        (await fixture.Service.ValiderParDmAsync(
            new ValiderDemandeAccesParDmInput(demande.Id, dm1Id, RoleUtilisateur.Demandeur, null))).Succeeded.Should().BeTrue();

        // DM2 essaye de re-traiter -> verrou
        var result = await fixture.Service.ValiderParDmAsync(
            new ValiderDemandeAccesParDmInput(demande.Id, dm2Id, RoleUtilisateur.ChefDeProjet, null));

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("deja ete traitee");
    }

    [Fact]
    public async Task RejeterParDmAsync_AvecMotif_DoitInformerLeDemandeur()
    {
        await using var db = CreateDbContext();
        var directionId = await SeedDirectionAvecDmAsync(db);
        var dmId = await db.Utilisateurs.Where(u => u.DirectionId == directionId).Select(u => u.Id).FirstAsync();
        var demande = CreateDemandeAcces("z@cit.test", "Z1", AccessRequestConstants.LocalAzureDepartment, directionId);
        db.DemandesAccesAzureAd.Add(demande);
        await db.SaveChangesAsync();
        var fixture = CreateService(db);

        var result = await fixture.Service.RejeterParDmAsync(
            new RejeterDemandeAccesParDmInput(demande.Id, dmId, "Identite non confirmee"));

        result.Succeeded.Should().BeTrue();
        (await db.DemandesAccesAzureAd.SingleAsync()).Statut.Should().Be(StatutDemandeAcces.RejeteeParDm);
        // Le demandeur recoit un email (workflow terminal cote DM).
        fixture.Email.Verify(e => e.EnvoyerAsync(
            "z@cit.test",
            It.Is<string>(s => s.Contains("refus")),
            It.IsAny<string>()), Times.Once);
    }

    private static WorkflowFixture CreateService(
        ApplicationDbContext db,
        PasswordSetupTokenCreation? token = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.Matricule).Returns("ADMIN001");
        currentUser.SetupGet(c => c.Roles).Returns(new[] { RoleUtilisateur.AdminIT.ToString() });

        var audit = new Mock<IAuditService>();
        audit
            .Setup(a => a.LogActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .Returns(Task.CompletedTask);

        var email = new Mock<IEmailService>();
        email.SetReturnsDefault(Task.CompletedTask);

        var notification = new Mock<INotificationService>();
        notification.SetReturnsDefault(Task.CompletedTask);

        var passwordSetupToken = new Mock<IPasswordSetupTokenService>();
        passwordSetupToken
            .Setup(p => p.CreerAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(token ?? new PasswordSetupTokenCreation("token", DateTime.Now.AddHours(1)));
        passwordSetupToken
            .Setup(p => p.InitialiserMotDePasseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Success("OK"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmtpSettings:BaseUrl"] = "https://app.test"
            })
            .Build();

        var service = new DemandeAccesWorkflowService(
            db,
            currentUser.Object,
            audit.Object,
            email.Object,
            notification.Object,
            passwordSetupToken.Object,
            new UtilisateurIdentityResolver(db),
            configuration);

        return new WorkflowFixture(service, audit, email, notification, passwordSetupToken);
    }

    private static DemandeAccesAzureAd CreateDemandeAcces(
        string email,
        string matricule,
        string azureDepartment,
        Guid? directionId = null,
        StatutDemandeAcces statut = StatutDemandeAcces.EnAttente)
    {
        return new DemandeAccesAzureAd
        {
            Id = Guid.NewGuid(),
            Nom = "Kouadio",
            Prenoms = "Raissa",
            Email = email,
            Matricule = matricule,
            AzureDepartment = azureDepartment,
            DirectionDetecteeId = directionId,
            Justification = "Demande d'acces",
            Statut = statut,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
    }

    private static Direction CreateDirection()
    {
        return new Direction
        {
            Id = Guid.NewGuid(),
            Code = "DSI",
            Libelle = "Direction des systemes d'information",
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

    private sealed record WorkflowFixture(
        DemandeAccesWorkflowService Service,
        Mock<IAuditService> Audit,
        Mock<IEmailService> Email,
        Mock<INotificationService> Notification,
        Mock<IPasswordSetupTokenService> PasswordSetupToken);
}
