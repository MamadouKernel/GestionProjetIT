using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DemandeProjetModel = GestionProjects.Domain.Models.DemandeProjet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests unitaires pour ProjetQueryService.
/// Couvre : AppliquerScopeAsync (filtrage par rôle), GetUserDirectionIdAsync.
/// Chaque test crée son propre contexte InMemory isolé.
/// </summary>
public class ProjetQueryServiceTests
{
    private readonly Guid _userId   = Guid.NewGuid();
    private readonly Guid _dirId    = Guid.NewGuid();

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private (ApplicationDbContext ctx, ProjetQueryService svc) CreateSut(string dbName)
    {
        var ctx = TestDbContextFactory.CreateInMemoryContext(dbName);
        var mockCache = new Mock<ICacheService>();
        mockCache
            .Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<SelectListItem>>>>(),
                It.IsAny<TimeSpan?>()))
            .Returns<string, Func<Task<List<SelectListItem>>>, TimeSpan?>(
                async (_, factory, _) => (List<SelectListItem>?)await factory());

        var svc = new ProjetQueryService(ctx, mockCache.Object);
        return (ctx, svc);
    }

    private void SeedUser(ApplicationDbContext ctx)
    {
        ctx.Directions.Add(new Direction
        {
            Id = _dirId, Code = "DIR", Libelle = "Dir Test",
            EstActive = true, DateCreation = DateTime.Now, CreePar = "T"
        });
        ctx.Utilisateurs.Add(new Utilisateur
        {
            Id = _userId, Matricule = "U1", MotDePasse = "x",
            Nom = "U", Prenoms = "1", Email = "u@t.ci",
            DirectionId = _dirId, DateCreation = DateTime.Now, CreePar = "T"
        });
        ctx.SaveChanges();
    }

    private DemandeProjetModel MakeDemande(Guid demandeurId)
    {
        var demande = new DemandeProjetModel
        {
            Id = Guid.NewGuid(),
            Titre = "Demande",
            DemandeurId = demandeurId,
            StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier,
            DateCreation = DateTime.Now, CreePar = "T"
        };
        return demande;
    }

    private GestionProjects.Domain.Models.Projet MakeProjet(
        ApplicationDbContext ctx,
        Guid? chefProjetId  = null,
        Guid? sponsorId     = null,
        Guid? directionId   = null,
        Guid? demandeurId   = null)
    {
        DemandeProjetModel? demande = demandeurId.HasValue ? MakeDemande(demandeurId.Value) : null;
        if (demande != null) ctx.DemandesProjets.Add(demande);

        var projet = new GestionProjects.Domain.Models.Projet
        {
            Id            = Guid.NewGuid(),
            CodeProjet    = Guid.NewGuid().ToString("N").Substring(0, 10),
            Titre         = "Projet",
            ChefProjetId  = chefProjetId,
            SponsorId     = sponsorId ?? Guid.NewGuid(),
            DirectionId   = directionId,
            DemandeProjetId = demande?.Id ?? Guid.NewGuid(),
            PhaseActuelle = PhaseProjet.AnalyseClarification,
            StatutProjet  = StatutProjet.EnCours,
            DateCreation  = DateTime.Now, CreePar = "T"
        };

        ctx.Projets.Add(projet);
        ctx.SaveChanges();

        return projet;
    }

    // ─── GetUserDirectionIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetUserDirectionIdAsync_UtilisateurAvecDirection_RetourneDirectionId()
    {
        var (ctx, svc) = CreateSut(nameof(GetUserDirectionIdAsync_UtilisateurAvecDirection_RetourneDirectionId));
        SeedUser(ctx);

        var result = await svc.GetUserDirectionIdAsync(_userId);

        result.Should().Be(_dirId);
        ctx.Dispose();
    }

    [Fact]
    public async Task GetUserDirectionIdAsync_UtilisateurInexistant_RetourneNull()
    {
        var (ctx, svc) = CreateSut(nameof(GetUserDirectionIdAsync_UtilisateurInexistant_RetourneNull));

        var result = await svc.GetUserDirectionIdAsync(Guid.NewGuid());

        result.Should().BeNull();
        ctx.Dispose();
    }

    // ─── AppliquerScopeAsync — Portefeuille complet ───────────────────────────

    [Fact]
    public async Task AppliquerScope_PortefeuilleAcces_RetourneTousLesProjets()
    {
        var (ctx, svc) = CreateSut(nameof(AppliquerScope_PortefeuilleAcces_RetourneTousLesProjets));
        MakeProjet(ctx, chefProjetId: _userId);
        MakeProjet(ctx, chefProjetId: Guid.NewGuid());
        MakeProjet(ctx, directionId: _dirId);

        var query  = ctx.Projets.AsQueryable();
        var result = await svc.AppliquerScopeAsync(query, _userId,
            canPortfolioAccess: true, hasChefProjetScope: false,
            hasDmScope: false, hasDemandeurScope: false, currentUserDirectionId: _dirId);

        result.ToList().Should().HaveCount(3);
        ctx.Dispose();
    }

    // ─── AppliquerScopeAsync — Chef de projet ────────────────────────────────

    [Fact]
    public async Task AppliquerScope_ChefProjetOnly_VoitSesProjets()
    {
        var (ctx, svc) = CreateSut(nameof(AppliquerScope_ChefProjetOnly_VoitSesProjets));
        MakeProjet(ctx, chefProjetId: _userId);
        MakeProjet(ctx, chefProjetId: Guid.NewGuid());

        var query  = ctx.Projets.AsQueryable();
        var result = await svc.AppliquerScopeAsync(query, _userId,
            canPortfolioAccess: false, hasChefProjetScope: true,
            hasDmScope: false, hasDemandeurScope: false, currentUserDirectionId: null);

        result.ToList().Should().ContainSingle()
              .Which.ChefProjetId.Should().Be(_userId);
        ctx.Dispose();
    }

    // ─── AppliquerScopeAsync — Directeur métier ──────────────────────────────

    [Fact]
    public async Task AppliquerScope_DmAvecDirection_VoitProjetsDeSaDirection()
    {
        var (ctx, svc) = CreateSut(nameof(AppliquerScope_DmAvecDirection_VoitProjetsDeSaDirection));
        var autreDir = Guid.NewGuid();
        MakeProjet(ctx, sponsorId: _userId);           // visible (sponsor)
        MakeProjet(ctx, directionId: _dirId);           // visible (même direction)
        MakeProjet(ctx, directionId: autreDir);         // non visible

        var query  = ctx.Projets.AsQueryable();
        var result = await svc.AppliquerScopeAsync(query, _userId,
            canPortfolioAccess: false, hasChefProjetScope: false,
            hasDmScope: true, hasDemandeurScope: false, currentUserDirectionId: _dirId);

        result.ToList().Should().HaveCount(2);
        ctx.Dispose();
    }

    // ─── AppliquerScopeAsync — Demandeur ─────────────────────────────────────

    [Fact]
    public async Task AppliquerScope_Demandeur_VoitSesDemandes()
    {
        var (ctx, svc) = CreateSut(nameof(AppliquerScope_Demandeur_VoitSesDemandes));
        MakeProjet(ctx, demandeurId: _userId);
        MakeProjet(ctx, demandeurId: Guid.NewGuid());

        var query  = ctx.Projets.Include(p => p.DemandeProjet).AsQueryable();
        var result = await svc.AppliquerScopeAsync(query, _userId,
            canPortfolioAccess: false, hasChefProjetScope: false,
            hasDmScope: false, hasDemandeurScope: true, currentUserDirectionId: null);

        result.ToList().Should().ContainSingle()
              .Which.DemandeProjet!.DemandeurId.Should().Be(_userId);
        ctx.Dispose();
    }

    // ─── AppliquerScopeAsync — Aucun scope ───────────────────────────────────

    [Fact]
    public async Task AppliquerScope_AucunScope_RetourneVide()
    {
        var (ctx, svc) = CreateSut(nameof(AppliquerScope_AucunScope_RetourneVide));
        MakeProjet(ctx, chefProjetId: Guid.NewGuid());
        MakeProjet(ctx, directionId: _dirId);

        var query  = ctx.Projets.AsQueryable();
        var result = await svc.AppliquerScopeAsync(query, _userId,
            canPortfolioAccess: false, hasChefProjetScope: false,
            hasDmScope: false, hasDemandeurScope: false, currentUserDirectionId: null);

        result.ToList().Should().BeEmpty();
        ctx.Dispose();
    }
}
