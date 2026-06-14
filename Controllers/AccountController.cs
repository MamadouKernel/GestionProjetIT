using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Helpers;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Application.ViewModels.Account;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
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
        private readonly ApplicationDbContext _db;
        private readonly IPermissionService _permissionService;
        private readonly IPasswordSetupTokenService _passwordSetupTokenService;
        private readonly IDemandeCreationCompteWorkflowService _demandeCreationCompteWorkflow;
        private readonly IDemandeAccesWorkflowService _demandeAccesWorkflow;

        public AccountController(
            ApplicationDbContext db,
            IPermissionService permissionService,
            IPasswordSetupTokenService passwordSetupTokenService,
            IDemandeCreationCompteWorkflowService demandeCreationCompteWorkflow,
            IDemandeAccesWorkflowService demandeAccesWorkflow)
        {
            _db = db;
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

            // On cherche l'utilisateur par login avec ses rôles
            var user = await _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Matricule == model.Matricule && !u.EstSupprime);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Matricule ou mot de passe incorrect.");
                return View(model);
            }

            // Vérifier que le mot de passe n'est pas vide
            if (string.IsNullOrEmpty(user.MotDePasse))
            {
                ModelState.AddModelError(string.Empty, "Ce compte est en attente d'activation. Utilisez le lien recu par email ou contactez la DSI.");
                return View(model);
            }

            // Vérification du mot de passe (BCrypt)
            bool passwordOk = false;
            try
            {
                if (string.IsNullOrEmpty(user.MotDePasse))
                {
                    ModelState.AddModelError(string.Empty, "Erreur : mot de passe utilisateur invalide.");
                    return View(model);
                }

                // Vérifier le format du hash BCrypt
                if (!user.MotDePasse.StartsWith("$2"))
                {
                    ModelState.AddModelError(string.Empty, "Erreur : format de mot de passe invalide.");
                    return View(model);
                }

                passwordOk = BCrypt.Net.BCrypt.Verify(model.MotDePasse, user.MotDePasse);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Erreur lors de la vérification du mot de passe : {ex.Message}");
                return View(model);
            }

            if (!passwordOk)
            {
                ModelState.AddModelError(string.Empty, "Matricule ou mot de passe incorrect.");
                return View(model);
            }

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

                // Mise à jour suivi connexion
                user.NombreConnexion += 1;
                user.DateDerniereConnexion = DateTime.Now;
                await _db.SaveChangesAsync();

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
            var now = DateTime.Now;
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
                "ACTIVATION_COMPTE");

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
            var user = await _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.EstSupprime);

            if (user == null)
            {
                return NotFound();
            }

            var vm = new ProfilViewModel
            {
                Id = user.Id,
                Matricule = user.Matricule,
                Nom = user.Nom,
                Prenoms = user.Prenoms,
                Email = user.Email,
                DirectionLibelle = user.Direction?.Libelle,
                Role = user.Role.ToString(),
                DateDerniereConnexion = user.DateDerniereConnexion,
                NombreConnexion = user.NombreConnexion
            };

            return View(vm);
        }

        // POST: /Account/Profil
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Profil(ProfilViewModel model)
        {
            var userId = User.GetUserIdOrThrow();
            var user = await _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.EstSupprime);

            if (user == null)
            {
                return NotFound();
            }

            // Si changement de mot de passe demandé
            if (!string.IsNullOrEmpty(model.NouveauMotDePasse))
            {
                // Vérifier le mot de passe actuel
                if (string.IsNullOrEmpty(model.MotDePasseActuel))
                {
                    ModelState.AddModelError(nameof(model.MotDePasseActuel), "Le mot de passe actuel est requis pour changer le mot de passe.");
                }
                else
                {
                    try
                    {
                        if (!BCrypt.Net.BCrypt.Verify(model.MotDePasseActuel, user.MotDePasse))
                        {
                            ModelState.AddModelError(nameof(model.MotDePasseActuel), "Le mot de passe actuel est incorrect.");
                        }
                        else
                        {
                            // Hash du nouveau mot de passe
                            user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(model.NouveauMotDePasse);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(string.Empty, $"Erreur lors de la vérification du mot de passe : {ex.Message}");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                // Réinitialiser les champs de mot de passe pour ne pas les afficher
                model.NouveauMotDePasse = null;
                model.ConfirmerMotDePasse = null;
                model.MotDePasseActuel = null;
                model.DirectionLibelle = user.Direction?.Libelle;
                model.Role = user.Role.ToString();
                model.DateDerniereConnexion = user.DateDerniereConnexion;
                model.NombreConnexion = user.NombreConnexion;
                return View(model);
            }

            // Mettre à jour les informations
            user.Nom = model.Nom;
            user.Prenoms = model.Prenoms;
            user.Email = model.Email;

            await _db.SaveChangesAsync();

            var principal = BuildPrincipal(user);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            TempData["Success"] = "Profil mis à jour avec succès.";
            return RedirectToAction(nameof(Profil));
        }

        // ─── Inscription (nouveau workflow) ───────────────────────────────────

        private async Task<InscriptionViewModel> BuildInscriptionViewModelAsync()
        {
            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .Select(d => new DirectionSelectItem { Id = d.Id, Libelle = d.Libelle })
                .ToListAsync();
            return new InscriptionViewModel { Directions = directions };
        }

        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> Inscription()
        {
            if (User?.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            return View(await BuildInscriptionViewModelAsync());
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
                return View(await BuildInscriptionViewModelAsync());
            }

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction(nameof(Login));
        }

        // AJAX : services par direction
        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> GetServicesByDirection(Guid directionId)
        {
            var services = await _db.Services
                .Where(s => s.DirectionId == directionId && !s.EstSupprime && s.EstActive)
                .OrderBy(s => s.Libelle)
                .Select(s => new { s.Id, s.Libelle })
                .ToListAsync();
            return Json(services);
        }

        // AJAX : DMs par direction
        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> GetDMsByDirection(Guid directionId)
        {
            var dms = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && u.DirectionId == directionId)
                .Join(_db.UtilisateurRoles.Where(r => r.Role == RoleUtilisateur.DirecteurMetier && !r.EstSupprime),
                      u => u.Id, r => r.UtilisateurId, (u, r) => u)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .Select(u => new { id = u.Id, libelle = (u.Nom + " " + u.Prenoms).Trim() })
                .ToListAsync();
            return Json(dms);
        }

        // GET: /Account/DemandeAcces
        [AllowAnonymous]
        [HttpGet]
        public IActionResult DemandeAcces()
        {
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        // POST: /Account/DemandeAcces
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> DemandeAcces(string nom, string prenoms, string email, string matricule, string rolesSouhaites, string? message)
        {
            var result = await _demandeAccesWorkflow.SoumettreDemandeLocaleAsync(
                new SoumettreDemandeAccesLocaleInput(nom, prenoms, email, matricule, rolesSouhaites, message));

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                TempData["Error"] = result.ErrorMessage;
                return View();
            }

            if (!string.IsNullOrWhiteSpace(result.InfoMessage))
            {
                TempData["Info"] = result.InfoMessage;
                return RedirectToAction(nameof(Login));
            }

            TempData["Success"] = result.SuccessMessage;
            return RedirectToAction(nameof(Login));
        }

        private static bool EstActif(Utilisateur? u)
        {
            return u != null && !u.EstSupprime;
        }
    }
}
