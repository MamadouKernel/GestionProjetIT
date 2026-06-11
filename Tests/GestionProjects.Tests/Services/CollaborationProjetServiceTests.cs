using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Services;

public class CollaborationProjetServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CollaborationProjetService _service;

    public CollaborationProjetServiceTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        _service = new CollaborationProjetService(_context);
    }

    [Fact]
    public async Task ConfigurerAsync_CreeCollaborationEtTachesParDefaut()
    {
        var projet = await CreerProjetAsync(PhaseProjet.AnalyseClarification);

        var request = new CollaborationProjetConfigurationRequest
        {
            Mode = ModeCollaborationProjet.Microsoft365
        };

        var collaboration = await _service.ConfigurerAsync(projet.Id, request, "TEST");

        collaboration.Should().NotBeNull();
        collaboration.Statut.Should().Be(StatutCollaborationProjet.Configuree);
        collaboration.NomEquipeTeams.Should().Be($"Equipe {projet.CodeProjet}");
        collaboration.NomPlanPlanner.Should().Be($"Plan {projet.CodeProjet}");
        collaboration.Taches.Should().HaveCount(5);
        collaboration.Taches.Should().Contain(t => t.Phase == PhaseProjet.UatMep);
    }

    [Fact]
    public async Task SynchroniserAsync_MetAJourNombreMembresEtStatuts()
    {
        var projet = await CreerProjetAsync(PhaseProjet.PlanificationValidation);

        _context.MembresProjets.AddRange(
            new MembreProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Nom = "Traore",
                Prenom = "Awa",
                Fonction = "Business Analyst",
                DirectionLibelle = "DSI",
                RoleDansProjet = "MOA",
                Email = "awa.traore@cit.ci",
                EstActif = true,
                DateCreation = DateTime.Now,
                CreePar = "TEST"
            },
            new MembreProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Nom = "Ndiaye",
                Prenom = "Moussa",
                Fonction = "Architecte",
                DirectionLibelle = "DSI",
                RoleDansProjet = "Expert",
                Email = "moussa.ndiaye@cit.ci",
                EstActif = true,
                DateCreation = DateTime.Now,
                CreePar = "TEST"
            });
        await _context.SaveChangesAsync();

        await _service.ConfigurerAsync(projet.Id, new CollaborationProjetConfigurationRequest
        {
            Mode = ModeCollaborationProjet.Microsoft365,
            BucketId = "BUCKET-001"
        }, "TEST");

        var result = await _service.SynchroniserAsync(projet.Id, "TEST");

        result.Success.Should().BeTrue();
        result.NombreMembresSynchronises.Should().Be(4);
        result.NombreTachesSynchronisees.Should().Be(5);

        var taches = await _context.TachesCollaborationProjets
            .Where(t => t.ProjetId == projet.Id)
            .ToListAsync();

        taches.Should().Contain(t => t.Phase == PhaseProjet.AnalyseClarification && t.Statut == StatutTacheCollaborationProjet.Terminee);
        taches.Should().Contain(t => t.Phase == PhaseProjet.PlanificationValidation && t.Statut == StatutTacheCollaborationProjet.EnCours);
        taches.Should().Contain(t => t.Phase == PhaseProjet.ExecutionSuivi && t.Statut == StatutTacheCollaborationProjet.APlanifier);
        taches.Should().OnlyContain(t => t.EstSynchronisee);
    }

    private async Task<Projet> CreerProjetAsync(PhaseProjet phase)
    {
        var direction = await _context.Directions.FirstAsync(d => d.Code == "DSI");
        var sponsor = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");
        var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Diallo");
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Kouassi");

        var demande = new DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande collaboration",
            Description = "Description",
            Contexte = "Contexte",
            Objectifs = "Objectifs",
            AvantagesAttendus = "Avantages",
            Perimetre = "Perimetre",
            Urgence = UrgenceProjet.Moyenne,
            Criticite = CriticiteProjet.Moyenne,
            DemandeurId = demandeur.Id,
            DirectionId = direction.Id,
            DirecteurMetierId = sponsor.Id,
            StatutDemande = StatutDemande.ValideeParDSI,
            DateSoumission = DateTime.Now
        };
        _context.DemandesProjets.Add(demande);
        await _context.SaveChangesAsync();

        var projet = new Projet
        {
            Id = Guid.NewGuid(),
            CodeProjet = $"PRJ-COL-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Titre = "Projet Collaboration",
            Objectif = "Objectif",
            DemandeProjetId = demande.Id,
            DirectionId = direction.Id,
            SponsorId = sponsor.Id,
            ChefProjetId = chefProjet.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = phase,
            PourcentageAvancement = 25,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateDebut = DateTime.Today,
            DateFinPrevue = DateTime.Today.AddDays(60),
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();

        return projet;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
