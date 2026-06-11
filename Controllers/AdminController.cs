using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionProjects.Controllers
{
    public class DelegationsViewModel
    {
        public IEnumerable<DelegationValidationDSI> DelegationsDSI { get; set; } = new List<DelegationValidationDSI>();
        public IEnumerable<DelegationChefProjet> DelegationsChefProjet { get; set; } = new List<DelegationChefProjet>();
    }

    [Authorize]
    public partial class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailService _email;
        private readonly IPermissionService _permissionService;
        private readonly IUtilisateurService _utilisateurService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService,
            IEmailService email,
            IPermissionService permissionService,
            IUtilisateurService utilisateurService,
            ILogger<AdminController> logger)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
            _email = email;
            _permissionService = permissionService;
            _utilisateurService = utilisateurService;
            _logger = logger;
        }

        private static List<RoleUtilisateur> ParseSelectedRoles(string? roles)
        {
            var roleIds = (roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value.Trim(), out var roleId) ? roleId : 0)
                .Where(roleId => roleId > 0)
                .Distinct()
                .ToList();

            var parsedRoles = roleIds
                .Select(roleId => (RoleUtilisateur)roleId)
                .Where(role => Enum.IsDefined(typeof(RoleUtilisateur), role))
                .Distinct()
                .ToList();

            if (!parsedRoles.Any())
                parsedRoles.Add(RoleUtilisateur.Demandeur);

            if (parsedRoles.Contains(RoleUtilisateur.AdminIT))
                return new List<RoleUtilisateur> { RoleUtilisateur.AdminIT };

            return parsedRoles;
        }

        private async Task SynchronizeUserRolesAsync(Utilisateur user, IEnumerable<RoleUtilisateur> selectedRoles)
        {
            await _utilisateurService.SynchronizeUserRolesAsync(user, selectedRoles);
        }

        private async Task UpsertParametreSystemeAsync(string cle, string valeur, string description)
        {
            var parametre = await _db.ParametresSysteme.FirstOrDefaultAsync(p => p.Cle == cle && !p.EstSupprime);
            if (parametre == null)
            {
                _db.ParametresSysteme.Add(new ParametreSysteme
                {
                    Id = Guid.NewGuid(),
                    Cle = cle,
                    Valeur = valeur,
                    Description = description,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                });
            }
            else
            {
                parametre.Valeur = valeur;
                parametre.Description = description;
                parametre.DateModification = DateTime.Now;
                parametre.ModifiePar = _currentUserService.Matricule;
            }
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;

            if (!await CanAccessActionAsync(action))
            {
                context.Result = new ForbidResult();
                return;
            }

            await next();
        }

        private async Task<bool> CanAccessActionAsync(string action)
        {
            return action switch
            {
                nameof(Users) or
                nameof(ImportUsers) or
                nameof(DownloadModeleImportUsers) or
                nameof(GetUser) or
                nameof(CreateUser) or
                nameof(UpdateUser) or
                nameof(DeleteUser) or
                nameof(ResetPassword) => await CanManageUsersAsync(),

                nameof(ListeRoles) or
                nameof(GererRoles) or
                nameof(UpdateRoles) => await CanManageRolesAsync(),

                nameof(Directions) or
                nameof(GetDirectionCode) or
                nameof(CreateDirection) or
                nameof(UpdateDirection) or
                nameof(DeleteDirection) => await CanManageDirectionsAsync(),

                nameof(Services) or
                nameof(CreateService) or
                nameof(UpdateService) or
                nameof(DeleteService) => await CanManageServicesAsync(),

                nameof(Parametres) or
                nameof(EnregistrerParametresWorkflow) or
                nameof(GetParametre) or
                nameof(CreateParametre) or
                nameof(UpdateParametre) or
                nameof(DeleteParametre) or
                nameof(SaveTeamsWebhook) => await CanManageParametersAsync(),

                nameof(Delegations) or
                nameof(GetDelegation) or
                nameof(CreateDelegation) or
                nameof(UpdateDelegation) or
                nameof(DeleteDelegation) or
                nameof(DelegationsChefProjet) or
                nameof(GetDelegationChefProjet) or
                nameof(CreateDelegationChefProjet) or
                nameof(UpdateDelegationChefProjet) or
                nameof(DeleteDelegationChefProjet) => await CanManageDelegationsAsync(),

                nameof(DemandesCreationCompte) or
                nameof(ValiderDemandeCreationCompteDM) or
                nameof(RefuserDemandeCreationCompteDM) or
                nameof(ValiderDemandeCreationCompteDSI) or
                nameof(RefuserDemandeCreationCompteDSI) => await CanAccessAccountRequestWorkflowAsync(),

                _ => true
            };
        }

        private async Task<bool> CanManageUsersAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "Users");
        }

        private async Task<bool> CanManageRolesAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "ListeRoles");
        }

        private async Task<bool> CanManageDirectionsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "Directions");
        }

        private async Task<bool> CanManageServicesAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "Services");
        }

        private async Task<bool> CanManageParametersAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "Parametres");
        }

        private async Task<bool> CanManageDelegationsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "Delegations");
        }

        private async Task<bool> HasFullAdminScopeAsync()
        {
            return await CanManageUsersAsync() ||
                   await _permissionService.CurrentUserHasPermissionAsync("Autorisations", "Index") ||
                   await _permissionService.CurrentUserHasPermissionAsync("DemandesAcces", "Index");
        }

        private async Task<bool> CanValidateAccountRequestAsDmAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "ValiderDemandeCreationCompteDM") ||
                   await _permissionService.CurrentUserHasPermissionAsync("Admin", "RefuserDemandeCreationCompteDM");
        }

        private async Task<bool> CanValidateAccountRequestAsDsiAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "ValiderDemandeCreationCompteDSI") ||
                   await _permissionService.CurrentUserHasPermissionAsync("Admin", "RefuserDemandeCreationCompteDSI");
        }

        private async Task<bool> CanAccessAccountRequestWorkflowAsync()
        {
            return await HasFullAdminScopeAsync() ||
                   await CanValidateAccountRequestAsDmAsync() ||
                   await CanValidateAccountRequestAsDsiAsync();
        }
    }
}
