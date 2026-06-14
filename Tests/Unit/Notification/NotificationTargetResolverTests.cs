using FluentAssertions;
using GestionProjects.Application.Common.Constants;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Notification;

public sealed class NotificationTargetResolverTests
{
    [Fact]
    public async Task ResolveAsync_DemandeAccesAzureAd_RetourneDemandeAccesAvecFocus()
    {
        await using var db = CreateDbContext();
        var demandeId = Guid.NewGuid();
        var resolver = new NotificationTargetResolver(db);

        var target = await resolver.ResolveAsync(DomainEntityTypes.DemandeAccesAzureAd, demandeId);

        target.Controller.Should().Be("DemandesAcces");
        target.Action.Should().Be("Index");
        target.RouteValues["focusId"].Should().Be(demandeId);
    }

    [Fact]
    public async Task ResolveAsync_AnomalieProjet_RetourneProjetParentOngletExecution()
    {
        await using var db = CreateDbContext();
        var projetId = Guid.NewGuid();
        var anomalieId = Guid.NewGuid();
        db.AnomaliesProjets.Add(new AnomalieProjet
        {
            Id = anomalieId,
            ProjetId = projetId,
            Reference = "ANO-001",
            Description = "Blocage UAT",
            Priorite = PrioriteAnomalie.Critique,
            Statut = StatutAnomalie.Ouverte
        });
        await db.SaveChangesAsync();
        var resolver = new NotificationTargetResolver(db);

        var target = await resolver.ResolveAsync(DomainEntityTypes.AnomalieProjet, anomalieId);

        target.Controller.Should().Be(DomainEntityTypes.Projet);
        target.Action.Should().Be("Details");
        target.RouteValues["id"].Should().Be(projetId);
        target.RouteValues["tab"].Should().Be(ProjectDetailTabs.Execution);
    }

    [Fact]
    public async Task ResolveAsync_EntiteInconnue_RetourneCentreNotifications()
    {
        await using var db = CreateDbContext();
        var resolver = new NotificationTargetResolver(db);

        var target = await resolver.ResolveAsync("Inconnue", Guid.NewGuid());

        target.Controller.Should().Be("Notification");
        target.Action.Should().Be("Index");
        target.RouteValues.Should().BeEmpty();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
