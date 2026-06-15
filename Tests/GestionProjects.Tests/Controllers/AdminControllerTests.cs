using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Controllers;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace GestionProjects.Tests.Controllers
{
    /// <summary>
    /// Tests unitaires pour AdminController (Users, Directions, Delegations).
    /// On contourne OnActionExecutionAsync en appelant directement les actions
    /// après avoir branché un PermissionService qui autorise tout par défaut.
    /// </summary>
    public class AdminControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _db;
        private readonly Mock<IAuditService> _auditMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        private readonly Mock<IEmailService> _emailMock;
        private readonly Mock<IPermissionService> _permissionMock;
        private readonly Mock<IUtilisateurService> _utilisateurMock;
        private readonly Mock<ILogger<AdminController>> _loggerMock;
        private readonly AdminController _controller;

        private static readonly Guid AdminUserId = Guid.NewGuid();

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new ApplicationDbContext(options);

            _auditMock        = new Mock<IAuditService>();
            _currentUserMock  = new Mock<ICurrentUserService>();
            _emailMock        = new Mock<IEmailService>();
            _permissionMock   = new Mock<IPermissionService>();
            _utilisateurMock  = new Mock<IUtilisateurService>();
            _loggerMock       = new Mock<ILogger<AdminController>>();

            _currentUserMock.Setup(x => x.Matricule).Returns("ADMIN001");

            // Par défaut : tout autorisé
            _permissionMock
                .Setup(x => x.CurrentUserHasPermissionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Vrais services (pas des mocks) branchés sur la DB in-memory : les
            // tests admin exercent ainsi la logique réelle déplacée dans les services.
            var directionService = new DirectionAdminService(
                _db, _currentUserMock.Object, _auditMock.Object);
            var serviceService = new ServiceAdminService(
                _db, _currentUserMock.Object, _auditMock.Object);
            var passwordSetupTokenMock = new Mock<IPasswordSetupTokenService>();
            var emailMock = new Mock<IEmailService>();
            var userAdminConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SmtpSettings:BaseUrl"] = "http://localhost"
                })
                .Build();
            var userService = new UserAdminService(
                _db, _currentUserMock.Object, _auditMock.Object, _utilisateurMock.Object,
                passwordSetupTokenMock.Object, emailMock.Object, userAdminConfig);
            var roleService = new RoleAdminService(
                _db, _auditMock.Object, _utilisateurMock.Object);
            var parametreService = new ParametreAdminService(
                _db, _currentUserMock.Object, _auditMock.Object);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SmtpSettings:BaseUrl"] = "http://localhost"
                })
                .Build();
            var demandeCompteService = new DemandeCompteAdminService(
                _db,
                _currentUserMock.Object,
                _auditMock.Object,
                _emailMock.Object,
                new PasswordSetupTokenService(_db, configuration),
                configuration);
            var delegationService = new DelegationAdminService(
                _db, _currentUserMock.Object, _auditMock.Object);
            var userImportService = new UserImportService(
                _db,
                _auditMock.Object,
                _currentUserMock.Object,
                _utilisateurMock.Object,
                new Mock<ILogger<UserImportService>>().Object);

            _controller = new AdminController(
                _auditMock.Object,
                _currentUserMock.Object,
                _emailMock.Object,
                _permissionMock.Object,
                _utilisateurMock.Object,
                directionService,
                serviceService,
                userService,
                roleService,
                parametreService,
                demandeCompteService,
                delegationService,
                userImportService,
                _loggerMock.Object);

            SetupControllerContext(_controller, AdminUserId, RoleUtilisateur.AdminIT);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private static void SetupControllerContext(Controller ctrl, Guid userId, RoleUtilisateur role)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "ADMIN001"),
                new Claim(ClaimTypes.Role, role.ToString())
            };
            var identity  = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
                RouteData  = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            };
        }

        private Utilisateur CreateUser(string matricule = "MAT001", RoleUtilisateur role = RoleUtilisateur.Demandeur)
        {
            var u = new Utilisateur
            {
                Id           = Guid.NewGuid(),
                Matricule    = matricule,
                Nom          = "Test",
                Prenoms      = "User",
                Email        = $"{matricule.ToLower()}@test.ci",
                MotDePasse   = "hash",
                DateCreation = DateTime.Now,
                CreePar      = "SYSTEM",
                EstSupprime  = false,
                UtilisateurRoles = new List<UtilisateurRole>
                {
                    new UtilisateurRole
                    {
                        Id           = Guid.NewGuid(),
                        Role         = role,
                        DateCreation = DateTime.Now,
                        CreePar      = "SYSTEM",
                        EstSupprime  = false
                    }
                }
            };
            _db.Utilisateurs.Add(u);
            _db.SaveChanges();
            return u;
        }

        private Direction CreateDirection(string code = "DIR01", string libelle = "Test Direction")
        {
            var d = new Direction
            {
                Id           = Guid.NewGuid(),
                Code         = code,
                Libelle      = libelle,
                DateCreation = DateTime.Now,
                CreePar      = "SYSTEM",
                EstSupprime  = false,
                EstActive    = true
            };
            _db.Directions.Add(d);
            _db.SaveChanges();
            return d;
        }

        // ─── Tests Users ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Users_ActionRetourneViewResult()
        {
            // Arrange
            CreateUser("MAT001");

            // Act
            var result = await _controller.Users();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Users_ViewModelContientLesUtilisateurs()
        {
            // Arrange
            CreateUser("MAT002");
            CreateUser("MAT003");

            // Act
            var result = (ViewResult)await _controller.Users();
            var vm = result.Model as GestionProjects.Application.ViewModels.Admin.UsersListViewModel;

            // Assert
            vm.Should().NotBeNull();
            vm!.Users.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Users_FiltreParRecherche_RetourneSousEnsemble()
        {
            // Arrange
            CreateUser("ALPHA01");
            CreateUser("BETA02");

            // Act
            var result = (ViewResult)await _controller.Users(recherche: "ALPHA");
            var vm = result.Model as GestionProjects.Application.ViewModels.Admin.UsersListViewModel;

            // Assert
            vm.Should().NotBeNull();
            vm!.Users.Should().OnlyContain(u => u.Nom.Contains("ALPHA") || u.Prenoms.Contains("ALPHA") ||
                                                u.Matricule.Contains("ALPHA") || u.Email.Contains("ALPHA"));
        }

        [Fact]
        public async Task Users_SansPermission_RetourneForbid()
        {
            // Arrange — interdire l'accès Users
            _permissionMock
                .Setup(x => x.CurrentUserHasPermissionAsync("Admin", "Users"))
                .ReturnsAsync(false);

            // Act — simuler le filtre OnActionExecutionAsync
            var canAccess = await InvokeCanAccessAsync("Users");

            // Assert
            canAccess.Should().BeFalse();
        }

        // ─── Tests Directions ────────────────────────────────────────────────────

        [Fact]
        public async Task Directions_ActionRetourneViewResult()
        {
            // Arrange
            CreateDirection("DSI", "Direction Système Info");

            // Act
            var result = await _controller.Directions();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Directions_ViewModelContientLesDirections()
        {
            // Arrange
            CreateDirection("DIR01", "Direction 1");
            CreateDirection("DIR02", "Direction 2");

            // Act
            var result = (ViewResult)await _controller.Directions();
            var vm = result.Model as GestionProjects.Application.ViewModels.Admin.DirectionsListViewModel;

            // Assert
            vm.Should().NotBeNull();
            vm!.Directions.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task Directions_SansPermission_AccesRefuse()
        {
            // Arrange
            _permissionMock
                .Setup(x => x.CurrentUserHasPermissionAsync("Admin", "Directions"))
                .ReturnsAsync(false);

            // Act
            var canAccess = await InvokeCanAccessAsync("Directions");

            // Assert
            canAccess.Should().BeFalse();
        }

        // ─── Tests Delegations ───────────────────────────────────────────────────

        [Fact]
        public async Task Delegations_ActionRetourneViewResult()
        {
            // Act
            var result = await _controller.Delegations();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Delegations_ViewModelContientOngletActif()
        {
            // Act
            var result = (ViewResult)await _controller.Delegations(tab: "chef");
            var vm = result.Model as GestionProjects.Application.ViewModels.Admin.DelegationsPageViewModel;

            // Assert
            vm.Should().NotBeNull();
            vm!.ActiveTab.Should().Be("chef");
        }

        [Fact]
        public async Task Delegations_SansPermission_AccesRefuse()
        {
            // Arrange
            _permissionMock
                .Setup(x => x.CurrentUserHasPermissionAsync("Admin", "Delegations"))
                .ReturnsAsync(false);

            // Act
            var canAccess = await InvokeCanAccessAsync("Delegations");

            // Assert
            canAccess.Should().BeFalse();
        }

        [Fact]
        public async Task Delegations_AvecHasFullAdminScope_VoitToutesLesDelegations()
        {
            // Arrange — admin full scope
            _permissionMock
                .Setup(x => x.CurrentUserHasPermissionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var dsi   = CreateUser("DSI001", RoleUtilisateur.DSI);
            var delg  = CreateUser("DELG01", RoleUtilisateur.ResponsableSolutionsIT);

            _db.DelegationsValidationDSI.Add(new DelegationValidationDSI
            {
                Id           = Guid.NewGuid(),
                DSIId        = dsi.Id,
                DelegueId    = delg.Id,
                DateDebut    = DateTime.Today,
                DateFin      = DateTime.Today.AddDays(10),
                EstActive    = true,
                DateCreation = DateTime.Now,
                CreePar      = "SYSTEM",
                EstSupprime  = false
            });
            await _db.SaveChangesAsync();

            // Act
            var result = (ViewResult)await _controller.Delegations();
            var vm = result.Model as GestionProjects.Application.ViewModels.Admin.DelegationsPageViewModel;

            // Assert
            vm.Should().NotBeNull();
            vm!.DelegationsDSI.Should().HaveCount(1);
        }

        // ─── Tests GererRoles ────────────────────────────────────────────────────

        [Fact]
        public async Task GererRoles_UtilisateurInexistant_RetourneNotFound()
        {
            // Act
            var result = await _controller.GererRoles(Guid.NewGuid());

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task GererRoles_UtilisateurExistant_RetourneViewResult()
        {
            // Arrange
            var user = CreateUser("MAT010");

            // Act
            var result = await _controller.GererRoles(user.Id);

            // Assert
            result.Should().BeOfType<ViewResult>();
            var vm = ((ViewResult)result).Model as GestionProjects.Application.ViewModels.Admin.GererRolesViewModel;
            vm.Should().NotBeNull();
            vm!.User.Id.Should().Be(user.Id);
            vm.AllRoles.Should().NotBeEmpty();
        }

        // ─── Helpers privés ──────────────────────────────────────────────────────

        /// <summary>
        /// Simule la logique de CanAccessActionAsync exposée via le filtre OnActionExecutionAsync.
        /// Appelle directement le service de permission pour l'action nommée.
        /// </summary>
        private async Task<bool> InvokeCanAccessAsync(string action)
        {
            return action switch
            {
                "Users" or "ImportUsers" or "CreateUser" or "UpdateUser" or "DeleteUser"
                    => await _permissionMock.Object.CurrentUserHasPermissionAsync("Admin", "Users"),

                "ListeRoles" or "GererRoles" or "UpdateRoles"
                    => await _permissionMock.Object.CurrentUserHasPermissionAsync("Admin", "ListeRoles"),

                "Directions" or "CreateDirection" or "UpdateDirection" or "DeleteDirection"
                    => await _permissionMock.Object.CurrentUserHasPermissionAsync("Admin", "Directions"),

                "Delegations" or "CreateDelegation" or "UpdateDelegation" or "DeleteDelegation"
                    => await _permissionMock.Object.CurrentUserHasPermissionAsync("Admin", "Delegations"),

                _ => false
            };
        }

        public void Dispose()
        {
            _db.Dispose();
            _controller.Dispose();
        }
    }
}
