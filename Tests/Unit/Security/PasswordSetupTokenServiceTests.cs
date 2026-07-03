using FluentAssertions;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GestionProjects.Tests.Unit.Security;

public class PasswordSetupTokenServiceTests
{
    [Fact]
    public async Task CreerAsync_DoitStockerUnHashEtPasLeTokenBrut()
    {
        await using var db = CreateDbContext();
        var user = await CreateUserAsync(db);
        var service = CreateService(db);

        var result = await service.CreerAsync(user.Id, "TEST");
        await db.SaveChangesAsync();

        var stored = await db.JetonsInitialisationMotDePasse.SingleAsync();
        stored.TokenHash.Should().NotBe(result.Token);
        stored.TokenHash.Should().NotBeNullOrWhiteSpace();
        stored.DateExpiration.Should().BeAfter(DateTime.Now);
    }

    [Fact]
    public async Task CreerAsync_DoitExpirerBeaucoupPlusViteQuePourUneActivation_QuandReinitialisation()
    {
        await using var db = CreateDbContext();
        var user = await CreateUserAsync(db);
        var service = CreateService(db);

        var activation = await service.CreerAsync(user.Id, "TEST");
        var reinitialisation = await service.CreerAsync(user.Id, "TEST", estReinitialisation: true);

        (reinitialisation.DateExpiration - DateTime.UtcNow).Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1));
        (activation.DateExpiration - DateTime.UtcNow).Should().BeCloseTo(TimeSpan.FromHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task InitialiserMotDePasseAsync_DoitHasherMotDePasseEtConsommerLeJeton()
    {
        await using var db = CreateDbContext();
        var user = await CreateUserAsync(db);
        var service = CreateService(db);
        var token = await service.CreerAsync(user.Id, "TEST");
        await db.SaveChangesAsync();

        var result = await service.InitialiserMotDePasseAsync(
            user.Id,
            token.Token,
            "MotDePasse1234",
            "127.0.0.1",
            "TEST");

        result.Succeeded.Should().BeTrue();
        var reload = await db.Utilisateurs.FindAsync(user.Id);
        reload!.MotDePasse.Should().StartWith("$2");
        BCrypt.Net.BCrypt.Verify("MotDePasse1234", reload.MotDePasse).Should().BeTrue();

        var stored = await db.JetonsInitialisationMotDePasse.SingleAsync();
        stored.DateUtilisation.Should().NotBeNull();
        stored.UtiliseDepuisIp.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task InitialiserMotDePasseAsync_DoitRefuserUnJetonDejaUtilise()
    {
        await using var db = CreateDbContext();
        var user = await CreateUserAsync(db);
        var service = CreateService(db);
        var token = await service.CreerAsync(user.Id, "TEST");
        await db.SaveChangesAsync();
        await service.InitialiserMotDePasseAsync(user.Id, token.Token, "MotDePasse1234", null, "TEST");

        var secondAttempt = await service.InitialiserMotDePasseAsync(
            user.Id,
            token.Token,
            "AutreMotDePasse1234",
            null,
            "TEST");

        secondAttempt.Succeeded.Should().BeFalse();
        secondAttempt.Errors.Should().Contain(e => e.Field == "Token");
    }

    [Fact]
    public async Task InitialiserMotDePasseAsync_DoitRefuserUnJetonExpire()
    {
        await using var db = CreateDbContext();
        var user = await CreateUserAsync(db);
        var service = CreateService(db);
        var token = await service.CreerAsync(user.Id, "TEST");
        await db.SaveChangesAsync();

        var stored = await db.JetonsInitialisationMotDePasse.SingleAsync();
        stored.DateExpiration = DateTime.Now.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await service.InitialiserMotDePasseAsync(
            user.Id,
            token.Token,
            "MotDePasse1234",
            null,
            "TEST");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Token");
    }

    [Fact]
    public async Task InitialiserMotDePasseAsync_DoitRefuserUnMotDePasseFaible()
    {
        await using var db = CreateDbContext();
        var user = await CreateUserAsync(db);
        var service = CreateService(db);
        var token = await service.CreerAsync(user.Id, "TEST");
        await db.SaveChangesAsync();

        var result = await service.InitialiserMotDePasseAsync(
            user.Id,
            token.Token,
            "faible",
            null,
            "TEST");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "NouveauMotDePasse");
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PasswordSetupTokenService CreateService(ApplicationDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:PasswordSetupTokenHours"] = "24"
            })
            .Build();

        return new PasswordSetupTokenService(db, configuration);
    }

    private static async Task<Utilisateur> CreateUserAsync(ApplicationDbContext db)
    {
        var user = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "USR001",
            Nom = "Test",
            Prenoms = "User",
            Email = "user@test.local",
            MotDePasse = string.Empty,
            DateCreation = DateTime.Now,
            CreePar = "TEST",
            EstSupprime = false
        };

        db.Utilisateurs.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
