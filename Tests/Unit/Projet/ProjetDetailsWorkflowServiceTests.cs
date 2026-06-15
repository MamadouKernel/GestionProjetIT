using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Moq;
using Xunit;
using ProjetModel = GestionProjects.Domain.Models.Projet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests du GET Details extrait vers ProjetDetailsWorkflowService.BuildDetailsViewModelAsync :
/// chargements conditionnels par onglet, liste des chefs réassignables, audit de prise en charge.
/// </summary>
public class ProjetDetailsWorkflowServiceTests
{
    private static (ApplicationDbContext ctx, ProjetDetailsWorkflowService svc, Mock<IAuditService> audit) CreateSut(string dbName)
    {
        var ctx = TestDbContextFactory.CreateInMemoryContext(dbName);
        var audit = new Mock<IAuditService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.Matricule).Returns("TEST");

        var svc = new ProjetDetailsWorkflowService(ctx, audit.Object, currentUser.Object);
        return (ctx, svc, audit);
    }

    private static ProjetModel SeedProjet(ApplicationDbContext ctx)
    {
        var projet = new ProjetModel
        {
            Id = Guid.NewGuid(),
            Titre = "Projet Beta",
            CodeProjet = "BET-001",
            DateCreation = DateTime.Now,
            CreePar = "T"
        };
        ctx.Projets.Add(projet);
        ctx.SaveChanges();
        return projet;
    }

    [Fact]
    public async Task BuildDetails_OngletHistorique_ChargeLesAuditLogs()
    {
        var (ctx, svc, _) = CreateSut(nameof(BuildDetails_OngletHistorique_ChargeLesAuditLogs));
        var projet = SeedProjet(ctx);
        ctx.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Entite = "Projet",
            EntiteId = projet.Id.ToString(),
            TypeAction = "CREATION_PROJET",
            DateAction = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "T"
        });
        await ctx.SaveChangesAsync();

        var vmHistorique = await svc.BuildDetailsViewModelAsync(projet, Guid.NewGuid(), "historique", false, false, false, false);
        var vmSynthese = await svc.BuildDetailsViewModelAsync(projet, Guid.NewGuid(), "synthese", false, false, false, false);

        vmHistorique.AuditLogs.Should().ContainSingle();
        vmSynthese.AuditLogs.Should().BeEmpty(); // chargé seulement sur l'onglet historique
    }

    [Fact]
    public async Task BuildDetails_ChefAssigne_JournaliseLaPriseEnCharge_SiAbsente()
    {
        var (ctx, svc, audit) = CreateSut(nameof(BuildDetails_ChefAssigne_JournaliseLaPriseEnCharge_SiAbsente));
        var projet = SeedProjet(ctx);
        var userId = Guid.NewGuid();

        await svc.BuildDetailsViewModelAsync(projet, userId, "synthese", false, false, isAssignedChefProjet: true, false);

        audit.Verify(a => a.LogActionAsync("PRISE_EN_CHARGE_PROJET", "Projet", projet.Id,
            It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task DemarrerProjet_NonDemarreAssigne_DoitPasserEnCoursSansAvancementArtificiel()
    {
        var (ctx, svc, audit) = CreateSut(nameof(DemarrerProjet_NonDemarreAssigne_DoitPasserEnCoursSansAvancementArtificiel));
        var chefProjetId = Guid.NewGuid();
        var projet = SeedProjet(ctx);
        projet.ChefProjetId = chefProjetId;
        projet.StatutProjet = StatutProjet.NonDemarre;
        projet.PhaseActuelle = PhaseProjet.AnalyseClarification;
        projet.PourcentageAvancement = 10;
        await ctx.SaveChangesAsync();

        var result = await svc.DemarrerProjetAsync(projet.Id, chefProjetId);

        result.Succeeded.Should().BeTrue();
        var reload = await ctx.Projets.FindAsync(projet.Id);
        reload!.StatutProjet.Should().Be(StatutProjet.EnCours);
        reload.PourcentageAvancement.Should().Be(0);
        reload.DateDebut.Should().NotBeNull();

        ctx.HistoriquePhasesProjets.Should().ContainSingle(h =>
            h.ProjetId == projet.Id &&
            h.Phase == PhaseProjet.AnalyseClarification &&
            h.StatutProjet == StatutProjet.EnCours);

        audit.Verify(a => a.LogActionAsync("DEMARRAGE_PROJET", "Projet", projet.Id,
            It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task DemarrerProjet_SansResponsableSolutionsIT_DoitRefuser()
    {
        var (ctx, svc, _) = CreateSut(nameof(DemarrerProjet_SansResponsableSolutionsIT_DoitRefuser));
        var projet = SeedProjet(ctx);
        projet.StatutProjet = StatutProjet.NonDemarre;
        projet.PhaseActuelle = PhaseProjet.AnalyseClarification;
        projet.ChefProjetId = null;
        await ctx.SaveChangesAsync();

        var result = await svc.DemarrerProjetAsync(projet.Id, Guid.NewGuid());

        result.ErrorMessage.Should().Contain("ResponsableSolutionsIT");
        var reload = await ctx.Projets.FindAsync(projet.Id);
        reload!.StatutProjet.Should().Be(StatutProjet.NonDemarre);
        reload.DateDebut.Should().BeNull();
    }

    [Fact]
    public async Task BuildDetails_PeutReassigner_ChargeLesChefsDeProjet()
    {
        var (ctx, svc, _) = CreateSut(nameof(BuildDetails_PeutReassigner_ChargeLesChefsDeProjet));
        var projet = SeedProjet(ctx);

        var chef = new Utilisateur
        {
            Id = Guid.NewGuid(), Matricule = "CP1", MotDePasse = "x",
            Nom = "Chef", Prenoms = "Projet", Email = "cp@t.ci",
            DateCreation = DateTime.Now, CreePar = "T"
        };
        ctx.Utilisateurs.Add(chef);
        ctx.UtilisateurRoles.Add(new UtilisateurRole
        {
            Id = Guid.NewGuid(), UtilisateurId = chef.Id, Role = RoleUtilisateur.ChefDeProjet,
            DateDebut = DateTime.Now, DateCreation = DateTime.Now, CreePar = "T"
        });
        await ctx.SaveChangesAsync();

        var vmAvec = await svc.BuildDetailsViewModelAsync(projet, Guid.NewGuid(), "synthese", false, canReassignChefProjet: true, false, false);
        var vmSans = await svc.BuildDetailsViewModelAsync(projet, Guid.NewGuid(), "synthese", false, canReassignChefProjet: false, false, false);

        vmAvec.ChefsProjet.Should().ContainSingle(u => u.Id == chef.Id);
        vmSans.ChefsProjet.Should().BeEmpty();
    }
}
