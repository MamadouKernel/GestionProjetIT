using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Helpers;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IPermissionService _permissionService;
        private readonly IPasswordSetupTokenService _passwordSetupTokenService;
        private readonly IDemandeCreationCompteWorkflowService _demandeCreationCompteWorkflow;
        private readonly IDemandeAccesWorkflowService _demandeAccesWorkflow;

        public AccountController(
            IAccountService accountService,
            IPermissionService permissionService,
            IPasswordSetupTokenService passwordSetupTokenService,
            IDemandeCreationCompteWorkflowService demandeCreationCompteWorkflow,
            IDemandeAccesWorkflowService demandeAccesWorkflow)
        {
            _accountService = accountService;
            _permissionService = permissionService;
            _passwordSetupTokenService = passwordSetupTokenService;
            _demandeCreationCompteWorkflow = demandeCreationCompteWorkflow;
            _demandeAccesWorkflow = demandeAccesWorkflow;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Si déjà connecté → redirection vers la page d'accueil
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            var vm = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };
            return View(vm);
        }

        // POST: /Account/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!IsLocalLoginAllowed())
            {
                ModelState.AddModelError(string.Empty, "La connexion locale est désactivée sur cet environnement. Utilisez Microsoft (Azure AD).");
                return View(model);
            }

            // Ne pas valider ReturnUrl - c'est optionnel
            ModelState.Remove(nameof(model.ReturnUrl));
            
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var loginResult = await _accountService.ValidateLocalLoginAsync(model);
            if (!loginResult.Succeeded || loginResult.User == null)
            {
                ModelState.AddModelError(string.Empty, loginResult.ErrorMessage ?? "Matricule ou mot de passe incorrect.");
                return View(model);
            }

            var user = loginResult.User;
            var principal = BuildPrincipal(user);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            try
            {
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    authProperties);

                await _accountService.RecordLoginAsync(user.Id);

                // Vérifier que l'utilisateur est bien authentifié
                if (!HttpContext.User.Identity?.IsAuthenticated == true)
                {
                    // Attendre un peu pour que le cookie soit défini
                    await Task.Delay(100);
                }

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return Redirect(model.ReturnUrl);

                var activeRoles = GetActiveRoles(user);

                var hasProjectWorkspace = false;
                var hasDemandeWorkspace = false;

                foreach (var role in activeRoles)
                {
                    if (!hasProjectWorkspace &&
                        (await _permissionService.HasPermissionAsync(role, "Projet", "Index") ||
                         await _permissionService.HasPermissionAsync(role, "Dashboard", "Index")))
                    {
                        hasProjectWorkspace = true;
                    }

                    if (!hasDemandeWorkspace &&
                        await _permissionService.HasPermissionAsync(role, "DemandeProjet", "Index"))
                    {
                        hasDemandeWorkspace = true;
                    }
                }

                if (hasProjectWorkspace)
                    return RedirectToAction("Index", "Projet");
                if (hasDemandeWorkspace)
                    return RedirectToAction("Index", "DemandeProjet");

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Erreur lors de l'authentification : {ex.Message}");
                return View(model);
            }
        }

        private async Task<IActionResult> SignOutAndRedirectToLoginAsync(string? reason = null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (reason == "inactivity")
            {
                TempData["Info"] = "Votre session a expiré en raison de l'inactivité. Veuillez vous reconnecter.";
            }
            
            return RedirectToAction("Login");
        }

        private static ClaimsPrincipal BuildPrincipal(Utilisateur user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("Matricule", user.Matricule),
                new("Nom", user.Nom ?? string.Empty),
                new("Prenoms", user.Prenoms ?? string.Empty)
            };

            foreach (var role in GetActiveRoles(user))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            if (user.DirectionId.HasValue)
            {
                claims.Add(new Claim("DirectionId", user.DirectionId.Value.ToString()));
                if (user.Direction != null)
                {
                    claims.Add(new Claim("Libelle", user.Direction.Libelle));
                }
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }

        private static List<RoleUtilisateur> GetActiveRoles(Utilisateur user)
        {
            var now = DateTime.UtcNow;
            var roles = user.UtilisateurRoles
                .Where(ur => !ur.EstSupprime &&
                             (!ur.DateDebut.HasValue || ur.DateDebut.Value <= now) &&
                             (!ur.DateFin.HasValue || ur.DateFin.Value >= now))
                .Select(ur => ur.Role)
                .Distinct()
                .ToList();

            if (!roles.Any())
            {
                roles.Add(RoleUtilisateur.Demandeur);
            }

            return roles;
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout(string? reason = null)
        {
            return await SignOutAndRedirectToLoginAsync(reason);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost(string? reason = null)
        {
            return await SignOutAndRedirectToLoginAsync(reason);
        }

        // Compatibilité avec les anciennes pages déjà rendues pointant vers /Account/LogoutPost en GET.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> LogoutPost()
        {
            return await SignOutAndRedirectToLoginAsync();
        }

        // POST: /Account/KeepAlive - Renouvelle la session
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult KeepAlive()
        {
            // Renouveler le cookie d'authentification
            // Le sliding expiration dans Program.cs s'occupera du reste
            // Cette action sert juste à déclencher une requête authentifiée qui renouvelle le cookie
            return Json(new { success = true, message = "Session renouvelée" });
        }

        // GET: /Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult InitialiserMotDePasse(Guid utilisateurId, string token)
        {
            if (utilisateurId == Guid.Empty || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Lien d'activation invalide.";
                return RedirectToAction(nameof(Login));
            }

            return View(new InitialiserMotDePasseViewModel
            {
                UtilisateurId = utilisateurId,
                Token = token
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> InitialiserMotDePasse(InitialiserMotDePasseViewModel model)
        {
            if (!ValidationHelper.IsStrongPassword(model.NouveauMotDePasse))
            {
                ModelState.AddModelError(
                    nameof(model.NouveauMotDePasse),
                    ValidationHelper.StrongPasswordPolicyMessage);
            }

            if (!ModelState.IsValid)
            {
                model.NouveauMotDePasse = string.Empty;
                model.ConfirmerMotDePasse = string.Empty;
                return View(model);
            }

            var result = await _passwordSetupTokenService.InitialiserMotDePasseAsync(
                model.UtilisateurId,
                model.Token,
                model.NouveauMotDePasse,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                "INITIALISATION_MOT_DE_PASSE");

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(error.Field, error.Message);

                model.NouveauMotDePasse = string.Empty;
                model.ConfirmerMotDePasse = string.Empty;
                return View(model);
            }

            TempData["Success"] = "Votre mot de passe a ete initialise. Vous pouvez maintenant vous connecter.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/MotDePasseOublie
        [AllowAnonymous]
        [HttpGet]
        public IActionResult MotDePasseOublie()
        {
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new MotDePasseOublieViewModel());
        }

        // POST: /Account/MotDePasseOublie
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> MotDePasseOublie(MotDePasseOublieViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _accountService.DemarrerReinitialisationMotDePasseAsync(
                model.Matricule.Trim(),
                model.Email.Trim(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            // Message volontairement generique, que le compte existe ou non, pour eviter
            // de reveler si un matricule/email est enregistre dans l'application.
            TempData["Success"] = "Si ces informations correspondent à un compte, un email contenant un lien de réinitialisation vient d'être envoyé.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/LoginMicrosoft
        // Déclenche le flux OIDC Azure AD. Nécessite que le bloc AzureAd soit activé dans Program.cs.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult LoginMicrosoft(string? returnUrl = null)
        {
            if (!IsMicrosoftLoginAllowed())
            {
                TempData["Info"] = "La connexion Microsoft n'est pas disponible sur cet environnement. Utilisez la connexion locale CIT.";
                return RedirectToAction(nameof(Login), new { returnUrl, mode = "local" });
            }

            var redirectUri = string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl)
                ? Url.Action("Index", "Home")!
                : returnUrl;

            var properties = new AuthenticationProperties { RedirectUri = redirectUri };

            // Tente le challenge AzureAd — si le schéma n'est pas enregistré (dev),
            // redirige vers le login avec un message d'information.
            try
            {
                return Challenge(properties, "AzureAd");
            }
            catch
            {
                TempData["Info"] = "La connexion Microsoft n'est pas activée sur cet environnement. Veuillez utiliser vos identifiants CIT.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }
        }

        private IConfiguration? GetConfiguration()
        {
            return HttpContext?.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        }

        private bool IsLocalLoginAllowed()
        {
            var configuration = GetConfiguration();
            var environment = HttpContext?.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) as Microsoft.AspNetCore.Hosting.IWebHostEnvironment;

            if (configuration != null &&
                bool.TryParse(configuration["Authentication:AllowLocalLogin"], out var configuredValue))
            {
                return configuredValue;
            }

            return environment?.IsDevelopment() == true || !IsAzureAdConfigured(configuration);
        }

        private bool IsMicrosoftLoginAllowed()
        {
            var configuration = GetConfiguration();
            var azureAdConfigured = IsAzureAdConfigured(configuration);

            if (!azureAdConfigured)
            {
                return false;
            }

            if (configuration != null &&
                bool.TryParse(configuration["Authentication:AllowMicrosoftLogin"], out var configuredValue))
            {
                return configuredValue;
            }

            return true;
        }

        private static bool IsAzureAdConfigured(IConfiguration? configuration)
        {
            var tenantId = configuration?["AzureAd:TenantId"];
            var clientId = configuration?["AzureAd:ClientId"];
            var clientSecret = configuration?["AzureAd:ClientSecret"];

            return !string.IsNullOrWhiteSpace(tenantId) &&
                   !string.IsNullOrWhiteSpace(clientId) &&
                   !string.IsNullOrWhiteSpace(clientSecret) &&
                   !tenantId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) &&
                   !clientId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) &&
                   !clientSecret.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
        }

        // GET: /Account/Profil
        [Authorize]
        public async Task<IActionResult> Profil()
        {
            var userId = User.GetUserIdOrThrow();
            var vm = await _accountService.GetProfilAsync(userId);
            if (vm == null)
            {
                return NotFound();
            }

            return View(vm);
        }

        // POST: /Account/Profil
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Profil(ProfilViewModel model)
        {
            var userId = User.GetUserIdOrThrow();

            var result = await _accountService.UpdateProfilAsync(userId, model, !ModelState.IsValid);
            if (result.NotFound || result.User == null)
            {
                return NotFound();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            if (!result.Succeeded)
            {
                return View(result.ViewModel);
            }

            var principal = BuildPrincipal(result.User);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            TempData["Success"] = "Profil mis à jour avec succès.";
            return RedirectToAction(nameof(Profil));
        }

        // ─── Inscription (nouveau workflow) ───────────────────────────────────

        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> Inscription()
        {
            if (User?.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            return View(await _accountService.BuildInscriptionViewModelAsync());
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Inscription(
            string nom, string prenoms, string email,
            Guid? directionId, string? serviceLibelle, Guid? directeurMetierId)
        {
            var result = await _demandeCreationCompteWorkflow.SoumettreAsync(
                new SoumettreDemandeCreationCompteInput(
                    nom,
                    prenoms,
                    email,
                    directionId,
                    serviceLibelle,
                    directeurMetierId));

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                TempData["Error"] = result.ErrorMessage;
                return View(await _accountService.BuildInscriptionViewModelAsync());
            }

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction(nameof(Login));
        }

        // AJAX : services par direction
        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> GetServicesByDirection(Guid directionId)
        {
            var services = await _accountService.GetServicesByDirectionAsync(directionId);
            return Json(services);
        }

        // AJAX : DMs par direction
        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> GetDMsByDirection(Guid directionId)
        {
            var dms = await _accountService.GetDirecteursMetierByDirectionAsync(directionId);
            return Json(dms);
        }

        // GET: /Account/DemandeAcces
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> DemandeAcces([FromServices] GestionProjects.Infrastructure.Persistence.ApplicationDbContext db)
        {
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(await BuildDemandeAccesViewModel(db));
        }

        // POST: /Account/DemandeAcces
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> DemandeAcces(
            string nom, string prenoms, string email, string matricule,
            Guid directionId, string rolesSouhaites, string? message,
            [FromServices] GestionProjects.Infrastructure.Persistence.ApplicationDbContext db)
        {
            var result = await _demandeAccesWorkflow.SoumettreDemandeLocaleAsync(
                new SoumettreDemandeAccesLocaleInput(nom, prenoms, email, matricule, directionId, rolesSouhaites, message));

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                TempData["Error"] = result.ErrorMessage;
                return View(await BuildDemandeAccesViewModel(db));
            }

            if (!string.IsNullOrWhiteSpace(result.InfoMessage))
            {
                TempData["Info"] = result.InfoMessage;
                return RedirectToAction(nameof(Login));
            }

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction(nameof(Login));
        }

        private static async Task<GestionProjects.Application.ViewModels.Account.DemandeAccesViewModel> BuildDemandeAccesViewModel(
            GestionProjects.Infrastructure.Persistence.ApplicationDbContext db)
        {
            // Directions actives + indication "a au moins un Directeur Metier rattache".
            // Une direction sans DM ne peut pas accueillir un nouvel utilisateur : le
            // workflow approbation->DM n'aurait personne a solliciter.
            return new GestionProjects.Application.ViewModels.Account.DemandeAccesViewModel
            {
                Directions = await db.Directions
                    .Where(d => !d.EstSupprime && d.EstActive)
                    .OrderBy(d => d.Libelle)
                    .Select(d => new GestionProjects.Application.ViewModels.Account.DirectionOption(
                        d.Id,
                        d.Libelle,
                        db.Utilisateurs.Any(u =>
                            !u.EstSupprime &&
                            u.DirectionId == d.Id &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == GestionProjects.Domain.Enums.RoleUtilisateur.DirecteurMetier))))
                    .ToListAsync()
            };
        }
    }
}
