using Xunit;
using FluentAssertions;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Domain.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Tests.Unit.Projet
{
    public class ExecutionTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public ExecutionTests()
        {
            _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        }

        [Fact]
        public async Task EXEC01_UploadCRReunion_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "CR_Reunion_01.docx",
                TypeLivrable = TypeLivrable.CompteRenduReunion,
                Phase = PhaseProjet.ExecutionSuivi,
                CheminRelatif = "uploads/cr.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.CompteRenduReunion);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task EXEC02_UploadRapportAvancement_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Rapport_Avancement_M1.docx",
                TypeLivrable = TypeLivrable.CompteRenduReunion,
                Phase = PhaseProjet.ExecutionSuivi,
                CheminRelatif = "uploads/rapport.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.CompteRenduReunion);
            livrableDb.Should().NotBeNull();
        }


        [Fact]
        public async Task EXEC03_CommentaireTechnique_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            var responsable = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Koffi");

            projet.CommentaireTechnique = "Commentaire technique sur l'avancement";
            projet.DateDernierCommentaireTechnique = DateTime.Now;
            projet.DernierCommentaireTechniqueParId = responsable.Id;
            await _context.SaveChangesAsync();

            projet.CommentaireTechnique.Should().NotBeNullOrEmpty();
            projet.DateDernierCommentaireTechnique.Should().NotBeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(25)]
        [InlineData(50)]
        [InlineData(75)]
        [InlineData(100)]
        public async Task EXEC04_PourcentageAvancement_DoitEtreValide(int pourcentage)
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            projet.PourcentageAvancement = pourcentage;
            await _context.SaveChangesAsync();

            projet.PourcentageAvancement.Should().Be(pourcentage);
            projet.PourcentageAvancement.Should().BeInRange(0, 100);
        }

        [Fact]
        public async Task EXEC05_PourcentageAvancement_NePeutPasDepasser100()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            projet.PourcentageAvancement = 100;
            await _context.SaveChangesAsync();

            projet.PourcentageAvancement.Should().BeLessOrEqualTo(100);
        }

        [Fact]
        public async Task EXEC06_PourcentageAvancement_NePeutPasEtreNegatif()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            projet.PourcentageAvancement = 0;
            await _context.SaveChangesAsync();

            projet.PourcentageAvancement.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task EXEC07_MiseAJourAvancement_DoitEtreTracee()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            projet.PourcentageAvancement = 30;
            await _context.SaveChangesAsync();

            projet.PourcentageAvancement = 60;
            await _context.SaveChangesAsync();

            projet.PourcentageAvancement.Should().Be(60);
        }

        [Theory]
        [InlineData(EtatProjet.Vert)]
        [InlineData(EtatProjet.Orange)]
        [InlineData(EtatProjet.Rouge)]
        public async Task EXEC08_EtatProjet_DoitEtreDefini(EtatProjet etat)
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            projet.EtatProjet = etat;
            await _context.SaveChangesAsync();

            projet.EtatProjet.Should().Be(etat);
        }

        [Fact]
        public async Task EXEC09_EtatProjetVert_ProjetDansLesClous()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            projet.EtatProjet = EtatProjet.Vert;
            projet.PourcentageAvancement = 50;

            await _context.SaveChangesAsync();

            projet.EtatProjet.Should().Be(EtatProjet.Vert);
        }

        [Fact]
        public async Task EXEC10_EtatProjetRouge_ProjetEnDifficulte()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            projet.EtatProjet = EtatProjet.Rouge;

            await _context.SaveChangesAsync();

            projet.EtatProjet.Should().Be(EtatProjet.Rouge);
        }


        [Fact]
        public async Task EXEC11_MiseAJourRisques_DoitEtrePossible()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            var risque = new RisqueProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Description = "Risque technique",
                Probabilite = ProbabiliteRisque.Moyenne,
                Impact = ImpactRisque.Eleve,
                Statut = StatutRisque.Identifie,
                PlanMitigation = "Plan de mitigation",
                Responsable = "Chef de projet",
                DateCreationRisque = DateTime.Now
            };
            _context.RisquesProjets.Add(risque);
            await _context.SaveChangesAsync();

            risque.Statut = StatutRisque.EnCoursTraitement;
            await _context.SaveChangesAsync();

            var risqueDb = await _context.RisquesProjets.FindAsync(risque.Id);
            risqueDb!.Statut.Should().Be(StatutRisque.EnCoursTraitement);
        }

        [Fact]
        public async Task EXEC12_DecisionGoNoGo_DoitEtreDocumentee()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            projet.PourcentageAvancement = 100;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Decision_Go_NoGo_UAT.docx",
                TypeLivrable = TypeLivrable.Autre,
                Phase = PhaseProjet.ExecutionSuivi,
                CheminRelatif = "uploads/go_nogo.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.Autre);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task EXEC13_TransitionVersUAT_RequiertAvancement100()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            projet.PourcentageAvancement = 100;

            projet.PhaseActuelle = PhaseProjet.UatMep;
            await _context.SaveChangesAsync();

            projet.PhaseActuelle.Should().Be(PhaseProjet.UatMep);
            projet.PourcentageAvancement.Should().Be(100);
        }

        [Fact]
        public async Task EXEC14_IndicateurRAG_DoitEtreCalcule()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;

            projet.IndicateurRAG = IndicateurRAG.Vert;
            projet.DateDernierCalculRAG = DateTime.Now;
            await _context.SaveChangesAsync();

            projet.IndicateurRAG.Should().Be(IndicateurRAG.Vert);
            projet.DateDernierCalculRAG.Should().NotBeNull();
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

        public void Dispose()
        {
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }
    }
}
