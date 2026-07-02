using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels.AzureAuth;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    [AllowAnonymous]
    public class AzureAuthController : Controller
    {
        private const string AzureAdScheme = "AzureAd";
        private readonly ILogger<AzureAuthController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IUtilisateurIdentityResolver _identityResolver;
        private readonly IAzureAuthWorkflowService _azureAuthWorkflow;

        public AzureAuthController(
            ILogger<AzureAuthController> logger,
            IConfiguration configuration,
            IUtilisateurIdentityResolver identityResolver,
            IAzureAuthWorkflowService azureAuthWorkflow)
        {
            _logger = logger;
            _configuration = configuration;
            _identityResolver = identityResolver;
            _azureAuthWorkflow = azureAuthWorkflow;
        }

        [HttpGet]
        public IActionResult SignIn(string? returnUrl = null)
        {
            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ||
                tenantId == "VOTRE_TENANT_ID" || clientId == "VOTRE_CLIENT_ID" || clientSecret == "VOTRE_CLIENT_SECRET" ||
                tenantId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ||
                clientId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ||
                clientSecret.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Tentative de connexion Azure AD avec une configuration incomplète");
                TempData["ErrorMessage"] = "Azure AD n'est pas configuré. Veuillez utiliser la connexion locale ou contacter l'administrateur.";
                return RedirectToAction("Login", "Account");
            }

            var redirectUrl = Url.Action("SignInCallback", "AzureAuth", null, Request.Scheme);
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            return Challenge(properties, AzureAdScheme);
        }

        [HttpGet]
        public async Task<IActionResult> SignInCallback()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogError("Échec de l'authentification Azure AD");
                    TempData["Error"] = "Erreur lors de l'authentification Azure AD. Veuillez réessayer.";
                    return RedirectToAction("Login", "Account");
                }

                var azureUser = User;
                var email = azureUser.FindFirst(ClaimTypes.Email)?.Value
                    ?? azureUser.FindFirst("preferred_username")?.Value
                    ?? azureUser.FindFirst("upn")?.Value;

                var nom = azureUser.FindFirst(ClaimTypes.Surname)?.Value
                    ?? azureUser.FindFirst("family_name")?.Value;

                var prenom = azureUser.FindFirst(ClaimTypes.GivenName)?.Value
                    ?? azureUser.FindFirst("given_name")?.Value;

                var matricule = azureUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? azureUser.FindFirst("oid")?.Value
                    ?? email?.Split('@')[0];

                var azureDepartment = GetAzureDepartment(azureUser);
                var directionDetectee = await _azureAuthWorkflow.DetectDirectionAsync(azureDepartment);

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(matricule))
                {
                    _logger.LogError("Impossible de récupérer les informations de l'utilisateur Azure AD");
                    TempData["Error"] = "Erreur lors de la récupération de vos informations. Veuillez réessayer.";
                    return RedirectToAction("Login", "Account");
                }

                var identityResolution = await _identityResolver.ResolveActiveUserAsync(
                    email,
                    matricule,
                    UtilisateurIdentityResolutionMode.PreferEmail,
                    includeRoles: true,
                    includeDirection: true);

                if (identityResolution.HasError)
                {
                    _logger.LogWarning(
                        "Connexion Microsoft bloquee pour {Email}: {Reason}",
                        email,
                        identityResolution.ErrorMessage);
                    TempData["Error"] = $"Connexion Microsoft impossible : {identityResolution.ErrorMessage}";
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return RedirectToAction("Login", "Account");
                }

                var utilisateur = identityResolution.Utilisateur;

                if (utilisateur == null)
                {
                    var demandeAccesExistante = await _azureAuthWorkflow.HasPendingAccessRequestAsync(email, matricule);

                    _logger.LogDebug("Utilisateur non référencé (Azure AD login attempt)");

                    TempData["AzureEmail"] = email;
                    TempData["AzureNom"] = nom ?? string.Empty;
                    TempData["AzurePrenom"] = prenom ?? string.Empty;
                    TempData["AzureMatricule"] = matricule;
                    TempData["AzureDepartment"] = azureDepartment ?? string.Empty;
                    TempData["AzureDirectionDetecteeId"] = directionDetectee.DirectionId?.ToString() ?? string.Empty;
                    TempData["AzureDirectionDetecteeNom"] = directionDetectee.DirectionLibelle ?? "Non déterminée";

                    if (demandeAccesExistante)
                    {
                        TempData["Info"] = "Une demande d'accès est déjà en attente de traitement pour votre compte.";
                    }

                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return RedirectToAction("DemanderAcces");
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, utilisateur.Id.ToString()),
                    new Claim(ClaimTypes.Name, utilisateur.Matricule),
                    new Claim(ClaimTypes.Email, utilisateur.Email),
                    new Claim("Nom", utilisateur.Nom),
                    new Claim("Prenoms", utilisateur.Prenoms)
                };

                var rolesActifs = utilisateur.UtilisateurRoles
                    .Where(r => !r.EstSupprime &&
                        (!r.DateDebut.HasValue || r.DateDebut.Value <= DateTime.UtcNow) &&
                        (!r.DateFin.HasValue || r.DateFin.Value >= DateTime.UtcNow))
                    .ToList();

                foreach (var ur in rolesActifs)
                {
                    claims.Add(new Claim(ClaimTypes.Role, ur.Role.ToString()));
                }

                if (!rolesActifs.Any())
                {
                    claims.Add(new Claim(ClaimTypes.Role, RoleUtilisateur.Demandeur.ToString()));
                }

                if (utilisateur.Direction != null)
                {
                    claims.Add(new Claim("DirectionId", utilisateur.Direction.Id.ToString()));
                    claims.Add(new Claim("DirectionNom", utilisateur.Direction.Libelle));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                };

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                await _azureAuthWorkflow.RecordSuccessfulLoginAsync(utilisateur.Id);

                _logger.LogInformation("Connexion Azure AD réussie pour {Matricule}", utilisateur.Matricule);
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du callback Azure AD");
                TempData["Error"] = "Erreur lors de la connexion. Veuillez réessayer.";
                return RedirectToAction("Login", "Account");
            }
        }

        [HttpGet]
        public IActionResult DemanderAcces()
        {
            var vm = new DemanderAccesViewModel
            {
                Email              = TempData["AzureEmail"] as string ?? string.Empty,
                Nom                = TempData["AzureNom"] as string ?? string.Empty,
                Prenom             = TempData["AzurePrenom"] as string ?? string.Empty,
                Matricule          = TempData["AzureMatricule"] as string ?? string.Empty,
                AzureDepartment    = TempData["AzureDepartment"] as string ?? string.Empty,
                DirectionDetecteeId  = TempData["AzureDirectionDetecteeId"] as string ?? string.Empty,
                DirectionDetecteeNom = TempData["AzureDirectionDetecteeNom"] as string ?? "Non déterminée"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DemanderAcces(
            string email,
            string nom,
            string prenom,
            string matricule,
            string justification,
            string? azureDepartment,
            string? directionDetecteeId)
        {
            try
            {
                var result = await _azureAuthWorkflow.SoumettreDemandeAzureAsync(
                    new SoumettreDemandeAccesAzureInput(
                        email,
                        nom,
                        prenom,
                        matricule,
                        justification,
                        azureDepartment,
                        directionDetecteeId));

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    TempData["Error"] = result.ErrorMessage;
                    return RedirectToAction("Login", "Account");
                }

                if (!string.IsNullOrWhiteSpace(result.InfoMessage))
                {
                    TempData["Info"] = result.InfoMessage;
                    return RedirectToAction("Login", "Account");
                }

                _logger.LogDebug("Demande d'accès créée (utilisateur Azure AD)");
                TempData["Success"] = result.SuccessMessage;
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la création de la demande d'accès");
                TempData["Error"] = "Erreur lors de l'envoi de la demande. Veuillez réessayer.";
                return RedirectToAction("Login", "Account");
            }
        }

        private static string? GetAzureDepartment(ClaimsPrincipal azureUser)
        {
            var claimTypes = new[]
            {
                "department",
                "Department",
                "extension_department",
                "extension_Department",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/department"
            };

            foreach (var claimType in claimTypes)
            {
                var value = azureUser.FindFirst(claimType)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

    }
}
