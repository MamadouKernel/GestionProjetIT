using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccountController(ApplicationDbContext db)
        {
            _db = db;
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
                ModelState.AddModelError(string.Empty, "Erreur : le mot de passe de l'utilisateur n'est pas configuré. Veuillez contacter l'administrateur.");
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

            // Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("Matricule", user.Matricule),
                new Claim("Nom", user.Nom ?? string.Empty),
                new Claim("Prenoms", user.Prenoms ?? string.Empty)
            };

            // Ajouter tous les rôles actifs de l'utilisateur
            var rolesActifs = user.GetRolesActifs().ToList();
            if (rolesActifs.Any())
            {
                foreach (var role in rolesActifs)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
                }
            }
            else
            {
                // Par défaut, assigner le rôle Demandeur si aucun rôle n'est défini
                claims.Add(new Claim(ClaimTypes.Role, RoleUtilisateur.Demandeur.ToString()));
            }

            if (user.DirectionId.HasValue)
            {
                claims.Add(new Claim("DirectionId", user.DirectionId.Value.ToString()));
                if (user.Direction != null)
                    claims.Add(new Claim("Libelle", user.Direction.Libelle));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

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

                // Redirection selon le workflow : vers /Projet/Index ou tableau de bord
                if (User.HasFullAccess() || 
                    User.HasRole(Roles.ChefDeProjet) || 
                    User.HasRole(Roles.DirecteurMetier))
                    return RedirectToAction("Index", "Projet");
                else if (User.HasRole(Roles.Demandeur))
                    return RedirectToAction("Index", "DemandeProjet");
                else
                    return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Erreur lors de l'authentification : {ex.Message}");
                return View(model);
            }
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout(string? reason = null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (reason == "inactivity")
            {
                TempData["Info"] = "Votre session a expiré en raison de l'inactivité. Veuillez vous reconnecter.";
            }
            
            return RedirectToAction("Login");
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

            // Mettre à jour les claims si nécessaire
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("Matricule", user.Matricule),
                new Claim("Nom", user.Nom ?? string.Empty),
                new Claim("Prenoms", user.Prenoms ?? string.Empty)
            };

            // Ajouter tous les rôles actifs de l'utilisateur
            var rolesActifs = user.GetRolesActifs().ToList();
            if (rolesActifs.Any())
            {
                foreach (var role in rolesActifs)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
                }
            }
            else
            {
                // Par défaut, assigner le rôle Demandeur si aucun rôle n'est défini
                claims.Add(new Claim(ClaimTypes.Role, RoleUtilisateur.Demandeur.ToString()));
            }

            if (user.DirectionId.HasValue)
            {
                claims.Add(new Claim("DirectionId", user.DirectionId.Value.ToString()));
                if (user.Direction != null)
                    claims.Add(new Claim("Libelle", user.Direction.Libelle));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            TempData["Success"] = "Profil mis à jour avec succès.";
            return RedirectToAction(nameof(Profil));
        }

        private static bool EstActif(Utilisateur? u)
        {
            return u != null && !u.EstSupprime;
        }
    }
}

