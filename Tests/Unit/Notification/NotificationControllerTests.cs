using FluentAssertions;
using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Controllers;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace GestionProjects.Tests.Unit.Notification;

public sealed class NotificationControllerTests
{
    [Fact]
    public async Task Ouvrir_DemandeAccesAzureAd_RedirigeVersDemandeCibleeEtMarqueCommeLue()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var demandeId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        db.Utilisateurs.Add(new Utilisateur
        {
            Id = userId,
            Matricule = "ADMIN001",
            Nom = "Admin",
            Prenoms = "IT",
            Email = "admin.it@cit.test",
            MotDePasse = "hash"
        });
        db.Notifications.Add(new GestionProjects.Domain.Models.Notification
        {
            Id = notificationId,
            UtilisateurId = userId,
            TypeNotification = TypeNotification.DemandeSupportTechnique,
            Titre = "Nouvelle demande d'acces",
            Message = "Une demande doit etre traitee.",
            EntiteType = DomainEntityTypes.DemandeAccesAzureAd,
            EntiteId = demandeId,
            DateCreation = DateTime.Now
        });
        await db.SaveChangesAsync();

        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(s => s.MarquerCommeLueAsync(notificationId, userId))
            .Returns(Task.CompletedTask);
        var targetResolver = new Mock<INotificationTargetResolver>();
        targetResolver
            .Setup(r => r.ResolveAsync(DomainEntityTypes.DemandeAccesAzureAd, demandeId))
            .ReturnsAsync(new NotificationTarget(
                "DemandesAcces",
                "Index",
                new Dictionary<string, object?> { ["focusId"] = demandeId }));

        var controller = new NotificationController(db, notificationService.Object, targetResolver.Object)
        {
            ControllerContext = BuildControllerContext(userId)
        };

        var result = await controller.Ouvrir(notificationId);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ControllerName.Should().Be("DemandesAcces");
        redirect.ActionName.Should().Be("Index");
        redirect.RouteValues?["focusId"].Should().Be(demandeId);
        notificationService.Verify(s => s.MarquerCommeLueAsync(notificationId, userId), Times.Once);
        targetResolver.Verify(r => r.ResolveAsync(DomainEntityTypes.DemandeAccesAzureAd, demandeId), Times.Once);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ControllerContext BuildControllerContext(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "ADMIN001")
        };

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) },
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
    }

}
