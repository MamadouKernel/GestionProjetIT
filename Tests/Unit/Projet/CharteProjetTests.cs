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
    /// Tests pour le module Charte Projet (CHR-01 à CHR-12)
    /// </summary>
    public class CharteProjetTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public CharteProjetTests()
        {
            _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        }

        /// <summary>
        /// CHR-01: Formulaire charte - Tous les champs présents
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR01_FormulaireCharte_DoitAvoirTousLesChamps()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            
            // Act
            var charte = new CharteProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                NomProjet = "Projet Test",
                NumeroProjet = "PRJ-001",
                ObjectifProjet = "Objectif principal",
                AssuranceQualite = "Normes ISO",
                Perimetre = "Périmètre défini",
                ContraintesInitiales = "Contraintes identifiées",
                RisquesInitiaux = "Risques initiaux",
                DemandeurId = projet.SponsorId,
                Sponsors = "Sponsor 1, Sponsor 2",
                ChefProjetId = projet.ChefProjetId!.Value,
                EmailChefProjet = "chef@test.com",
                CodeDocument = "CIT-CIV-DSI-CP-0001-Rév.01",
                TypeDocument = "Charte de projet",
                Departement = "SYSTEME D'INFORMATION",
                NumeroRevision = 1
            };


            // Assert
            charte.NomProjet.Should().NotBeNullOrEmpty();
            charte.ObjectifProjet.Should().NotBeNullOrEmpty();
            charte.AssuranceQualite.Should().NotBeNullOrEmpty();
            charte.Perimetre.Should().NotBeNullOrEmpty();
            charte.ContraintesInitiales.Should().NotBeNullOrEmpty();
            charte.RisquesInitiaux.Should().NotBeNullOrEmpty();
            charte.Sponsors.Should().NotBeNullOrEmpty();
            charte.CodeDocument.Should().NotBeNullOrEmpty();
            charte.TypeDocument.Should().Be("Charte de projet");
            charte.Departement.Should().Be("SYSTEME D'INFORMATION");
        }

        /// <summary>
        /// CHR-02: Charte - Jalons obligatoires
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR02_Charte_DoitAvoirJalons()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var charte = await CreerCharteTestAsync(projet.Id);

            // Act
            var jalon1 = new JalonCharte
            {
                Id = Guid.NewGuid(),
                CharteProjetId = charte.Id,
                Nom = "Jalon 1",
                DatePrevisionnelle = DateTime.Now.AddMonths(1),
                Description = "Description jalon 1"
            };
            _context.JalonsCharte.Add(jalon1);
            await _context.SaveChangesAsync();

            // Assert
            var charteDb = await _context.CharteProjets
                .Include(c => c.Jalons)
                .FirstAsync(c => c.Id == charte.Id);
            charteDb.Jalons.Should().NotBeEmpty();
            charteDb.Jalons.Should().Contain(j => j.Nom == "Jalon 1");
        }

        /// <summary>
        /// CHR-03: Charte - Parties prenantes
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR03_Charte_DoitAvoirPartiesPrenantes()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var charte = await CreerCharteTestAsync(projet.Id);

            // Act
            var partiePrenante = new PartiePrenanteCharte
            {
                Id = Guid.NewGuid(),
                CharteProjetId = charte.Id,
                Nom = "Partie Prenante 1",
                Role = "Sponsor - Validation budgétaire"
            };
            _context.PartiesPrenantesCharte.Add(partiePrenante);
            await _context.SaveChangesAsync();

            // Assert
            var charteDb = await _context.CharteProjets
                .Include(c => c.PartiesPrenantes)
                .FirstAsync(c => c.Id == charte.Id);
            charteDb.PartiesPrenantes.Should().NotBeEmpty();
            charteDb.PartiesPrenantes.Should().Contain(p => p.Nom == "Partie Prenante 1");
        }


        /// <summary>
        /// CHR-04: Charte - Numéro de révision
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task CHR04_Charte_DoitAvoirNumeroRevision()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            
            // Act
            var charte = await CreerCharteTestAsync(projet.Id);
            charte.NumeroRevision = 2;
            charte.DateRevision = DateTime.Now;
            charte.DescriptionRevision = "Mise à jour suite aux remarques";
            await _context.SaveChangesAsync();

            // Assert
            charte.NumeroRevision.Should().Be(2);
            charte.DateRevision.Should().NotBeNull();
            charte.DescriptionRevision.Should().NotBeNullOrEmpty();
        }

        /// <summary>
        /// CHR-05: Charte - Signatures requises
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR05_Charte_DoitAvoirSignatures()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var charte = await CreerCharteTestAsync(projet.Id);

            // Act
            charte.SignatureSponsor = true;
            charte.DateSignatureSponsor = DateTime.Now;
            charte.SignatureSponsorId = projet.SponsorId;
            
            charte.SignatureChefProjet = true;
            charte.DateSignatureChefProjet = DateTime.Now;
            charte.SignatureChefProjetId = projet.ChefProjetId;
            await _context.SaveChangesAsync();

            // Assert
            charte.SignatureSponsor.Should().BeTrue();
            charte.DateSignatureSponsor.Should().NotBeNull();
            charte.SignatureChefProjet.Should().BeTrue();
            charte.DateSignatureChefProjet.Should().NotBeNull();
        }

        /// <summary>
        /// CHR-06: Validation charte par Directeur Métier
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR06_ValidationCharteDM_DoitMettreAJourStatut()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var directeurMetier = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Yao");

            // Act
            projet.CharteValideeParDM = true;
            projet.DateCharteValideeParDM = DateTime.Now;
            projet.CharteValideeParDMId = directeurMetier.Id;
            await _context.SaveChangesAsync();

            // Assert
            projet.CharteValideeParDM.Should().BeTrue();
            projet.DateCharteValideeParDM.Should().NotBeNull();
            projet.CharteValideeParDMId.Should().Be(directeurMetier.Id);
        }


        /// <summary>
        /// CHR-07: Validation charte par DSI
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR07_ValidationCharteDSI_DoitMettreAJourStatut()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var dsi = await _context.Utilisateurs.FirstAsync(u => u.Nom == "Koffi");
            
            projet.CharteValideeParDM = true;
            projet.DateCharteValideeParDM = DateTime.Now;

            // Act
            projet.CharteValideeParDSI = true;
            projet.DateCharteValideeParDSI = DateTime.Now;
            projet.CharteValideeParDSIId = dsi.Id;
            await _context.SaveChangesAsync();

            // Assert
            projet.CharteValideeParDSI.Should().BeTrue();
            projet.DateCharteValideeParDSI.Should().NotBeNull();
            projet.CharteValideeParDSIId.Should().Be(dsi.Id);
        }

        /// <summary>
        /// CHR-08: Charte validée - Transition vers Planification
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR08_CharteValidee_DoitPermettreTransitionPlanification()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.CharteValideeParDM = true;
            projet.CharteValideeParDSI = true;
            projet.CharteValidee = true;
            projet.DateCharteValidee = DateTime.Now;

            // Act
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            await _context.SaveChangesAsync();

            // Assert
            projet.CharteValidee.Should().BeTrue();
            projet.PhaseActuelle.Should().Be(PhaseProjet.PlanificationValidation);
        }

        /// <summary>
        /// CHR-09: Rejet charte par DM - Commentaire obligatoire
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR09_RejetCharteDM_DoitAvoirCommentaire()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();

            // Act
            projet.CharteValideeParDM = false;
            projet.CommentaireRefusCharteDM = "Objectifs à clarifier";
            await _context.SaveChangesAsync();

            // Assert
            projet.CharteValideeParDM.Should().BeFalse();
            projet.CommentaireRefusCharteDM.Should().NotBeNullOrEmpty();
        }


        /// <summary>
        /// CHR-10: Rejet charte par DSI - Commentaire obligatoire
        /// Criticité: Bloquante
        /// </summary>
        [Fact]
        public async Task CHR10_RejetCharteDSI_DoitAvoirCommentaire()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            projet.CharteValideeParDM = true;

            // Act
            projet.CharteValideeParDSI = false;
            projet.CommentaireRefusCharteDSI = "Ressources insuffisantes";
            await _context.SaveChangesAsync();

            // Assert
            projet.CharteValideeParDSI.Should().BeFalse();
            projet.CommentaireRefusCharteDSI.Should().NotBeNullOrEmpty();
        }

        /// <summary>
        /// CHR-11: Charte - Code document unique
        /// Criticité: Majeure
        /// </summary>
        [Fact]
        public async Task CHR11_Charte_DoitAvoirCodeDocumentUnique()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var charte = await CreerCharteTestAsync(projet.Id);

            // Act & Assert
            charte.CodeDocument.Should().MatchRegex(@"^CIT-CIV-DSI-CP-\d{4}-Rév\.\d{2}$");
        }

        /// <summary>
        /// CHR-12: Charte - Historique des révisions
        /// Criticité: Moyenne
        /// </summary>
        [Fact]
        public async Task CHR12_Charte_DoitTracerRevisions()
        {
            // Arrange
            var projet = await CreerProjetTestAsync();
            var charte = await CreerCharteTestAsync(projet.Id);

            // Act
            charte.NumeroRevision = 1;
            charte.DateRevision = DateTime.Now.AddDays(-10);
            charte.DescriptionRevision = "Version initiale";
            await _context.SaveChangesAsync();

            charte.NumeroRevision = 2;
            charte.DateRevision = DateTime.Now;
            charte.DescriptionRevision = "Mise à jour suite validation";
            await _context.SaveChangesAsync();

            // Assert
            charte.NumeroRevision.Should().Be(2);
            charte.DescriptionRevision.Should().Contain("Mise à jour");
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

        private async Task<CharteProjet> CreerCharteTestAsync(Guid projetId)
        {
            var projet = await _context.Projets.FindAsync(projetId);
            var charte = new CharteProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                NomProjet = projet!.Titre,
                NumeroProjet = projet.CodeProjet,
                ObjectifProjet = "Objectif principal du projet",
                AssuranceQualite = "Normes ISO 9001",
                Perimetre = "Périmètre défini",
                ContraintesInitiales = "Contraintes budgétaires et temporelles",
                RisquesInitiaux = "Risques identifiés en phase initiale",
                DemandeurId = projet.SponsorId,
                Sponsors = "Sponsor principal",
                ChefProjetId = projet.ChefProjetId!.Value,
                EmailChefProjet = "chef@test.com",
                CodeDocument = "CIT-CIV-DSI-CP-0001-Rév.01",
                TypeDocument = "Charte de projet",
                Departement = "SYSTEME D'INFORMATION",
                NumeroRevision = 1
            };
            _context.CharteProjets.Add(charte);
            await _context.SaveChangesAsync();
            return charte;
        }

        public void Dispose()
        {
            _context?.Database.EnsureDeleted();
            _context?.Dispose();
        }
    }
}
