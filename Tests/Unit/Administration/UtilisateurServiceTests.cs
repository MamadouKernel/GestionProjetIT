using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Tests.Helpers;
using Moq;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Unit.Administration;

/// <summary>
/// Tests unitaires pour UtilisateurService.
/// Couvre : ParseSelectedRoles, règle AdminIT exclusif,
///          CreateUserAsync, UpdateUserAsync, MatriculeExisteAsync, EmailExisteAsync.
/// </summary>
public class UtilisateurServiceTests : IDisposable
{
    private readonly GestionProjects.Infrastructure.Persistence.ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly UtilisateurService _service;

    public UtilisateurServiceTests()
    {
        _context = TestDbContextFactory.CreateContextWithSeedDataAsync(Guid.NewGuid().ToString()).Result;
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.Matricule).Returns("TEST");
        _currentUser = mockUser.Object;
        _service = new UtilisateurService(_context, _currentUser);
    }

    // ─── ParseSelectedRoles ──────────────────────────────────────────────────

    [Fact]
    public void ParseSelectedRoles_ListeVide_RetourneDemandeur()
    {
        var result = _service.ParseSelectedRoles(null);

        result.Should().ContainSingle()
              .Which.Should().Be(RoleUtilisateur.Demandeur);
    }

    [Fact]
    public void ParseSelectedRoles_ChaineVide_RetourneDemandeur()
    {
        var result = _service.ParseSelectedRoles("   ");

        result.Should().ContainSingle()
              .Which.Should().Be(RoleUtilisateur.Demandeur);
    }

    [Fact]
    public void ParseSelectedRoles_RolesValides_RetourneRolesExacts()
    {
        // Demandeur=1, ChefDeProjet=5
        var result = _service.ParseSelectedRoles("1,5");

        result.Should().HaveCount(2)
              .And.Contain(RoleUtilisateur.Demandeur)
              .And.Contain(RoleUtilisateur.ChefDeProjet);
    }

    [Fact]
    public void ParseSelectedRoles_AdminIT_RetourneSeulementAdminIT()
    {
        // Règle métier : AdminIT est exclusif
        var result = _service.ParseSelectedRoles($"{(int)RoleUtilisateur.AdminIT},{(int)RoleUtilisateur.Demandeur},{(int)RoleUtilisateur.ChefDeProjet}");

        result.Should().ContainSingle()
              .Which.Should().Be(RoleUtilisateur.AdminIT);
    }

    [Fact]
    public void ParseSelectedRoles_AdminITSeul_RetourneAdminIT()
    {
        var result = _service.ParseSelectedRoles($"{(int)RoleUtilisateur.AdminIT}");

        result.Should().ContainSingle()
              .Which.Should().Be(RoleUtilisateur.AdminIT);
    }

    [Fact]
    public void ParseSelectedRoles_RoleInvalide_Ignore()
    {
        // 999 n'est pas défini dans l'enum
        var result = _service.ParseSelectedRoles("999,1");

        result.Should().ContainSingle()
              .Which.Should().Be(RoleUtilisateur.Demandeur);
    }

    [Fact]
    public void ParseSelectedRoles_Doublons_RetourneSansDoublons()
    {
        var result = _service.ParseSelectedRoles("1,1,5,5");

        result.Should().HaveCount(2)
              .And.OnlyHaveUniqueItems();
    }

    // ─── MatriculeExisteAsync / EmailExisteAsync ─────────────────────────────

    [Fact]
    public async Task MatriculeExisteAsync_MatriculeExistant_RetourneTrue()
    {
        var result = await _service.MatriculeExisteAsync("admin");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MatriculeExisteAsync_MatriculeInexistant_RetourneFalse()
    {
        var result = await _service.MatriculeExisteAsync("XXXNONE");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MatriculeExisteAsync_AvecExclusionPropre_RetourneFalse()
    {
        var user = await _context.Utilisateurs.FirstAsync(u => u.Matricule == "admin");

        // Exclure l'utilisateur lui-même → pas de conflit
        var result = await _service.MatriculeExisteAsync("admin", user.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EmailExisteAsync_EmailExistant_RetourneTrue()
    {
        var result = await _service.EmailExisteAsync("admin@cit.ci");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExisteAsync_EmailInexistant_RetourneFalse()
    {
        var result = await _service.EmailExisteAsync("aucun@test.ci");

        result.Should().BeFalse();
    }

    // ─── CreateUserAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_DonneesValides_CreeLUtilisateur()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var ctx = await TestDbContextFactory.CreateContextWithSeedDataAsync(dbName);
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.Matricule).Returns("TEST");
        var svc = new UtilisateurService(ctx, mockUser.Object);

        await svc.CreateUserAsync(
            "NEW001", "Dupont", "Jean", "jean.dupont@test.ci",
            "Secure@123456", null, new[] { RoleUtilisateur.Demandeur });

        await ctx.SaveChangesAsync();

        var saved = await ctx.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u => u.Matricule == "NEW001");

        saved.Should().NotBeNull();
        saved!.Nom.Should().Be("Dupont");
        saved.Email.Should().Be("jean.dupont@test.ci");
        saved.GetRolesActifs().Should().Contain(RoleUtilisateur.Demandeur);
        BCrypt.Net.BCrypt.Verify("Secure@123456", saved.MotDePasse).Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_MotDePasseFaible_LeveException()
    {
        var act = async () => await _service.CreateUserAsync(
            "NEW002", "Test", "User", "test@test.ci",
            "faible", null, new[] { RoleUtilisateur.Demandeur });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*mot de passe*");
    }

    [Fact]
    public async Task CreateUserAsync_MatriculeExistant_LeveException()
    {
        var act = async () => await _service.CreateUserAsync(
            "admin", "Autre", "User", "autre@test.ci",
            "Secure@123456", null, new[] { RoleUtilisateur.Demandeur });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*matricule*");
    }

    [Fact]
    public async Task CreateUserAsync_AdminIT_NePossedePasAutresRoles()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var ctx = await TestDbContextFactory.CreateContextWithSeedDataAsync(dbName);
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.Matricule).Returns("TEST");
        var svc = new UtilisateurService(ctx, mockUser.Object);

        var roles = new[] { RoleUtilisateur.AdminIT, RoleUtilisateur.Demandeur, RoleUtilisateur.ChefDeProjet };

        await svc.CreateUserAsync(
            "ADM999", "Admin", "Nouveau", "admin.nouveau@test.ci",
            "Secure@123456", null, roles);

        await ctx.SaveChangesAsync();

        var saved = await ctx.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstAsync(u => u.Matricule == "ADM999");

        saved.GetRolesActifs().Should().ContainSingle()
             .Which.Should().Be(RoleUtilisateur.AdminIT);
    }

    // ─── SynchronizeUserRolesAsync ───────────────────────────────────────────

    [Fact]
    public async Task SynchronizeUserRolesAsync_AjouterRole_RoleAjouteDansCollection()
    {
        // On vérifie l'état en mémoire — évite le bug connu d'EF InMemory avec SaveChanges
        var user = await _context.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstAsync(u => u.Matricule == "DEM001");

        await _service.SynchronizeUserRolesAsync(
            user, new[] { RoleUtilisateur.Demandeur, RoleUtilisateur.ChefDeProjet });

        user.GetRolesActifs().Should().Contain(RoleUtilisateur.ChefDeProjet);
        user.GetRolesActifs().Should().Contain(RoleUtilisateur.Demandeur);
    }

    [Fact]
    public async Task SynchronizeUserRolesAsync_RetirerRole_RoleMarqueSupprimeEnMemoire()
    {
        var user = await _context.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstAsync(u => u.Matricule == "DEM001");

        await _service.SynchronizeUserRolesAsync(user, new[] { RoleUtilisateur.ChefDeProjet });

        // Vérifier l'état en mémoire après synchronisation
        user.GetRolesActifs().Should().NotContain(RoleUtilisateur.Demandeur);
        user.GetRolesActifs().Should().Contain(RoleUtilisateur.ChefDeProjet);

        // Vérifier que Demandeur est bien marqué EstSupprime
        user.UtilisateurRoles
            .First(r => r.Role == RoleUtilisateur.Demandeur)
            .EstSupprime.Should().BeTrue();
    }

    // ─── UpdateUserAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserAsync_UserInexistant_RetourneFalse()
    {
        var result = await _service.UpdateUserAsync(
            Guid.NewGuid(), "MAT", "Nom", "Pre", "email@test.ci",
            null, new[] { RoleUtilisateur.Demandeur });

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUserAsync_DonneesValides_MisAJour()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var ctx = await TestDbContextFactory.CreateContextWithSeedDataAsync(dbName);
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.Matricule).Returns("TEST");
        var svc = new UtilisateurService(ctx, mockUser.Object);

        var user = await ctx.Utilisateurs.FirstAsync(u => u.Matricule == "DEM001");

        var result = await svc.UpdateUserAsync(
            user.Id, "DEM001", "NouveauNom", "NouveauPrenom",
            "nouveau@test.ci", null, new[] { RoleUtilisateur.Demandeur });

        await ctx.SaveChangesAsync();

        result.Should().BeTrue();
        var updated = await ctx.Utilisateurs.FindAsync(user.Id);
        updated!.Nom.Should().Be("NouveauNom");
        updated.Email.Should().Be("nouveau@test.ci");
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}
