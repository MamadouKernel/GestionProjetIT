using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GestionProjects.Tests.Integration;

/// <summary>
/// Tests d'intégration sur un moteur SQLite RELATIONNEL : ils valident les
/// garanties de la couche données qui n'existent PAS sous le provider InMemory
/// (index uniques, index filtrés/partiels, filtres de requête globaux en SQL,
/// override d'audit dans SaveChanges).
/// </summary>
public class DataLayerSqliteTests
{
    private static Utilisateur NouvelUtilisateur(string matricule, string email)
    {
        return new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = matricule,
            MotDePasse = "hash",
            Nom = "Nom",
            Prenoms = "Prenom",
            Email = email
        };
    }

    [Fact]
    public void EnsureCreated_AppliqueLeSchemaSansErreur()
    {
        // La création du schéma depuis le modèle (index filtrés "[EstSupprime] = 0",
        // précisions décimales, longueurs bornées) doit réussir sur SQLite.
        var act = () => new SqliteTestDb();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task FiltreSoftDelete_ExclutLesLignesSupprimees()
    {
        using var db = new SqliteTestDb();

        var direction = new Direction { Id = Guid.NewGuid(), Code = "DSI", Libelle = "Systèmes d'Information", EstActive = true };
        db.Context.Directions.Add(direction);
        await db.Context.SaveChangesAsync();

        // Soft-delete
        direction.EstSupprime = true;
        await db.Context.SaveChangesAsync();

        // Le filtre global doit l'exclure des requêtes normales…
        (await db.Context.Directions.CountAsync()).Should().Be(0);
        // …mais la ligne existe toujours physiquement.
        (await db.Context.Directions.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task IndexUniqueMatricule_RefuseDeuxComptesActifsIdentiques()
    {
        using var db = new SqliteTestDb();

        db.Context.Utilisateurs.Add(NouvelUtilisateur("DUP001", "a@cit.ci"));
        db.Context.Utilisateurs.Add(NouvelUtilisateur("DUP001", "b@cit.ci"));

        Func<Task> act = async () => await db.Context.SaveChangesAsync();

        // L'InMemory accepterait ce doublon ; SQLite applique l'index unique filtré.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task IndexUniqueMatricule_AutoriseLaReutilisationApresSoftDelete()
    {
        using var db = new SqliteTestDb();

        var premier = NouvelUtilisateur("REUSE01", "premier@cit.ci");
        db.Context.Utilisateurs.Add(premier);
        await db.Context.SaveChangesAsync();

        // Soft-delete : la ligne sort du filtre "[EstSupprime] = 0" de l'index.
        premier.EstSupprime = true;
        await db.Context.SaveChangesAsync();

        // Réutiliser le même matricule pour un nouveau compte actif doit passer.
        db.Context.Utilisateurs.Add(NouvelUtilisateur("REUSE01", "second@cit.ci"));
        Func<Task> act = async () => await db.Context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
        (await db.Context.Utilisateurs.CountAsync(u => u.Matricule == "REUSE01")).Should().Be(1);
    }

    [Fact]
    public async Task Audit_RenseigneCreeParEtDateCreation_ALInsertion()
    {
        using var db = new SqliteTestDb(); // pas de ICurrentUserService -> "SYSTEM"

        var direction = new Direction { Id = Guid.NewGuid(), Code = "FIN", Libelle = "Finance", EstActive = true };
        db.Context.Directions.Add(direction);
        await db.Context.SaveChangesAsync();

        direction.CreePar.Should().Be("SYSTEM");
        direction.DateCreation.Should().NotBe(default);
        direction.ModifiePar.Should().BeNull();
    }

    [Fact]
    public async Task Audit_RenseigneModifieParDuUtilisateurCourant_ALaModification()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(s => s.Matricule).Returns("TESTUSER");
        currentUser.SetupGet(s => s.Roles).Returns(Array.Empty<string>());

        using var db = new SqliteTestDb(currentUser.Object);

        var direction = new Direction { Id = Guid.NewGuid(), Code = "RH", Libelle = "Ressources Humaines", EstActive = true };
        db.Context.Directions.Add(direction);
        await db.Context.SaveChangesAsync();

        direction.Libelle = "RH modifiée";
        await db.Context.SaveChangesAsync();

        direction.ModifiePar.Should().Be("TESTUSER");
        direction.DateModification.Should().NotBeNull();
    }
}
