using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using ProjetModel = GestionProjects.Domain.Models.Projet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests de la réalisation des bénéfices : définition, revue post-implémentation, suppression.
/// </summary>
public class BeneficeProjetServiceTests
{
    private static (ApplicationDbContext ctx, BeneficeProjetService svc) CreateSut(string dbName)
    {
        var ctx = TestDbContextFactory.CreateInMemoryContext(dbName);
        var svc = new BeneficeProjetService(ctx, new Mock<IAuditService>().Object);
        return (ctx, svc);
    }

    private static ProjetModel SeedProjet(ApplicationDbContext ctx)
    {
        var projet = new ProjetModel
        {
            Id = Guid.NewGuid(), Titre = "P", CodeProjet = "BEN-001",
            DateCreation = DateTime.Now, CreePar = "T"
        };
        ctx.Projets.Add(projet);
        ctx.SaveChanges();
        return projet;
    }

    [Fact]
    public async Task Ajouter_CreeUnBeneficeAttendu()
    {
        var (ctx, svc) = CreateSut(nameof(Ajouter_CreeUnBeneficeAttendu));
        var projet = SeedProjet(ctx);

        var result = await svc.AjouterAsync(projet.Id, Guid.NewGuid(),
            "Réduction des délais", "Délai moyen (j)", "< 2j", new DateTime(2027, 6, 30));

        result.Succeeded.Should().BeTrue();
        var benefice = await ctx.BeneficesProjets.FirstAsync();
        benefice.Statut.Should().Be(StatutBenefice.Attendu);
        benefice.Libelle.Should().Be("Réduction des délais");
    }

    [Fact]
    public async Task Ajouter_SansLibelle_Refuse()
    {
        var (ctx, svc) = CreateSut(nameof(Ajouter_SansLibelle_Refuse));
        var projet = SeedProjet(ctx);

        var result = await svc.AjouterAsync(projet.Id, Guid.NewGuid(), "  ", "ind", "cible", null);

        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        (await ctx.BeneficesProjets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Evaluer_EnregistreLaRevue_EtRefuseStatutAttendu()
    {
        var (ctx, svc) = CreateSut(nameof(Evaluer_EnregistreLaRevue_EtRefuseStatutAttendu));
        var projet = SeedProjet(ctx);
        await svc.AjouterAsync(projet.Id, Guid.NewGuid(), "Bénéfice", "ind", "cible", null);
        var benefice = await ctx.BeneficesProjets.FirstAsync();

        (await svc.EvaluerAsync(benefice.Id, Guid.NewGuid(), StatutBenefice.Attendu, null, null))
            .ErrorMessage.Should().NotBeNullOrWhiteSpace();

        var result = await svc.EvaluerAsync(benefice.Id, Guid.NewGuid(),
            StatutBenefice.PartiellementRealise, "-15%", "Tendance positive");

        result.Succeeded.Should().BeTrue();
        var reload = await ctx.BeneficesProjets.FirstAsync();
        reload.Statut.Should().Be(StatutBenefice.PartiellementRealise);
        reload.ValeurRealisee.Should().Be("-15%");
        reload.DateRevue.Should().NotBeNull();
    }

    [Fact]
    public async Task Supprimer_SoftDeleteLeBenefice()
    {
        var (ctx, svc) = CreateSut(nameof(Supprimer_SoftDeleteLeBenefice));
        var projet = SeedProjet(ctx);
        await svc.AjouterAsync(projet.Id, Guid.NewGuid(), "Bénéfice", "ind", "cible", null);
        var benefice = await ctx.BeneficesProjets.FirstAsync();

        var result = await svc.SupprimerAsync(benefice.Id, Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        (await ctx.BeneficesProjets.CountAsync()).Should().Be(0); // filtre soft-delete
        (await ctx.BeneficesProjets.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }
}
