using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DemandeProjetModel = GestionProjects.Domain.Models.DemandeProjet;
using ProjetModel = GestionProjects.Domain.Models.Projet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests du GET CharteProjet extrait vers CharteProjetWorkflowService.ObtenirPourAffichageAsync :
/// get-or-create de la charte (avec ses jalons par défaut) et assemblage du view-model.
/// Adossés à SQLite : la requête de charte utilise des Include filtrés, mal supportés par
/// le provider InMemory mais corrects en SQL relationnel (le vrai contexte d'exécution).
/// </summary>
public class CharteProjetWorkflowServiceTests
{
    private static (SqliteTestDb db, CharteProjetWorkflowService svc) CreateSut()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.Matricule).Returns("TEST");

        var db = new SqliteTestDb(currentUser.Object);
        var svc = new CharteProjetWorkflowService(db.Context, currentUser.Object, new Mock<IAuditService>().Object);
        return (db, svc);
    }

    private static Utilisateur AddUser(ApplicationDbContext ctx, string matricule)
    {
        var u = new Utilisateur
        {
            Id = Guid.NewGuid(), Matricule = matricule, MotDePasse = "x",
            Nom = matricule, Prenoms = matricule, Email = $"{matricule}@t.ci"
        };
        ctx.Utilisateurs.Add(u);
        return u;
    }

    private static ProjetModel SeedProjetGraph(ApplicationDbContext ctx)
    {
        var demandeur = AddUser(ctx, "DEM");
        var dm = AddUser(ctx, "DM");
        var sponsor = AddUser(ctx, "SP");
        ctx.SaveChanges();

        var demande = new DemandeProjetModel
        {
            Id = Guid.NewGuid(), Titre = "Demande", DemandeurId = demandeur.Id, DirecteurMetierId = dm.Id
        };
        ctx.DemandesProjets.Add(demande);
        ctx.SaveChanges();

        var projet = new ProjetModel
        {
            Id = Guid.NewGuid(), Titre = "Projet Alpha", CodeProjet = "ALP-001",
            DemandeProjetId = demande.Id, SponsorId = sponsor.Id, ChefProjetId = sponsor.Id
        };
        ctx.Projets.Add(projet);
        ctx.SaveChanges();

        // Recharger avec la navigation DemandeProjet (comme le contrôleur) : la charte
        // par défaut en tire un DemandeurId réel (FK non-nullable).
        return ctx.Projets.Include(p => p.DemandeProjet).First(p => p.Id == projet.Id);
    }

    [Fact]
    public async Task ObtenirPourAffichage_CreeUneCharteParDefautAvecJalons_SiAucune()
    {
        var (db, svc) = CreateSut();
        using (db)
        {
            var projet = SeedProjetGraph(db.Context);

            var vm = await svc.ObtenirPourAffichageAsync(projet);

            vm.Charte.Should().NotBeNull();
            vm.Charte!.NomProjet.Should().Be("Projet Alpha");
            vm.Charte.Jalons.Should().HaveCount(8);
            (await db.Context.CharteProjets.IgnoreQueryFilters().CountAsync(c => c.ProjetId == projet.Id))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task ObtenirPourAffichage_RetourneLaCharteExistante_SansEnRecreer()
    {
        var (db, svc) = CreateSut();
        using (db)
        {
            var projet = SeedProjetGraph(db.Context);

            var charteExistante = new CharteProjet
            {
                Id = Guid.NewGuid(), ProjetId = projet.Id, NomProjet = "Charte déjà là",
                DemandeurId = projet.DemandeProjet!.DemandeurId, ChefProjetId = projet.ChefProjetId!.Value
            };
            db.Context.CharteProjets.Add(charteExistante);
            await db.Context.SaveChangesAsync();

            var vm = await svc.ObtenirPourAffichageAsync(projet);

            vm.Charte!.Id.Should().Be(charteExistante.Id);
            (await db.Context.CharteProjets.IgnoreQueryFilters().CountAsync(c => c.ProjetId == projet.Id))
                .Should().Be(1);
        }
    }
}
