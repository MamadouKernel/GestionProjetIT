using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Services;

public class ElectronicSignatureServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ElectronicSignatureService _service;

    public ElectronicSignatureServiceTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        _service = new ElectronicSignatureService(_context);
    }

    [Fact]
    public async Task InitialiserCharteAsync_CreeDossierEtSignataires()
    {
        var projet = await CreerProjetAvecCharteSourceAsync();

        var dossier = await _service.InitialiserCharteAsync(projet.Id, FournisseurSignatureElectronique.DocuSign, "TEST");

        dossier.Should().NotBeNull();
        dossier.Statut.Should().Be(StatutDossierSignature.Brouillon);
        dossier.LivrableSourceId.Should().NotBeNull();
        dossier.Signataires.Should().HaveCount(2);
        dossier.Signataires.Should().Contain(s => s.Role == RoleSignataireProjet.Sponsor);
        dossier.Signataires.Should().Contain(s => s.Role == RoleSignataireProjet.ChefDeProjet);
    }

    [Fact]
    public async Task EnvoyerDossierAsync_GenereReferenceEtMetAJourStatut()
    {
        var projet = await CreerProjetAvecCharteSourceAsync();
        var dossier = await _service.InitialiserCharteAsync(projet.Id, FournisseurSignatureElectronique.AdobeSign, "TEST");

        var result = await _service.EnvoyerDossierAsync(dossier.Id, "TEST");

        result.Success.Should().BeTrue();
        result.Dossier.Should().NotBeNull();
        result.Dossier!.Statut.Should().Be(StatutDossierSignature.EnCoursSignature);
        result.Dossier.ExternalRequestId.Should().StartWith("ADOBE-");
        result.Dossier.UrlSuivi.Should().Contain("echosign");
    }

    [Fact]
    public async Task EnregistrerDecisionSignataireAsync_TousSignes_PasseDossierEnSigne()
    {
        var projet = await CreerProjetAvecCharteSourceAsync();
        var dossier = await _service.InitialiserCharteAsync(projet.Id, FournisseurSignatureElectronique.DocuSign, "TEST");
        await _service.EnvoyerDossierAsync(dossier.Id, "TEST");

        var signataires = await _context.SignatairesDossiersSignatureProjets
            .Where(s => s.DossierSignatureProjetId == dossier.Id)
            .OrderBy(s => s.OrdreSignature)
            .ToListAsync();

        await _service.EnregistrerDecisionSignataireAsync(dossier.Id, signataires[0].Id, true, "TEST");
        var result = await _service.EnregistrerDecisionSignataireAsync(dossier.Id, signataires[1].Id, true, "TEST");

        result.Success.Should().BeTrue();
        result.Dossier.Should().NotBeNull();
        result.Dossier!.Statut.Should().Be(StatutDossierSignature.Signe);
        result.Dossier.Signataires.Should().OnlyContain(s => s.Statut == StatutSignataireDossierSignature.Signe);
    }

    [Fact]
    public async Task FinaliserDossierAsync_RefuseSiSignatairesIncomplets()
    {
        var projet = await CreerProjetAvecCharteSourceAsync();
        var dossier = await _service.InitialiserCharteAsync(projet.Id, FournisseurSignatureElectronique.Manuel, "TEST");
        await _service.EnvoyerDossierAsync(dossier.Id, "TEST");

        var premierSignataire = await _context.SignatairesDossiersSignatureProjets
            .Where(s => s.DossierSignatureProjetId == dossier.Id)
            .OrderBy(s => s.OrdreSignature)
            .FirstAsync();

        await _service.EnregistrerDecisionSignataireAsync(dossier.Id, premierSignataire.Id, true, "TEST");
        var result = await _service.FinaliserDossierAsync(dossier.Id, "charte-signee.pdf", "uploads/projets/test/charte-signee.pdf", "TEST");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("signatures");
    }

    private async Task<Projet> CreerProjetAvecCharteSourceAsync()
    {
        var direction = await _context.Directions.FirstAsync(d => d.Code == "DSI");
        var sponsor = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");
        var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Diallo");
        var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Kouassi");

        var demande = new DemandeProjet
        {
            Id = Guid.NewGuid(),
            Titre = "Demande signature",
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
            CodeProjet = $"PRJ-SIG-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Titre = "Projet Signature",
            Objectif = "Objectif",
            DemandeProjetId = demande.Id,
            DirectionId = direction.Id,
            SponsorId = sponsor.Id,
            ChefProjetId = chefProjet.Id,
            StatutProjet = StatutProjet.EnCours,
            PhaseActuelle = PhaseProjet.AnalyseClarification,
            PourcentageAvancement = 10,
            EtatProjet = EtatProjet.Vert,
            BilanCloture = string.Empty,
            LeconsApprises = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        };
        _context.Projets.Add(projet);
        await _context.SaveChangesAsync();

        _context.LivrablesProjets.Add(new LivrableProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projet.Id,
            Phase = PhaseProjet.AnalyseClarification,
            TypeLivrable = TypeLivrable.CharteProjet,
            NomDocument = $"CharteProjet_{projet.CodeProjet}.pdf",
            CheminRelatif = $"uploads/projets/{projet.CodeProjet}/analyse/charte.pdf",
            DateDepot = DateTime.Now,
            DeposeParId = chefProjet.Id,
            Commentaire = "Generation initiale",
            Version = "R01",
            DateCreation = DateTime.Now,
            CreePar = "TEST"
        });
        await _context.SaveChangesAsync();

        return projet;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
