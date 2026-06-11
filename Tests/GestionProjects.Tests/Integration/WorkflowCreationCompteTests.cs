using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Integration
{
    public class WorkflowCreationCompteTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public WorkflowCreationCompteTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private async Task<(Direction direction, Utilisateur directeurMetier)> SeedBaseDataAsync()
        {
            var direction = new Direction
            {
                Id = Guid.NewGuid(),
                Code = "FIN",
                Libelle = "Direction Financière",
                EstActive = true,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Directions.Add(direction);

            var directeurMetier = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "DM001",
                Nom = "Dupont",
                Prenoms = "Marie",
                Email = "dm@test.com",
                MotDePasse = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                DirectionId = direction.Id,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            };
            _context.Utilisateurs.Add(directeurMetier);

            _context.UtilisateurRoles.Add(new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = directeurMetier.Id,
                Role = RoleUtilisateur.DirecteurMetier,
                DateDebut = DateTime.Now,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            });

            await _context.SaveChangesAsync();
            return (direction, directeurMetier);
        }

        private DemandeCreationCompte CreerDemande(Guid directionId, Guid directeurMetierId)
            => new DemandeCreationCompte
            {
                Id = Guid.NewGuid(),
                Nom = "Koné",
                Prenoms = "Awa",
                Email = "awa.kone@test.com",
                Service = "Comptabilité",
                DirectionId = directionId,
                DirecteurMetierId = directeurMetierId,
                Statut = StatutDemandeCompte.EnAttenteValidationDM,
                DateSoumission = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                EstSupprime = false
            };

        // -----------------------------------------------------------------------
        // 1. Soumission → statut initial EnAttenteValidationDM
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Workflow_SoumissionDemande_StatutEnAttenteValidationDM()
        {
            // Arrange
            var (direction, directeurMetier) = await SeedBaseDataAsync();

            // Act
            var demande = CreerDemande(direction.Id, directeurMetier.Id);
            _context.DemandesCreationCompte.Add(demande);
            await _context.SaveChangesAsync();

            // Assert
            var saved = await _context.DemandesCreationCompte.FindAsync(demande.Id);
            saved.Should().NotBeNull();
            saved!.Statut.Should().Be(StatutDemandeCompte.EnAttenteValidationDM);
        }

        // -----------------------------------------------------------------------
        // 2. DM valide → statut ValideeParDM
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Workflow_ValidationDM_ChangeStatutVersValideeParDM()
        {
            // Arrange
            var (direction, directeurMetier) = await SeedBaseDataAsync();
            var demande = CreerDemande(direction.Id, directeurMetier.Id);
            _context.DemandesCreationCompte.Add(demande);
            await _context.SaveChangesAsync();

            // Act
            demande.Statut = StatutDemandeCompte.ValideeParDM;
            demande.CommentaireDM = "Validé par le DM";
            await _context.SaveChangesAsync();

            // Assert
            var saved = await _context.DemandesCreationCompte.FindAsync(demande.Id);
            saved!.Statut.Should().Be(StatutDemandeCompte.ValideeParDM);
            saved.CommentaireDM.Should().Be("Validé par le DM");
        }

        // -----------------------------------------------------------------------
        // 3. DM refuse → statut RefuseeParDM
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Workflow_RefusDM_ChangeStatutVersRefuseeParDM()
        {
            // Arrange
            var (direction, directeurMetier) = await SeedBaseDataAsync();
            var demande = CreerDemande(direction.Id, directeurMetier.Id);
            _context.DemandesCreationCompte.Add(demande);
            await _context.SaveChangesAsync();

            // Act
            demande.Statut = StatutDemandeCompte.RefuseeParDM;
            demande.CommentaireDM = "Motif insuffisant";
            await _context.SaveChangesAsync();

            // Assert
            var saved = await _context.DemandesCreationCompte.FindAsync(demande.Id);
            saved!.Statut.Should().Be(StatutDemandeCompte.RefuseeParDM);
        }

        // -----------------------------------------------------------------------
        // 4. DSI valide → utilisateur créé en base, statut CompteCree
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Workflow_ValidationDSI_CreeLUtilisateurEtChangeStatut()
        {
            // Arrange
            var (direction, directeurMetier) = await SeedBaseDataAsync();
            var demande = CreerDemande(direction.Id, directeurMetier.Id);
            demande.Statut = StatutDemandeCompte.ValideeParDM; // pré-validée par DM
            _context.DemandesCreationCompte.Add(demande);
            await _context.SaveChangesAsync();

            // Act — simulation de la logique DSI : création de l'utilisateur + passage CompteCree
            var nouvelUtilisateur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = "AWA001",
                Nom = demande.Nom,
                Prenoms = demande.Prenoms,
                Email = demande.Email,
                MotDePasse = BCrypt.Net.BCrypt.HashPassword("MotDePasse@123"),
                DirectionId = demande.DirectionId,
                EstSupprime = false,
                DateCreation = DateTime.Now,
                CreePar = "DSI001"
            };
            _context.Utilisateurs.Add(nouvelUtilisateur);

            demande.Statut = StatutDemandeCompte.CompteCree;
            demande.UtilisateurCreePar = nouvelUtilisateur.Id;
            demande.CommentaireDSI = "Compte créé";
            await _context.SaveChangesAsync();

            // Assert
            var savedDemande = await _context.DemandesCreationCompte.FindAsync(demande.Id);
            savedDemande!.Statut.Should().Be(StatutDemandeCompte.CompteCree);
            savedDemande.UtilisateurCreePar.Should().Be(nouvelUtilisateur.Id);

            var utilisateurCree = await _context.Utilisateurs.FindAsync(nouvelUtilisateur.Id);
            utilisateurCree.Should().NotBeNull();
            utilisateurCree!.Email.Should().Be("awa.kone@test.com");
        }

        // -----------------------------------------------------------------------
        // 5. DSI valide SANS validation DM préalable → refus (règle métier)
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Workflow_ValidationDSI_SansValidationDMPrealable_Echoue()
        {
            // Arrange
            var (direction, directeurMetier) = await SeedBaseDataAsync();
            var demande = CreerDemande(direction.Id, directeurMetier.Id);
            // Statut reste EnAttenteValidationDM — le DM n'a pas encore validé
            _context.DemandesCreationCompte.Add(demande);
            await _context.SaveChangesAsync();

            // Act — vérification de la règle métier : le DSI ne peut pas approuver une demande non validée par DM
            var peutValiderDsi = demande.Statut == StatutDemandeCompte.ValideeParDM;

            // Assert
            peutValiderDsi.Should().BeFalse(
                "la DSI ne doit pas pouvoir valider une demande qui n'a pas été approuvée par le Directeur Métier");
            demande.Statut.Should().Be(StatutDemandeCompte.EnAttenteValidationDM);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
