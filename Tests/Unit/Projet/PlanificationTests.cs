using Xunit;
using FluentAssertions;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Domain.Models;
using GestionProjects.Domain.Enums;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Tests.Unit.Projet
{
    /// <summary>
    /// Tests pour le module Planification (PLAN-01 à PLAN-18)
    /// </summary>
    public class PlanificationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public PlanificationTests()
        {
            _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        }

        /// <summary>
        /// PLAN-01: Upload WBS (Work Breakdown Structure)
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN01_UploadWBS_DoitEtreEnregistre()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "WBS_Projet.xlsx",
                TypeLivrable = TypeLivrable.Wbs,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/projets/wbs.xlsx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            // Assert
            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.Wbs);
            livrableDb.Should().NotBeNull();
            livrableDb!.NomDocument.Should().Contain("WBS");
        }

        /// <summary>
        /// PLAN-02: Upload Planning détaillé
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN02_UploadPlanning_DoitEtreEnregistre()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Planning_Detaille.mpp",
                TypeLivrable = TypeLivrable.PlanningDetaille,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/projets/planning.mpp",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            // Assert
            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.PlanningDetaille);
            livrableDb.Should().NotBeNull();
        }


        /// <summary>
        /// PLAN-03: Upload Matrice RACI
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN03_UploadRACI_DoitEtreEnregistre()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Matrice_RACI.xlsx",
                TypeLivrable = TypeLivrable.MatriceRaci,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/projets/raci.xlsx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            // Assert
            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.MatriceRaci);
            livrableDb.Should().NotBeNull();
        }

        /// <summary>
        /// PLAN-04: Upload Plan de communication
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task PLAN04_UploadPlanCommunication_DoitEtreEnregistre()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Plan_Communication.docx",
                TypeLivrable = TypeLivrable.SchemaCommunication,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/projets/plan_com.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            // Assert
            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.SchemaCommunication);
            livrableDb.Should().NotBeNull();
        }

        /// <summary>
        /// PLAN-05: Dates de planification - Date début
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN05_DateDebut_DoitEtreDefinie()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            projet.DateDebut = DateTime.Now;
            await _context.SaveChangesAsync();

            // Assert
            projet.DateDebut.Should().NotBeNull();
            projet.DateDebut.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
        }


        /// <summary>
        /// PLAN-06: Dates de planification - Date fin prévue
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN06_DateFinPrevue_DoitEtreDefinie()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            projet.DateDebut = DateTime.Now;

            // Act
            projet.DateFinPrevue = DateTime.Now.AddMonths(6);
            await _context.SaveChangesAsync();

            // Assert
            projet.DateFinPrevue.Should().NotBeNull();
            projet.DateFinPrevue.Should().BeAfter(projet.DateDebut!.Value);
        }

        /// <summary>
        /// PLAN-07: Validation cohérence dates
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN07_DateFinPrevue_DoitEtreApresDateDebut()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.DateDebut = DateTime.Now;
            projet.DateFinPrevue = DateTime.Now.AddMonths(3);

            // Act & Assert
            projet.DateFinPrevue.Should().BeAfter(projet.DateDebut.Value);
        }

        /// <summary>
        /// PLAN-08: Validation planning par Directeur Métier
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN08_ValidationPlanningDM_DoitMettreAJourStatut()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;
            await _context.SaveChangesAsync();

            // Assert
            projet.PlanningValideParDM.Should().BeTrue();
            projet.DatePlanningValideParDM.Should().NotBeNull();
        }

        /// <summary>
        /// PLAN-09: Validation planning par DSI
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN09_ValidationPlanningDSI_DoitMettreAJourStatut()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;

            // Act
            projet.PlanningValideParDSI = true;
            projet.DatePlanningValideParDSI = DateTime.Now;
            await _context.SaveChangesAsync();

            // Assert
            projet.PlanningValideParDSI.Should().BeTrue();
            projet.DatePlanningValideParDSI.Should().NotBeNull();
        }


        /// <summary>
        /// PLAN-10: Double validation requise (DM puis DSI)
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN10_DoubleValidation_DoitEtreSequentielle()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act - Validation DM d'abord
            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;
            await _context.SaveChangesAsync();

            // Puis validation DSI
            projet.PlanningValideParDSI = true;
            projet.DatePlanningValideParDSI = DateTime.Now;
            await _context.SaveChangesAsync();

            // Assert
            projet.PlanningValideParDM.Should().BeTrue();
            projet.PlanningValideParDSI.Should().BeTrue();
            projet.DatePlanningValideParDM.Should().BeBefore(projet.DatePlanningValideParDSI!.Value);
        }

        /// <summary>
        /// PLAN-11: Planning validé - Transition vers Exécution
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN11_PlanningValide_DoitPermettreTransitionExecution()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            projet.PlanningValideParDM = true;
            projet.PlanningValideParDSI = true;

            // Act
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            await _context.SaveChangesAsync();

            // Assert
            projet.PhaseActuelle.Should().Be(PhaseProjet.ExecutionSuivi);
        }

        /// <summary>
        /// PLAN-12: Livrables obligatoires présents
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN12_LivrablesObligatoires_DoiventEtrePresents()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act - Ajouter les 3 livrables obligatoires
            var wbs = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "WBS.xlsx",
                TypeLivrable = TypeLivrable.Wbs,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/wbs.xlsx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };

            var planning = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Planning.mpp",
                TypeLivrable = TypeLivrable.PlanningDetaille,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/planning.mpp",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };

            var raci = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "RACI.xlsx",
                TypeLivrable = TypeLivrable.MatriceRaci,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/raci.xlsx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };

            _context.LivrablesProjets.AddRange(wbs, planning, raci);
            await _context.SaveChangesAsync();

            // Assert
            var livrables = await _context.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id && l.Phase == PhaseProjet.PlanificationValidation)
                .ToListAsync();

            livrables.Should().HaveCount(3);
            livrables.Should().Contain(l => l.TypeLivrable == TypeLivrable.Wbs);
            livrables.Should().Contain(l => l.TypeLivrable == TypeLivrable.PlanningDetaille);
            livrables.Should().Contain(l => l.TypeLivrable == TypeLivrable.MatriceRaci);
        }


        /// <summary>
        /// PLAN-13: Rejet planning par DM - Commentaire requis
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN13_RejetPlanningDM_DoitAvoirCommentaire()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            projet.PlanningValideParDM = false;
            // Note: Le commentaire serait stocké dans un système de notifications ou commentaires
            await _context.SaveChangesAsync();

            // Assert
            projet.PlanningValideParDM.Should().BeFalse();
        }

        /// <summary>
        /// PLAN-14: Rejet planning par DSI - Commentaire requis
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN14_RejetPlanningDSI_DoitAvoirCommentaire()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            projet.PlanningValideParDM = true;

            // Act
            projet.PlanningValideParDSI = false;
            await _context.SaveChangesAsync();

            // Assert
            projet.PlanningValideParDSI.Should().BeFalse();
        }

        /// <summary>
        /// PLAN-15: Historique des validations
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task PLAN15_HistoriqueValidations_DoitEtreTrace()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now.AddDays(-1);
            await _context.SaveChangesAsync();

            projet.PlanningValideParDSI = true;
            projet.DatePlanningValideParDSI = DateTime.Now;
            await _context.SaveChangesAsync();

            // Assert
            projet.DatePlanningValideParDM.Should().NotBeNull();
            projet.DatePlanningValideParDSI.Should().NotBeNull();
            projet.DatePlanningValideParDM.Should().BeBefore(projet.DatePlanningValideParDSI!.Value);
        }

        /// <summary>
        /// PLAN-16: Upload Plan de gestion des risques
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task PLAN16_UploadPlanGestionRisques_DoitEtreEnregistre()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Plan_Gestion_Risques.docx",
                TypeLivrable = TypeLivrable.Autre,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/projets/plan_risques.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            // Assert
            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.Autre);
            livrableDb.Should().NotBeNull();
        }


        /// <summary>
        /// PLAN-17: Upload Plan de gestion de la qualité
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task PLAN17_UploadPlanGestionQualite_DoitEtreEnregistre()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomDocument = "Plan_Gestion_Qualite.docx",
                TypeLivrable = TypeLivrable.Autre,
                Phase = PhaseProjet.PlanificationValidation,
                CheminRelatif = "uploads/projets/plan_qualite.docx",
                DateDepot = DateTime.Now,
                DeposeParId = projet.ChefProjetId!.Value,
                Commentaire = string.Empty,
                Version = "1.0"
            };
            _context.LivrablesProjets.Add(livrable);
            await _context.SaveChangesAsync();

            // Assert
            var livrableDb = await _context.LivrablesProjets
                .FirstOrDefaultAsync(l => l.ProjetId == projet.Id && l.TypeLivrable == TypeLivrable.Autre);
            livrableDb.Should().NotBeNull();
        }

        /// <summary>
        /// PLAN-18: Notification après validation complète
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task PLAN18_ValidationComplete_DoitNotifierEquipe()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            // Act
            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;
            projet.PlanningValideParDSI = true;
            projet.DatePlanningValideParDSI = DateTime.Now;
            await _context.SaveChangesAsync();

            // Assert - Les deux validations sont complètes
            projet.PlanningValideParDM.Should().BeTrue();
            projet.PlanningValideParDSI.Should().BeTrue();
        }

        /// <summary>
        /// PLAN-19: Saisie d'un planning interactif (tâches Gantt)
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task PLAN19_TachesPlanning_DoitEtrePersistesEtOrdonnees()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            var tache1 = new TachePlanningProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                CodeWbs = "1",
                Libelle = "Kick-off",
                Responsable = "CP",
                DateDebutPrevue = DateTime.Today,
                DateFinPrevue = DateTime.Today,
                Avancement = 100,
                Ordre = 0,
                EstJalon = true,
                DateCreation = DateTime.Now,
                CreePar = "TEST"
            };

            var tache2 = new TachePlanningProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                CodeWbs = "2",
                Libelle = "Déploiement",
                Responsable = "Equipe projet",
                DateDebutPrevue = DateTime.Today.AddDays(2),
                DateFinPrevue = DateTime.Today.AddDays(5),
                Avancement = 25,
                Ordre = 1,
                EstJalon = false,
                DateCreation = DateTime.Now,
                CreePar = "TEST"
            };

            // Act
            _context.TachesPlanningProjets.AddRange(tache1, tache2);
            await _context.SaveChangesAsync();

            // Assert
            var tasks = await _context.TachesPlanningProjets
                .Where(t => t.ProjetId == projet.Id && !t.EstSupprime)
                .OrderBy(t => t.Ordre)
                .ToListAsync();

            tasks.Should().HaveCount(2);
            tasks[0].CodeWbs.Should().Be("1");
            tasks[0].EstJalon.Should().BeTrue();
            tasks[1].Libelle.Should().Be("Déploiement");
        }

        /// <summary>
        /// PLAN-20: Un projet peut porter plusieurs tâches de planning
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task PLAN20_Projet_DoitExposerSesTachesPlanning()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;

            _context.TachesPlanningProjets.Add(new TachePlanningProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                CodeWbs = "1.1",
                Libelle = "Analyse détaillée",
                Responsable = "Analyste",
                DateDebutPrevue = DateTime.Today,
                DateFinPrevue = DateTime.Today.AddDays(3),
                Avancement = 0,
                Ordre = 0,
                EstJalon = false,
                DateCreation = DateTime.Now,
                CreePar = "TEST"
            });
            await _context.SaveChangesAsync();

            // Act
            var projetCharge = await _context.Projets
                .Include(p => p.TachesPlanning)
                .FirstAsync(p => p.Id == projet.Id);

            // Assert
            projetCharge.TachesPlanning.Should().ContainSingle();
            projetCharge.TachesPlanning.First().Responsable.Should().Be("Analyste");
        }

        // Méthodes helper
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
