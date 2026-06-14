using FluentAssertions;
using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Controllers;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Claims;
using Xunit;

namespace GestionProjects.Tests.Unit.DemandesAcces;

public sealed class DemandesAccesControllerTests
{
    [Fact]
    public async Task Approuver_ReutiliseUtilisateurExistantQuandMatriculeExiste()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var utilisateurExistantId = Guid.NewGuid();
        var demandeId = Guid.NewGuid();

        db.Utilisateurs.AddRange(
            new Utilisateur
            {
                Id = adminId,
                Matricule = "ADMIN001",
                Nom = "Admin",
                Prenoms = "IT",
                Email = "admin.it@cit.test",
                MotDePasse = "hash"
            },
            new Utilisateur
            {
                Id = utilisateurExistantId,
                Matricule = "2414",
                Nom = "Ancien",
                Prenoms = "Compte",
                Email = "ancienne.adresse@cit.test",
                MotDePasse = "hash"
            });
        db.DemandesAccesAzureAd.Add(new DemandeAccesAzureAd
        {
            Id = demandeId,
            Nom = "Kouadio",
            Prenoms = "Raissa",
            Email = "raissa.kouadio@cit.test",
            Matricule = "2414",
            AzureDepartment = AccessRequestConstants.LocalAzureDepartment,
            Justification = AccessRequestConstants.LocalAccountLabel,
            Statut = StatutDemandeAcces.EnAttente,
            DateCreation = DateTime.Now
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, adminId);

        var result = await controller.Approuver(
            demandeId,
            directionId: null,
            commentaire: "OK",
            role: RoleUtilisateur.Demandeur);

        result.Should().BeOfType<RedirectToActionResult>();

        var utilisateursAvecMatricule = await db.Utilisateurs
            .Where(u => u.Matricule == "2414" && !u.EstSupprime)
            .ToListAsync();
        utilisateursAvecMatricule.Should().HaveCount(1);

        var demande = await db.DemandesAccesAzureAd.SingleAsync(d => d.Id == demandeId);
        demande.Statut.Should().Be(StatutDemandeAcces.Approuvee);
        demande.UtilisateurCreeId.Should().Be(utilisateurExistantId);

        var roleAttribue = await db.UtilisateurRoles.AnyAsync(ur =>
            ur.UtilisateurId == utilisateurExistantId &&
            ur.Role == RoleUtilisateur.Demandeur &&
            !ur.EstSupprime);
        roleAttribue.Should().BeTrue();
    }

    private static DemandesAccesController BuildController(ApplicationDbContext db, Guid adminId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.Matricule).Returns("ADMIN001");
        currentUser.SetupGet(c => c.Roles).Returns(new[] { RoleUtilisateur.AdminIT.ToString() });

        var audit = new Mock<IAuditService>();
        audit
            .Setup(a => a.LogActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .Returns(Task.CompletedTask);

        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var permission = new Mock<IPermissionService>();
        permission
            .Setup(p => p.CurrentUserHasPermissionAsync("DemandesAcces", "Index"))
            .ReturnsAsync(true);

        var passwordSetupToken = new Mock<IPasswordSetupTokenService>();
        var notification = new Mock<INotificationService>();
        var configuration = new ConfigurationBuilder().Build();
        var workflow = new DemandeAccesWorkflowService(
            db,
            currentUser.Object,
            audit.Object,
            email.Object,
            notification.Object,
            passwordSetupToken.Object,
            new UtilisateurIdentityResolver(db),
            configuration);

        var controller = new DemandesAccesController(
            db,
            permission.Object,
            workflow);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Name, "ADMIN001")
        };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return controller;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
