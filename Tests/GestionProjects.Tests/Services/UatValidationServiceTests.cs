using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Services;

public class UatValidationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UatValidationService _service;

    public UatValidationServiceTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        _service = new UatValidationService(_context);
    }

    [Fact]
    public async Task ValiderRecetteAsync_SansCasObligatoire_Echoue()
    {
        var projet = await CreerProjetUatAsync();

        var resultat = await _service.ValiderRecetteAsync(projet.Id);

        resultat.EstValide.Should().BeFalse();
        resultat.TotalCasObligatoires.Should().Be(0);
        resultat.Erreurs.Should().Contain(e => e.Contains("Aucun cas de test obligatoire"));
    }

    [Fact]
    public async Task ValiderRecetteAsync_CasObligatoireReussi_EstValide()
    {
        var projet = await CreerProjetUatAsync();
        var casTest = await AjouterCasTestAsync(projet, true);

        _context.ExecutionsTestsProjets.Add(new ExecutionTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            CasTestProjetId = casTest.Id,
            Statut = StatutExecutionTest.Reussie,
            Commentaire = "Execution conforme",
            DateExecution = DateTime.Now,
            ExecuteParId = projet.ChefProjetId,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        });
        await _context.SaveChangesAsync();

        var resultat = await _service.ValiderRecetteAsync(projet.Id);

        resultat.EstValide.Should().BeTrue();
        resultat.CasValides.Should().Be(1);
        resultat.CasSansExecution.Should().Be(0);
        resultat.AnomaliesBloquantes.Should().Be(0);
    }

    [Fact]
    public async Task ValiderRecetteAsync_AnomalieBloquanteOuverte_Echoue()
    {
        var projet = await CreerProjetUatAsync();
        var casTest = await AjouterCasTestAsync(projet, true);

        _context.ExecutionsTestsProjets.Add(new ExecutionTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            CasTestProjetId = casTest.Id,
            Statut = StatutExecutionTest.Reussie,
            Commentaire = "Execution conforme",
            DateExecution = DateTime.Now,
            ExecuteParId = projet.ChefProjetId,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        });

        _context.AnomaliesProjets.Add(new AnomalieProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            CasTestProjetId = casTest.Id,
            Reference = "ANOM-UAT-001",
            Description = "Blocage critique sur le parcours de recette",
            Priorite = PrioriteAnomalie.Critique,
            Statut = StatutAnomalie.Ouverte,
            Environnement = Environnement.Recette,
            DateCreationAnomalie = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        });
        await _context.SaveChangesAsync();

        var resultat = await _service.ValiderRecetteAsync(projet.Id);

        resultat.EstValide.Should().BeFalse();
        resultat.AnomaliesBloquantes.Should().Be(1);
        resultat.Erreurs.Should().Contain(e => e.Contains("anomalie(s) critique(s) ou haute(s)"));
    }

    [Fact]
    public async Task ValiderFinUatAsync_CampagneOuverte_Echoue()
    {
        var projet = await CreerProjetUatAsync();
        var casTest = await AjouterCasTestAsync(projet, true);

        var campagne = new CampagneTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            Nom = "Campagne ouverte",
            Description = "Campagne de test encore en cours",
            Environnement = Environnement.Recette,
            Statut = StatutCampagneTest.EnCours,
            DateLancement = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        _context.CampagnesTestsProjets.Add(campagne);
        _context.ExecutionsTestsProjets.Add(new ExecutionTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            CasTestProjetId = casTest.Id,
            CampagneTestProjetId = campagne.Id,
            Statut = StatutExecutionTest.Reussie,
            Commentaire = "Execution conforme",
            DateExecution = DateTime.Now,
            ExecuteParId = projet.ChefProjetId,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        });
        await _context.SaveChangesAsync();

        var resultat = await _service.ValiderFinUatAsync(projet.Id);

        resultat.EstValide.Should().BeFalse();
        resultat.CampagnesOuvertes.Should().Be(1);
        resultat.Erreurs.Should().Contain(e => e.Contains("campagne(s) de test doivent"));
    }

    [Fact]
    public async Task AssurerCampagneParDefautAsync_CreeCampagneSiAbsente()
    {
        var projet = await CreerProjetUatAsync();

        var campagne = await _service.AssurerCampagneParDefautAsync(projet, "TEST");

        campagne.Should().NotBeNull();
        campagne.ProjetId.Should().Be(projet.Id);
        campagne.Statut.Should().Be(StatutCampagneTest.EnCours);
        (await _context.CampagnesTestsProjets.CountAsync(c => c.ProjetId == projet.Id)).Should().Be(1);
    }

    [Fact]
    public async Task GenererReferenceCasTestAsync_IncrementeLeCompteurDuProjet()
    {
        var projet = await CreerProjetUatAsync();
        await AjouterCasTestAsync(projet, true);
        await AjouterCasTestAsync(projet, false);

        var reference = await _service.GenererReferenceCasTestAsync(projet);

        reference.Should().Be($"TC-{projet.CodeProjet}-003");
    }

    private async Task<Projet> CreerProjetUatAsync()
    {
        var direction = await _context.Directions.FirstAsync(d => d.Code == "DSI");
        var sponsor = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");
        var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Diallo");
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Kouassi");

        var demande = new DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande UAT Test",
            Description = "Description test",
            Contexte = "Contexte test",
            Objectifs = "Objectifs test",
            AvantagesAttendus = "Avantages test",
            Perimetre = "Perimetre test",
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
            CodeProjet = $"PRJ-UAT-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Titre = "Projet UAT Test",
            Objectif = "Objectif test",
            DemandeProjetId = demande.Id,
            DirectionId = direction.Id,
            SponsorId = sponsor.Id,
            ChefProjetId = chefProjet.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = PhaseProjet.UatMep,
            PourcentageAvancement = 0,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();

        return projet;
    }

    private async Task<CasTestProjet> AjouterCasTestAsync(Projet projet, bool estObligatoire)
    {
        var numero = await _context.CasTestsProjets.CountAsync(c => c.ProjetId == projet.Id) + 1;
        var casTest = new CasTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            Reference = $"TC-{projet.CodeProjet}-{numero:000}",
            Titre = $"Cas test {numero}",
            Description = "Description du cas de test",
            ResultatAttendu = "Le resultat attendu est conforme",
            Priorite = PrioriteAnomalie.Moyenne,
            EstObligatoire = estObligatoire,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };

        _context.CasTestsProjets.Add(casTest);
        await _context.SaveChangesAsync();
        return casTest;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
