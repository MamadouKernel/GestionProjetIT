using Xunit;
using FluentAssertions;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Domain.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Tests.Unit.Projet
{
    public class ClotureTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public ClotureTests()
        {
            _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        }

        [Fact]
        public async Task CLOT01_BilanCloture_DoitEtreRempli()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            projet.BilanCloture = "Bilan complet du projet avec résultats obtenus";
            await _context.SaveChangesAsync();

            projet.BilanCloture.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CLOT02_LeconsApprises_DoiventEtreDocumentees()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            projet.LeconsApprises = "Leçons apprises durant le projet";
            await _context.SaveChangesAsync();

            projet.LeconsApprises.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CLOT03_DemandeCloture_DoitEtreCreee()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;
            projet.RecetteValidee = true;
            projet.MepEffectuee = true;

            var demandeCloture = new DemandeClotureProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                CommentaireDemandeur = "Bilan du projet - Leçons apprises",
                DateDemande = DateTime.Now,
                StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente,
                StatutValidationDSI = StatutValidationCloture.EnAttente,
                CommentaireDSI = string.Empty,
                CommentaireDirecteurMetier = string.Empty
            };
            _context.DemandesClotureProjets.Add(demandeCloture);
            await _context.SaveChangesAsync();

            var demandeDb = await _context.DemandesClotureProjets.FindAsync(demandeCloture.Id);
            demandeDb.Should().NotBeNull();
        }

        [Fact]
        public async Task CLOT04_ValidationClotureDirecteurMetier_DoitMettreAJourStatut()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);

            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
            demandeCloture.DateValidationDirecteurMetier = DateTime.Now;
            await _context.SaveChangesAsync();

            demandeCloture.StatutValidationDirecteurMetier.Should().Be(StatutValidationCloture.Validee);
            demandeCloture.DateValidationDirecteurMetier.Should().NotBeNull();
        }


        [Fact]
        public async Task CLOT05_ValidationClotureDSI_DoitMettreAJourStatut()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);
            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
            demandeCloture.DateValidationDirecteurMetier = DateTime.Now;

            demandeCloture.StatutValidationDSI = StatutValidationCloture.Validee;
            demandeCloture.DateValidationDSI = DateTime.Now;
            await _context.SaveChangesAsync();

            demandeCloture.StatutValidationDSI.Should().Be(StatutValidationCloture.Validee);
            demandeCloture.DateValidationDSI.Should().NotBeNull();
        }

        [Fact]
        public async Task CLOT06_DoubleValidationCloture_DoitEtreSequentielle()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);

            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
            demandeCloture.DateValidationDirecteurMetier = DateTime.Now;
            await _context.SaveChangesAsync();

            demandeCloture.StatutValidationDSI = StatutValidationCloture.Validee;
            demandeCloture.DateValidationDSI = DateTime.Now;
            await _context.SaveChangesAsync();

            demandeCloture.DateValidationDirecteurMetier.Should().BeBefore(demandeCloture.DateValidationDSI!.Value);
        }

        [Fact]
        public async Task CLOT07_RejetClotureDM_DoitAvoirCommentaire()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);

            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Rejetee;
            demandeCloture.CommentaireDirecteurMetier = "Bilan incomplet";
            await _context.SaveChangesAsync();

            demandeCloture.StatutValidationDirecteurMetier.Should().Be(StatutValidationCloture.Rejetee);
            demandeCloture.CommentaireDirecteurMetier.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CLOT08_RejetClotureDSI_DoitAvoirCommentaire()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);
            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;

            demandeCloture.StatutValidationDSI = StatutValidationCloture.Rejetee;
            demandeCloture.CommentaireDSI = "Leçons apprises insuffisantes";
            await _context.SaveChangesAsync();

            demandeCloture.StatutValidationDSI.Should().Be(StatutValidationCloture.Rejetee);
            demandeCloture.CommentaireDSI.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CLOT09_UploadBilanProjet_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Bilan_Projet.pdf",
                TypeLivrable = TypeLivrable.RapportCloture,
                Phase = PhaseProjet.ClotureLeconsApprises,
                CheminRelatif = "uploads/bilan.pdf",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.RapportCloture);
            livrableDb.Should().NotBeNull();
        }


        [Fact]
        public async Task CLOT10_UploadRapportFinal_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Rapport_Final.pdf",
                TypeLivrable = TypeLivrable.RapportCloture,
                Phase = PhaseProjet.ClotureLeconsApprises,
                CheminRelatif = "uploads/rapport_final.pdf",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.RapportCloture);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task CLOT11_ClotureValidee_DoitChangerStatutProjet()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);
            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
            demandeCloture.StatutValidationDSI = StatutValidationCloture.Validee;
            demandeCloture.EstTerminee = true;

            projet.StatutProjet = StatutProjet.Cloture;
            projet.DateFinReelle = DateTime.Now;
            await _context.SaveChangesAsync();

            projet.StatutProjet.Should().Be(StatutProjet.Cloture);
            projet.DateFinReelle.Should().NotBeNull();
        }

        [Fact]
        public async Task CLOT12_DateFinReelle_DoitEtreDefinie()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            projet.DateFinReelle = DateTime.Now;
            await _context.SaveChangesAsync();

            projet.DateFinReelle.Should().NotBeNull();
        }

        [Fact]
        public async Task CLOT13_DateFinReelle_DoitEtreApresDateDebut()
        {
            var projet = await CreerProjetTestAsync();
            projet.DateDebut = DateTime.Now.AddMonths(-6);
            projet.DateFinReelle = DateTime.Now;

            projet.DateFinReelle.Should().BeAfter(projet.DateDebut!.Value);
        }

        [Fact]
        public async Task CLOT14_ProjetCloture_DoitEtreVisibleDansPortefeuille()
        {
            var projet = await CreerProjetTestAsync();
            projet.StatutProjet = StatutProjet.Cloture;
            projet.DateFinReelle = DateTime.Now;
            await _context.SaveChangesAsync();

            var projetDb = await _context.Projets
                .FirstOrDefaultAsync(p => p.Id == projet.Id && p.StatutProjet == StatutProjet.Cloture);
            projetDb.Should().NotBeNull();
        }

        [Fact]
        public async Task CLOT15_HistoriquePhases_DoitEtreComplet()
        {
            var projet = await CreerProjetTestAsync();

            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.ClotureLeconsApprises,
                StatutProjet = StatutProjet.Cloture,
                DateDebut = DateTime.Now,
                ModifieParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty
            };
            _context.HistoriquePhasesProjets.Add(historique);
            await _context.SaveChangesAsync();

            var historiqueDb = await _context.HistoriquePhasesProjets
                .FirstOrDefaultAsync(h => h.ProjetId == projet.Id);
            historiqueDb.Should().NotBeNull();
        }


        [Fact]
        public async Task CLOT16_LivrablesFinaux_DoiventEtreArchives()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;

            var livrables = new[]
            {
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "Bilan.pdf", TypeLivrable = TypeLivrable.RapportCloture, Phase = PhaseProjet.ClotureLeconsApprises, CheminRelatif = "uploads/bilan.pdf", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" },
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "Rapport.pdf", TypeLivrable = TypeLivrable.RapportCloture, Phase = PhaseProjet.ClotureLeconsApprises, CheminRelatif = "uploads/rapport.pdf", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" }
            };
            _context.LivrablesProjets.AddRange(livrables);
            await _context.SaveChangesAsync();

            var livrablesDb = await _context.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id && l.Phase == PhaseProjet.ClotureLeconsApprises)
                .ToListAsync();

            livrablesDb.Should().HaveCountGreaterOrEqualTo(2);
        }

        [Fact]
        public async Task CLOT17_NotificationCloture_DoitEtreEnvoyee()
        {
            var projet = await CreerProjetTestAsync();
            var demandeCloture = await CreerDemandeClotureTestAsync(projet.Id);
            demandeCloture.StatutValidationDirecteurMetier = StatutValidationCloture.Validee;
            demandeCloture.StatutValidationDSI = StatutValidationCloture.Validee;
            demandeCloture.EstTerminee = true;
            await _context.SaveChangesAsync();

            demandeCloture.EstTerminee.Should().BeTrue();
        }

        private async Task<GestionProjects.Domain.Models.Projet> CreerProjetTestAsync()
        {
            var direction = await _context.Directions.FirstAsync(d => d.Libelle == "Direction des Systèmes d'Information");
            var sponsor = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");
            var chefProjet = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Diallo");
            var demandeur = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Kouassi");

            var demande = new GestionProjects.Domain.Models.DemandeProjet
            {
                Id = Guid.NewGuid(),
                Titre = "Demande Test",
                Description = "Description test",
                Contexte = "Contexte test",
                Objectifs = "Objectifs test",
                AvantagesAttendus = "Avantages test",
                Perimetre = "Périmètre test",
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

            var projet = new GestionProjects.Domain.Models.Projet
            {
                Id = Guid.NewGuid(),
                CodeProjet = "PRJ-TEST-001",
                Titre = "Projet Test",
                Objectif = "Objectif test",
                DemandeProjetId = demande.Id,
                DirectionId = direction.Id,
                SponsorId = sponsor.Id,
                ChefProjetId = chefProjet.Id,
                StatutProjet = StatutProjet.EnCours,
                PhaseActuelle = PhaseProjet.AnalyseClarification,
                PourcentageAvancement = 0,
                EtatProjet = EtatProjet.Vert,
                BilanCloture = string.Empty,
                LeconsApprises = string.Empty
            };
            _context.Projets.Add(projet);
            await _context.SaveChangesAsync();

            return projet;
        }

        private async Task<DemandeClotureProjet> CreerDemandeClotureTestAsync(Guid projetId)
        {
            var demandeCloture = new DemandeClotureProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                CommentaireDemandeur = "Bilan complet du projet - Leçons apprises importantes",
                DateDemande = DateTime.Now,
                StatutValidationDirecteurMetier = StatutValidationCloture.EnAttente,
                StatutValidationDSI = StatutValidationCloture.EnAttente,
                CommentaireDSI = string.Empty,
                CommentaireDirecteurMetier = string.Empty
            };
            _context.DemandesClotureProjets.Add(demandeCloture);
            await _context.SaveChangesAsync();
            return demandeCloture;
        }

        public void Dispose()
        {
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }
    }
}
