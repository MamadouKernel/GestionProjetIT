using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using ProjetModel = GestionProjects.Domain.Models.Projet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests du workflow d'avenant (gestion du changement) : création + validations
/// Métier puis DSI (qui applique le changement au projet) + rejet.
/// </summary>
public class AvenantProjetServiceTests
{
    private static (ApplicationDbContext ctx, AvenantProjetService svc) CreateSut(string dbName)
    {
        var ctx = TestDbContextFactory.CreateInMemoryContext(dbName);
        var svc = new AvenantProjetService(ctx, new Mock<IAuditService>().Object);
        return (ctx, svc);
    }

    private static ProjetModel SeedProjet(ApplicationDbContext ctx, decimal budget, DateTime dateFin)
    {
        var projet = new ProjetModel
        {
            Id = Guid.NewGuid(),
            Titre = "Projet Avenant",
            CodeProjet = "AVN-001",
            StatutProjet = StatutProjet.EnCours,
            DateFinPrevue = dateFin,
            DateCreation = DateTime.Now,
            CreePar = "T"
        };
        ctx.Projets.Add(projet);
        ctx.FicheProjets.Add(new FicheProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            BudgetPrevisionnel = budget,
            DateCreation = DateTime.Now,
            CreePar = "T"
        });
        ctx.SaveChanges();
        return projet;
    }

    [Fact]
    public async Task Creer_AvenantBudget_CreeEnAttenteDMAvecSnapshot()
    {
        var (ctx, svc) = CreateSut(nameof(Creer_AvenantBudget_CreeEnAttenteDMAvecSnapshot));
        var projet = SeedProjet(ctx, budget: 1000m, dateFin: new DateTime(2026, 12, 31));

        var result = await svc.CreerAsync(projet.Id, Guid.NewGuid(), TypeAvenant.Budget,
            "Hausse de licence", "Coût licence revu", null, nouveauBudget: 1500m, nouvelleDateFinPrevue: null);

        result.Succeeded.Should().BeTrue();
        var avenant = await ctx.AvenantsProjets.FirstAsync();
        avenant.Statut.Should().Be(StatutAvenant.EnAttenteValidationDM);
        avenant.Numero.Should().Be(1);
        avenant.AncienBudget.Should().Be(1000m);
        avenant.NouveauBudget.Should().Be(1500m);
    }

    [Fact]
    public async Task Creer_AvenantBudgetSansMontant_Refuse()
    {
        var (ctx, svc) = CreateSut(nameof(Creer_AvenantBudgetSansMontant_Refuse));
        var projet = SeedProjet(ctx, 1000m, new DateTime(2026, 12, 31));

        var result = await svc.CreerAsync(projet.Id, Guid.NewGuid(), TypeAvenant.Budget,
            "Sans montant", "Justif", null, nouveauBudget: null, nouvelleDateFinPrevue: null);

        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        (await ctx.AvenantsProjets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Workflow_DM_puis_DSI_AppliqueLesChangementsAuProjet()
    {
        var (ctx, svc) = CreateSut(nameof(Workflow_DM_puis_DSI_AppliqueLesChangementsAuProjet));
        var projet = SeedProjet(ctx, 1000m, new DateTime(2026, 12, 31));
        var nouvelleDate = new DateTime(2027, 3, 31);

        await svc.CreerAsync(projet.Id, Guid.NewGuid(), TypeAvenant.Mixte,
            "Extension périmètre", "Nouveau module", "Ajout du module reporting", 2000m, nouvelleDate);
        var avenant = await ctx.AvenantsProjets.FirstAsync();

        (await svc.ValiderDmAsync(avenant.Id, Guid.NewGuid())).Succeeded.Should().BeTrue();
        (await ctx.AvenantsProjets.FirstAsync()).Statut.Should().Be(StatutAvenant.EnAttenteValidationDSI);

        (await svc.ValiderDsiAsync(avenant.Id, Guid.NewGuid())).Succeeded.Should().BeTrue();

        var avenantFinal = await ctx.AvenantsProjets.FirstAsync();
        avenantFinal.Statut.Should().Be(StatutAvenant.Applique);

        var projetReload = await ctx.Projets.FindAsync(projet.Id);
        projetReload!.DateFinPrevue.Should().Be(nouvelleDate);
        var fiche = await ctx.FicheProjets.FirstAsync(f => f.ProjetId == projet.Id);
        fiche.BudgetPrevisionnel.Should().Be(2000m);
    }

    [Fact]
    public async Task Rejeter_SansMotif_Refuse_AvecMotif_PasseEnRejete()
    {
        var (ctx, svc) = CreateSut(nameof(Rejeter_SansMotif_Refuse_AvecMotif_PasseEnRejete));
        var projet = SeedProjet(ctx, 1000m, new DateTime(2026, 12, 31));
        await svc.CreerAsync(projet.Id, Guid.NewGuid(), TypeAvenant.Delai,
            "Glissement", "Retard fournisseur", null, null, new DateTime(2027, 1, 31));
        var avenant = await ctx.AvenantsProjets.FirstAsync();

        (await svc.RejeterAsync(avenant.Id, Guid.NewGuid(), "  ")).ErrorMessage.Should().NotBeNullOrWhiteSpace();

        var result = await svc.RejeterAsync(avenant.Id, Guid.NewGuid(), "Non prioritaire");
        result.Succeeded.Should().BeTrue();
        (await ctx.AvenantsProjets.FirstAsync()).Statut.Should().Be(StatutAvenant.Rejete);
    }
}
