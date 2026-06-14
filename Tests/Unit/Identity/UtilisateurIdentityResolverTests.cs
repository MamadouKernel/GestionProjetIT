using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Identity;

public sealed class UtilisateurIdentityResolverTests
{
    [Fact]
    public async Task Strict_RetourneErreurQuandEmailExisteAvecUnAutreMatricule()
    {
        await using var db = CreateDbContext();
        db.Utilisateurs.Add(CreateUser("MAT001", "user@cit.test"));
        await db.SaveChangesAsync();

        var resolver = new UtilisateurIdentityResolver(db);

        var result = await resolver.ResolveActiveUserAsync(
            "user@cit.test",
            "MAT999",
            UtilisateurIdentityResolutionMode.Strict);

        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("deja associe au matricule MAT001");
        result.Utilisateur.Should().BeNull();
    }

    [Fact]
    public async Task PreferEmail_RetourneUtilisateurEmailQuandMatriculeAzureEstDifferent()
    {
        await using var db = CreateDbContext();
        var utilisateur = CreateUser("MAT001", "user@cit.test");
        db.Utilisateurs.Add(utilisateur);
        await db.SaveChangesAsync();

        var resolver = new UtilisateurIdentityResolver(db);

        var result = await resolver.ResolveActiveUserAsync(
            "user@cit.test",
            "azure-object-id",
            UtilisateurIdentityResolutionMode.PreferEmail);

        result.HasError.Should().BeFalse();
        result.Utilisateur?.Id.Should().Be(utilisateur.Id);
    }

    [Fact]
    public async Task TousLesModes_RetournentErreurQuandEmailEtMatriculePointentDeuxComptes()
    {
        await using var db = CreateDbContext();
        db.Utilisateurs.Add(CreateUser("MAT001", "first@cit.test"));
        db.Utilisateurs.Add(CreateUser("MAT999", "second@cit.test"));
        await db.SaveChangesAsync();

        var resolver = new UtilisateurIdentityResolver(db);

        var result = await resolver.ResolveActiveUserAsync(
            "first@cit.test",
            "MAT999",
            UtilisateurIdentityResolutionMode.PreferEmail);

        result.HasError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("deux comptes utilisateurs differents");
    }

    private static Utilisateur CreateUser(string matricule, string email)
    {
        return new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = matricule,
            Email = email,
            Nom = "Nom",
            Prenoms = "Prenoms",
            MotDePasse = "hash"
        };
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
