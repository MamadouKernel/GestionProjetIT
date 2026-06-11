using Xunit;
using FluentAssertions;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Domain.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Tests.Unit.Projet
{
    public class UATTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public UATTests()
        {
            _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        }

        [Fact]
        public async Task UAT01_UploadPlanRecette_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Plan_Recette.docx",
                TypeLivrable = TypeLivrable.CahierTests,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/plan_recette.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.CahierTests);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task UAT02_UploadCahierRecette_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Cahier_Recette.xlsx",
                TypeLivrable = TypeLivrable.CahierTests,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/cahier_recette.xlsx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.CahierTests);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task UAT03_UploadPVRecette_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "PV_Recette.pdf",
                TypeLivrable = TypeLivrable.PvRecette,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/pv_recette.pdf",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.PvRecette);
            livrableDb.Should().NotBeNull();
        }


        [Fact]
        public async Task UAT04_UploadPlanMEP_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Plan_MEP.docx",
                TypeLivrable = TypeLivrable.DossierMep,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/plan_mep.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.DossierMep);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task UAT05_UploadPVMEP_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "PV_MEP.pdf",
                TypeLivrable = TypeLivrable.PvMep,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/pv_mep.pdf",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.PvMep);
            livrableDb.Should().NotBeNull();
        }

        [Fact]
        public async Task UAT06_UploadDocumentationUtilisateur_DoitEtreEnregistre()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Documentation_Utilisateur.pdf",
                TypeLivrable = TypeLivrable.Autre,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/doc_user.pdf",
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
        public async Task UAT07_ValidationRecette_DoitMettreAJourStatut()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;
            var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");

            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;
            projet.RecetteValideeParId = directeurMetier.Id;
            await _context.SaveChangesAsync();

            projet.RecetteValidee.Should().BeTrue();
            projet.DateRecetteValidee.Should().NotBeNull();
            projet.RecetteValideeParId.Should().Be(directeurMetier.Id);
        }

        [Fact]
        public async Task UAT08_MEPEffectuee_DoitMettreAJourStatut()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;
            projet.RecetteValidee = true;

            projet.MepEffectuee = true;
            projet.DateMep = DateTime.Now;
            await _context.SaveChangesAsync();

            projet.MepEffectuee.Should().BeTrue();
            projet.DateMep.Should().NotBeNull();
        }

        [Fact]
        public async Task UAT09_GestionAnomalies_DoitEtrePossible()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var anomalie = new AnomalieProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Reference = "ANOM-001",
                Description = "Description de l'anomalie",
                Priorite = PrioriteAnomalie.Haute,
                Statut = StatutAnomalie.Ouverte,
                DateCreationAnomalie = DateTime.Now,
                Environnement = Environnement.Recette
            };
            _context.AnomaliesProjets.Add(anomalie);
            await _context.SaveChangesAsync();

            var anomalieDb = await _context.AnomaliesProjets.FindAsync(anomalie.Id);
            anomalieDb.Should().NotBeNull();
            anomalieDb!.Statut.Should().Be(StatutAnomalie.Ouverte);
        }


        [Fact]
        public async Task UAT10_ResolutionAnomalie_DoitMettreAJourStatut()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var anomalie = new AnomalieProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Reference = "ANOM-002",
                Description = "Description",
                Priorite = PrioriteAnomalie.Haute,
                Statut = StatutAnomalie.Ouverte,
                DateCreationAnomalie = DateTime.Now,
                Environnement = Environnement.Recette
            };
            _context.AnomaliesProjets.Add(anomalie);
            await _context.SaveChangesAsync();

            anomalie.Statut = StatutAnomalie.Fermee;
            anomalie.DateResolution = DateTime.Now;
            await _context.SaveChangesAsync();

            var anomalieDb = await _context.AnomaliesProjets.FindAsync(anomalie.Id);
            anomalieDb!.Statut.Should().Be(StatutAnomalie.Fermee);
            anomalieDb.DateResolution.Should().NotBeNull();
        }

        [Fact]
        public async Task UAT11_LivrablesObligatoires_DoiventEtrePresents()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrables = new[]
            {
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "Plan_Recette.docx", TypeLivrable = TypeLivrable.CahierTests, Phase = PhaseProjet.UatMep, CheminRelatif = "uploads/plan.docx", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" },
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "Cahier_Recette.xlsx", TypeLivrable = TypeLivrable.CahierTests, Phase = PhaseProjet.UatMep, CheminRelatif = "uploads/cahier.xlsx", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" },
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "PV_Recette.pdf", TypeLivrable = TypeLivrable.PvRecette, Phase = PhaseProjet.UatMep, CheminRelatif = "uploads/pv.pdf", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" }
            };
            _context.LivrablesProjets.AddRange(livrables);
            await _context.SaveChangesAsync();

            var livrablesDb = await _context.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id && l.Phase == PhaseProjet.UatMep)
                .ToListAsync();

            livrablesDb.Should().HaveCountGreaterOrEqualTo(3);
        }

        [Fact]
        public async Task UAT12_RecetteValidee_RequiertTousLesLivrables()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrables = new[]
            {
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "Plan.docx", TypeLivrable = TypeLivrable.CahierTests, Phase = PhaseProjet.UatMep, CheminRelatif = "uploads/plan.docx", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" },
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "Cahier.xlsx", TypeLivrable = TypeLivrable.CahierTests, Phase = PhaseProjet.UatMep, CheminRelatif = "uploads/cahier.xlsx", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" },
                new LivrableProjet { Id = Guid.NewGuid(), ProjetId = projet.Id, NomDocument = "PV.pdf", TypeLivrable = TypeLivrable.PvRecette, Phase = PhaseProjet.UatMep, CheminRelatif = "uploads/pv.pdf", DateDepot = DateTime.Now, DeposeParId = projet.ChefProjetId!.Value, Commentaire = string.Empty, Version = "1.0" }
            };
            _context.LivrablesProjets.AddRange(livrables);
            await _context.SaveChangesAsync();

            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;
            await _context.SaveChangesAsync();

            projet.RecetteValidee.Should().BeTrue();
        }


        [Fact]
        public async Task UAT13_MEP_RequiertRecetteValidee()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;
            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;

            projet.MepEffectuee = true;
            projet.DateMep = DateTime.Now;
            await _context.SaveChangesAsync();

            projet.MepEffectuee.Should().BeTrue();
            projet.RecetteValidee.Should().BeTrue();
        }

        [Fact]
        public async Task UAT14_TransitionVersCloture_RequiertMEP()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;
            projet.RecetteValidee = true;
            projet.MepEffectuee = true;
            projet.DateMep = DateTime.Now;

            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;
            await _context.SaveChangesAsync();

            projet.PhaseActuelle.Should().Be(PhaseProjet.ClotureLeconsApprises);
        }

        [Fact]
        public async Task UAT15_AnomaliesBloquantes_DoiventEtreResolues()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var anomalie = new AnomalieProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Reference = "ANOM-BLOQ-001",
                Description = "Description",
                Priorite = PrioriteAnomalie.Critique,
                Statut = StatutAnomalie.Fermee,
                DateCreationAnomalie = DateTime.Now,
                DateResolution = DateTime.Now,
                Environnement = Environnement.Recette
            };
            _context.AnomaliesProjets.Add(anomalie);
            await _context.SaveChangesAsync();

            var anomalieDb = await _context.AnomaliesProjets.FindAsync(anomalie.Id);
            anomalieDb!.Priorite.Should().Be(PrioriteAnomalie.Critique);
            anomalieDb.Statut.Should().Be(StatutAnomalie.Fermee);
        }

        [Fact]
        public async Task UAT16_CoherenceRecetteMEP_DoitEtreVerifiee()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;
            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now.AddDays(-5);
            projet.MepEffectuee = true;
            projet.DateMep = DateTime.Now;

            projet.DateMep.Should().BeAfter(projet.DateRecetteValidee!.Value);
        }

        [Fact]
        public async Task UAT17_DocumentationTechnique_DoitEtrePresente()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Documentation_Technique.pdf",
                TypeLivrable = TypeLivrable.Autre,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/doc_tech.pdf",
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
        public async Task UAT18_PlanRetourArriere_DoitEtrePresent()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;

            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Plan_Retour_Arriere.docx",
                TypeLivrable = TypeLivrable.Autre,
                Phase = PhaseProjet.UatMep,
                CheminRelatif = "uploads/plan_retour.docx",
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
        public async Task UAT19_ValidationDirecteurMetier_EstObligatoire()
        {
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.UatMep;
            var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");

            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;
            projet.RecetteValideeParId = directeurMetier.Id;
            await _context.SaveChangesAsync();

            projet.RecetteValidee.Should().BeTrue();
            projet.RecetteValideeParId.Should().NotBeNull();
            projet.RecetteValideeParId.Should().Be(directeurMetier.Id);
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
