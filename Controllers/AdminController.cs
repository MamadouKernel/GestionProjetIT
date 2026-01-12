using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    public class DelegationsViewModel
    {
        public IEnumerable<DelegationValidationDSI> DelegationsDSI { get; set; } = new List<DelegationValidationDSI>();
        public IEnumerable<DelegationChefProjet> DelegationsChefProjet { get; set; } = new List<DelegationChefProjet>();
    }

    [Authorize(Roles = "DSI,AdminIT,ResponsableSolutionsIT")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public AdminController(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        // ============ GESTION UTILISATEURS ============

        public async Task<IActionResult> Users(int page = 1, int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .OrderBy(u => u.Nom);

            var pagedResult = await query.ToPagedResultAsync(page, pageSize);

            ViewBag.PageNumber = pagedResult.PageNumber;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.PageSize = pagedResult.PageSize;

            var users = pagedResult.Items;

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> ImportUsers()
        {
            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            ViewBag.Resultats = new List<ImportResultat>();
            ViewBag.Erreurs = new List<string>();

            return View();
        }

        [HttpGet]
        public IActionResult DownloadModeleImportUsers()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Utilisateurs");

                // En-têtes
                worksheet.Cells[1, 1].Value = "Matricule";
                worksheet.Cells[1, 2].Value = "Nom";
                worksheet.Cells[1, 3].Value = "Prénoms";
                worksheet.Cells[1, 4].Value = "Email";
                worksheet.Cells[1, 5].Value = "Code Direction";
                worksheet.Cells[1, 6].Value = "Libellé Direction";
                worksheet.Cells[1, 7].Value = "Rôles";
                worksheet.Cells[1, 8].Value = "Peut Créer Demande";

                // Style des en-têtes
                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 129, 192)); // #4481c0
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Exemples de données
                worksheet.Cells[2, 1].Value = "EMP001";
                worksheet.Cells[2, 2].Value = "DUPONT";
                worksheet.Cells[2, 3].Value = "Jean";
                worksheet.Cells[2, 4].Value = "jean.dupont@cit.ci";
                worksheet.Cells[2, 5].Value = "DSI";
                worksheet.Cells[2, 6].Value = "Direction Système d'Information";
                worksheet.Cells[2, 7].Value = "Demandeur";
                worksheet.Cells[2, 8].Value = "Oui";

                worksheet.Cells[3, 1].Value = "EMP002";
                worksheet.Cells[3, 2].Value = "MARTIN";
                worksheet.Cells[3, 3].Value = "Marie";
                worksheet.Cells[3, 4].Value = "marie.martin@cit.ci";
                worksheet.Cells[3, 5].Value = "DSI";
                worksheet.Cells[3, 6].Value = "Direction Système d'Information";
                worksheet.Cells[3, 7].Value = "ChefDeProjet,DSI";
                worksheet.Cells[3, 8].Value = "Oui";

                worksheet.Cells[4, 1].Value = "EMP003";
                worksheet.Cells[4, 2].Value = "BERNARD";
                worksheet.Cells[4, 3].Value = "Pierre";
                worksheet.Cells[4, 4].Value = "pierre.bernard@cit.ci";
                worksheet.Cells[4, 5].Value = "";
                worksheet.Cells[4, 6].Value = "";
                worksheet.Cells[4, 7].Value = "Demandeur";
                worksheet.Cells[4, 8].Value = "Non";

                // Ajuster la largeur des colonnes
                worksheet.Column(1).Width = 15; // Matricule
                worksheet.Column(2).Width = 20; // Nom
                worksheet.Column(3).Width = 20; // Prénoms
                worksheet.Column(4).Width = 30; // Email
                worksheet.Column(5).Width = 15; // Code Direction
                worksheet.Column(6).Width = 35; // Libellé Direction
                worksheet.Column(7).Width = 25; // Rôles
                worksheet.Column(8).Width = 18; // Peut Créer Demande

                var excelBytes = package.GetAsByteArray();
                var fileName = $"Modele_Import_Utilisateurs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportUsers(IFormFile fichierExcel, string motDePasseParDefaut, bool ignorerDoublons = false)
        {
            var resultats = new List<ImportResultat>();
            var erreurs = new List<string>();

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // Validation du fichier
            if (fichierExcel == null || fichierExcel.Length == 0)
            {
                erreurs.Add("Aucun fichier n'a été sélectionné.");
                ViewBag.Resultats = resultats;
                ViewBag.Erreurs = erreurs;
                return View();
            }

            // Validation du mot de passe
            if (string.IsNullOrWhiteSpace(motDePasseParDefaut) || motDePasseParDefaut.Length < 6)
            {
                erreurs.Add("Le mot de passe par défaut doit contenir au moins 6 caractères.");
                ViewBag.Resultats = resultats;
                ViewBag.Erreurs = erreurs;
                return View();
            }

            // Vérifier l'extension du fichier
            var extension = Path.GetExtension(fichierExcel.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                erreurs.Add("Le fichier doit être au format Excel (.xlsx ou .xls).");
                ViewBag.Resultats = resultats;
                ViewBag.Erreurs = erreurs;
                return View();
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var stream = new MemoryStream())
                {
                    await fichierExcel.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        if (worksheet == null)
                        {
                            erreurs.Add("Le fichier Excel ne contient aucune feuille de calcul.");
                            ViewBag.Resultats = resultats;
                            ViewBag.Erreurs = erreurs;
                            return View();
                        }

                        var rowCount = worksheet.Dimension?.Rows ?? 0;
                        if (rowCount < 2)
                        {
                            erreurs.Add("Le fichier Excel doit contenir au moins une ligne de données (en plus de l'en-tête).");
                            ViewBag.Resultats = resultats;
                            ViewBag.Erreurs = erreurs;
                            return View();
                        }

                        // Traiter chaque ligne (en commençant à la ligne 2, la ligne 1 étant l'en-tête)
                        for (int row = 2; row <= rowCount; row++)
                        {
                            var resultat = new ImportResultat { Ligne = row };

                            try
                            {
                                // Lire les valeurs de la ligne
                                var matricule = worksheet.Cells[row, 1].Text?.Trim() ?? "";
                                var nom = worksheet.Cells[row, 2].Text?.Trim() ?? "";
                                var prenoms = worksheet.Cells[row, 3].Text?.Trim() ?? "";
                                var email = worksheet.Cells[row, 4].Text?.Trim() ?? "";
                                var codeDirection = worksheet.Cells[row, 5].Text?.Trim() ?? "";
                                var libelleDirection = worksheet.Cells[row, 6].Text?.Trim() ?? "";
                                var rolesStr = worksheet.Cells[row, 7].Text?.Trim() ?? "";
                                var peutCreerDemandeStr = worksheet.Cells[row, 8].Text?.Trim() ?? "";

                                resultat.Matricule = matricule;
                                resultat.Nom = nom;

                                // Validation des champs obligatoires
                                if (string.IsNullOrWhiteSpace(matricule))
                                {
                                    resultat.Statut = "Erreur";
                                    resultat.Message = "Le matricule est requis.";
                                    resultats.Add(resultat);
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(nom))
                                {
                                    resultat.Statut = "Erreur";
                                    resultat.Message = "Le nom est requis.";
                                    resultats.Add(resultat);
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(prenoms))
                                {
                                    resultat.Statut = "Erreur";
                                    resultat.Message = "Les prénoms sont requis.";
                                    resultats.Add(resultat);
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(email))
                                {
                                    resultat.Statut = "Erreur";
                                    resultat.Message = "L'email est requis.";
                                    resultats.Add(resultat);
                                    continue;
                                }

                                // Validation de l'email
                                if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                                {
                                    resultat.Statut = "Erreur";
                                    resultat.Message = "Format d'email invalide.";
                                    resultats.Add(resultat);
                                    continue;
                                }

                                // Vérifier si le matricule ou l'email existe déjà
                                var matriculeExists = await _db.Utilisateurs
                                    .AnyAsync(u => u.Matricule == matricule && !u.EstSupprime);
                                var emailExists = await _db.Utilisateurs
                                    .AnyAsync(u => u.Email == email && !u.EstSupprime);

                                if (matriculeExists || emailExists)
                                {
                                    if (ignorerDoublons)
                                    {
                                        resultat.Statut = "Ignoré";
                                        resultat.Message = matriculeExists && emailExists
                                            ? "Matricule et email existent déjà."
                                            : matriculeExists
                                            ? "Matricule existe déjà."
                                            : "Email existe déjà.";
                                        resultats.Add(resultat);
                                        continue;
                                    }
                                    else
                                    {
                                        resultat.Statut = "Erreur";
                                        resultat.Message = matriculeExists && emailExists
                                            ? "Matricule et email existent déjà."
                                            : matriculeExists
                                            ? "Matricule existe déjà."
                                            : "Email existe déjà.";
                                        resultats.Add(resultat);
                                        continue;
                                    }
                                }

                                // Trouver ou créer la direction
                                Guid? directionId = null;
                                if (!string.IsNullOrWhiteSpace(codeDirection))
                                {
                                    var direction = await _db.Directions
                                        .FirstOrDefaultAsync(d => d.Code == codeDirection && !d.EstSupprime);
                                    if (direction != null)
                                    {
                                        directionId = direction.Id;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(libelleDirection))
                                    {
                                        // Créer une nouvelle direction si elle n'existe pas
                                        direction = new Direction
                                        {
                                            Id = Guid.NewGuid(),
                                            Code = codeDirection,
                                            Libelle = libelleDirection,
                                            EstActive = true,
                                            DateCreation = DateTime.Now,
                                            CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                            EstSupprime = false
                                        };
                                        _db.Directions.Add(direction);
                                        await _db.SaveChangesAsync();
                                        directionId = direction.Id;
                                    }
                                }

                                // Parser les rôles
                                var roles = new List<RoleUtilisateur>();
                                if (!string.IsNullOrWhiteSpace(rolesStr))
                                {
                                    var rolesArray = rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var roleStr in rolesArray)
                                    {
                                        var roleTrimmed = roleStr.Trim();
                                        if (Enum.TryParse<RoleUtilisateur>(roleTrimmed, true, out var role))
                                        {
                                            roles.Add(role);
                                        }
                                    }
                                }

                                // Si aucun rôle n'est spécifié, assigner Demandeur par défaut
                                if (!roles.Any())
                                {
                                    roles.Add(RoleUtilisateur.Demandeur);
                                }

                                // Parser "Peut Créer Demande"
                                bool peutCreerDemande = true;
                                if (!string.IsNullOrWhiteSpace(peutCreerDemandeStr))
                                {
                                    peutCreerDemande = peutCreerDemandeStr.Trim().Equals("Oui", StringComparison.OrdinalIgnoreCase) ||
                                                       peutCreerDemandeStr.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                       peutCreerDemandeStr.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                                       peutCreerDemandeStr.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                                }

                                // Créer l'utilisateur
                                var userId = Guid.NewGuid();
                                var user = new Utilisateur
                                {
                                    Id = userId,
                                    Matricule = matricule,
                                    Nom = nom,
                                    Prenoms = prenoms,
                                    Email = email,
                                    MotDePasse = BCrypt.Net.BCrypt.HashPassword(motDePasseParDefaut),
                                    DirectionId = directionId,
                                    PeutCreerDemandeProjet = peutCreerDemande,
                                    NombreConnexion = 0,
                                    DateCreation = DateTime.Now,
                                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                    EstSupprime = false,
                                    ModifiePar = null,
                                    DateModification = null
                                };

                                _db.Utilisateurs.Add(user);

                                // Assigner les rôles
                                foreach (var role in roles)
                                {
                                    var utilisateurRole = new UtilisateurRole
                                    {
                                        Id = Guid.NewGuid(),
                                        UtilisateurId = userId,
                                        Role = role,
                                        DateDebut = DateTime.Now,
                                        DateCreation = DateTime.Now,
                                        CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                        EstSupprime = false
                                    };
                                    _db.UtilisateurRoles.Add(utilisateurRole);
                                }

                                await _db.SaveChangesAsync();

                                await _auditService.LogActionAsync("CREATION_UTILISATEUR_IMPORT", "Utilisateur", user.Id,
                                    null,
                                    new { Matricule = user.Matricule, Nom = user.Nom, Prenoms = user.Prenoms, Email = user.Email, Roles = string.Join(", ", roles) });

                                resultat.Statut = "Créé";
                                resultat.Message = $"Utilisateur créé avec succès. Rôles: {string.Join(", ", roles)}";
                                resultats.Add(resultat);
                            }
                            catch (Exception ex)
                            {
                                resultat.Statut = "Erreur";
                                resultat.Message = $"Erreur lors du traitement: {ex.Message}";
                                resultats.Add(resultat);
                            }
                        }
                    }
                }

                TempData["Success"] = $"Import terminé. {resultats.Count(r => r.Statut == "Créé")} utilisateur(s) créé(s), {resultats.Count(r => r.Statut == "Ignoré")} ignoré(s), {resultats.Count(r => r.Statut == "Erreur")} erreur(s).";
            }
            catch (Exception ex)
            {
                erreurs.Add($"Erreur lors de la lecture du fichier Excel: {ex.Message}");
            }

            ViewBag.Resultats = resultats;
            ViewBag.Erreurs = erreurs;
            return View();
        }

        // API endpoint pour récupérer les données d'un utilisateur
        [HttpGet]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var user = await _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (user == null)
                return NotFound();

            var rolesActifs = user.GetRolesActifs().Select(r => (int)r).ToList();

            return Json(new
            {
                id = user.Id,
                matricule = user.Matricule,
                nom = user.Nom,
                prenoms = user.Prenoms,
                email = user.Email,
                directionId = user.DirectionId?.ToString() ?? "",
                roles = rolesActifs, // Liste des IDs de rôles
                role = rolesActifs.FirstOrDefault(), // Pour compatibilité avec l'ancien code
                peutCreerDemandeProjet = user.PeutCreerDemandeProjet
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(string Matricule, string Nom, string Prenoms, string Email, string? DirectionId, string motDePasse, string confirmMotDePasse, bool PeutCreerDemandeProjet = true)
        {
            // Validation manuelle
            if (string.IsNullOrWhiteSpace(Matricule))
            {
                ModelState.AddModelError("Matricule", "Le matricule est requis.");
            }

            if (string.IsNullOrWhiteSpace(Nom))
            {
                ModelState.AddModelError("Nom", "Le nom est requis.");
            }

            if (string.IsNullOrWhiteSpace(Prenoms))
            {
                ModelState.AddModelError("Prenoms", "Les prénoms sont requis.");
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ModelState.AddModelError("Email", "L'email est requis.");
            }

            if (string.IsNullOrWhiteSpace(motDePasse))
            {
                ModelState.AddModelError("motDePasse", "Le mot de passe est requis.");
            }
            else if (motDePasse.Length < 6)
            {
                ModelState.AddModelError("motDePasse", "Le mot de passe doit contenir au moins 6 caractères.");
            }

            if (string.IsNullOrWhiteSpace(confirmMotDePasse))
            {
                ModelState.AddModelError("confirmMotDePasse", "La confirmation du mot de passe est requise.");
            }

            if (!string.IsNullOrWhiteSpace(motDePasse) && !string.IsNullOrWhiteSpace(confirmMotDePasse) && motDePasse != confirmMotDePasse)
            {
                ModelState.AddModelError("confirmMotDePasse", "Les mots de passe ne correspondent pas.");
            }

            // Vérifier si le matricule existe déjà
            if (!string.IsNullOrWhiteSpace(Matricule))
            {
                var matriculeExists = await _db.Utilisateurs
                    .AnyAsync(u => u.Matricule == Matricule && !u.EstSupprime);
                if (matriculeExists)
                {
                    ModelState.AddModelError("Matricule", "Ce matricule existe déjà.");
                }
            }

            // Vérifier si l'email existe déjà
            if (!string.IsNullOrWhiteSpace(Email))
            {
                var emailExists = await _db.Utilisateurs
                    .AnyAsync(u => u.Email == Email && !u.EstSupprime);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Cet email existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                var userId = Guid.NewGuid();
                var user = new Utilisateur
                {
                    Id = userId,
                    Matricule = Matricule.Trim(),
                    Nom = Nom.Trim(),
                    Prenoms = Prenoms.Trim(),
                    Email = Email.Trim(),
                    MotDePasse = BCrypt.Net.BCrypt.HashPassword(motDePasse),
                    NombreConnexion = 0,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null,
                    DateModification = null
                };

                // Gérer DirectionId (peut être vide)
                if (!string.IsNullOrWhiteSpace(DirectionId) && Guid.TryParse(DirectionId, out var directionGuid))
                {
                    user.DirectionId = directionGuid;
                }
                else
                {
                    user.DirectionId = null;
                }

                user.PeutCreerDemandeProjet = PeutCreerDemandeProjet;

                _db.Utilisateurs.Add(user);

                // Assigner automatiquement le rôle Demandeur par défaut
                var utilisateurRole = new UtilisateurRole
                {
                    Id = Guid.NewGuid(),
                    UtilisateurId = userId,
                    Role = RoleUtilisateur.Demandeur,
                    DateDebut = DateTime.Now,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };
                _db.UtilisateurRoles.Add(utilisateurRole);

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_UTILISATEUR", "Utilisateur", user.Id,
                    null,
                    new { Matricule = user.Matricule, Nom = user.Nom, Prenoms = user.Prenoms, Email = user.Email, RoleParDefaut = "Demandeur" });

                TempData["Success"] = $"Utilisateur créé avec succès. Le rôle 'Demandeur' a été assigné par défaut. Vous pouvez gérer les rôles depuis la liste des utilisateurs.";
                return RedirectToAction(nameof(Users));
            }

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View("Users", await _db.Utilisateurs.Include(u => u.Direction).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(Guid id, string Matricule, string Nom, string Prenoms, string Email, string? DirectionId, string? nouveauMotDePasse, string? confirmNouveauMotDePasse, bool PeutCreerDemandeProjet = true)
        {
            var existingUser = await _db.Utilisateurs
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (existingUser == null)
                return NotFound();

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(Matricule))
            {
                ModelState.AddModelError("Matricule", "Le matricule est requis.");
            }

            if (string.IsNullOrWhiteSpace(Nom))
            {
                ModelState.AddModelError("Nom", "Le nom est requis.");
            }

            if (string.IsNullOrWhiteSpace(Prenoms))
            {
                ModelState.AddModelError("Prenoms", "Les prénoms sont requis.");
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ModelState.AddModelError("Email", "L'email est requis.");
            }

            // Vérifier si le matricule existe déjà (sauf pour l'utilisateur actuel)
            if (!string.IsNullOrWhiteSpace(Matricule) && Matricule != existingUser.Matricule)
            {
                var matriculeExists = await _db.Utilisateurs
                    .AnyAsync(u => u.Matricule == Matricule && u.Id != id && !u.EstSupprime);
                if (matriculeExists)
                {
                    ModelState.AddModelError("Matricule", "Ce matricule existe déjà.");
                }
            }

            // Vérifier si l'email existe déjà (sauf pour l'utilisateur actuel)
            if (!string.IsNullOrWhiteSpace(Email) && Email != existingUser.Email)
            {
                var emailExists = await _db.Utilisateurs
                    .AnyAsync(u => u.Email == Email && u.Id != id && !u.EstSupprime);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Cet email existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                existingUser.Matricule = Matricule.Trim();
                existingUser.Nom = Nom.Trim();
                existingUser.Prenoms = Prenoms.Trim();
                existingUser.Email = Email.Trim();

                // Gérer DirectionId
                if (!string.IsNullOrWhiteSpace(DirectionId) && Guid.TryParse(DirectionId, out var directionGuid))
                {
                    existingUser.DirectionId = directionGuid;
                }
                else
                {
                    existingUser.DirectionId = null;
                }

                if (!string.IsNullOrEmpty(nouveauMotDePasse))
                {
                    // Valider la confirmation du mot de passe
                    if (string.IsNullOrWhiteSpace(confirmNouveauMotDePasse))
                    {
                        ModelState.AddModelError("confirmNouveauMotDePasse", "La confirmation du mot de passe est requise pour modifier le mot de passe.");
                    }
                    else if (nouveauMotDePasse != confirmNouveauMotDePasse)
                    {
                        ModelState.AddModelError("confirmNouveauMotDePasse", "Les mots de passe ne correspondent pas.");
                    }
                    else if (nouveauMotDePasse.Length < 6)
                    {
                        ModelState.AddModelError("nouveauMotDePasse", "Le mot de passe doit contenir au moins 6 caractères.");
                    }
                    else
                    {
                        existingUser.MotDePasse = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);
                    }
                }

                existingUser.PeutCreerDemandeProjet = PeutCreerDemandeProjet;
                existingUser.DateModification = DateTime.Now;
                existingUser.ModifiePar = _currentUserService.Matricule;

                // Note: La gestion des rôles se fait maintenant via l'action UpdateRoles (vue dédiée GererRoles)
                // On ne modifie plus les rôles dans cette action

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_UTILISATEUR", "Utilisateur", existingUser.Id,
                    new { AncienMatricule = existingUser.Matricule },
                    new { NouveauMatricule = Matricule, MotDePasseModifie = !string.IsNullOrEmpty(nouveauMotDePasse) });

                TempData["Success"] = $"Utilisateur modifié avec succès. Pour gérer les rôles, utilisez le bouton 'Gérer les rôles' depuis la liste.";
                return RedirectToAction(nameof(Users));
            }

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View("Users", await _db.Utilisateurs.Include(u => u.Direction).Include(u => u.UtilisateurRoles).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var user = await _db.Utilisateurs.FindAsync(id);
            if (user == null)
                return NotFound();

            user.EstSupprime = true;
            user.DateModification = DateTime.Now;
            user.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("DESACTIVATION_UTILISATEUR", "Utilisateur", user.Id,
                new { Matricule = user.Matricule, Nom = user.Nom, Prenoms = user.Prenoms });

            TempData["Success"] = "Utilisateur désactivé.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Guid id, string nouveauMotDePasse)
        {
            var user = await _db.Utilisateurs.FindAsync(id);
            if (user == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(nouveauMotDePasse))
            {
                TempData["Error"] = "Le mot de passe est requis.";
                return RedirectToAction(nameof(Users));
            }

            user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);
            user.DateModification = DateTime.Now;
            user.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REINITIALISATION_MOT_DE_PASSE", "Utilisateur", user.Id,
                new { Matricule = user.Matricule });

            TempData["Success"] = "Mot de passe réinitialisé avec succès.";
            return RedirectToAction(nameof(Users));
        }

        // ============ GESTION RÔLES ============

        public async Task<IActionResult> GererRoles(Guid id)
        {
            var user = await _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (user == null)
                return NotFound();

            // Récupérer tous les rôles disponibles
            ViewBag.AllRoles = Enum.GetValues(typeof(RoleUtilisateur))
                .Cast<RoleUtilisateur>()
                .ToList();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(Guid id, string Roles)
        {
            var user = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (user == null)
                return NotFound();

            // Parser les rôles sélectionnés
            var rolesSelectionnes = new List<int>();
            if (!string.IsNullOrWhiteSpace(Roles))
            {
                rolesSelectionnes = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => int.TryParse(r.Trim(), out var roleId) ? roleId : 0)
                    .Where(r => r > 0)
                    .ToList();
            }

            // Valider qu'au moins un rôle est sélectionné
            if (!rolesSelectionnes.Any())
            {
                TempData["Error"] = "Veuillez sélectionner au moins un rôle.";
                return RedirectToAction(nameof(GererRoles), new { id });
            }

            // Convertir en enum
            var rolesEnum = rolesSelectionnes
                .Select(r => (RoleUtilisateur)r)
                .Where(r => Enum.IsDefined(typeof(RoleUtilisateur), r))
                .ToList();

            if (!rolesEnum.Any())
            {
                TempData["Error"] = "Aucun rôle valide sélectionné.";
                return RedirectToAction(nameof(GererRoles), new { id });
            }

            // Récupérer les rôles actuellement actifs pour le log d'audit
            var rolesActuels = user.GetRolesActifs().ToList();

            // Traiter chaque rôle sélectionné
            foreach (var roleSelectionne in rolesEnum)
            {
                // D'abord, vérifier dans les entités déjà trackées (chargées avec Include)
                var roleTracke = user.UtilisateurRoles
                    .FirstOrDefault(ur => ur.Role == roleSelectionne);

                if (roleTracke != null)
                {
                    // Le rôle existe déjà dans les entités trackées, s'assurer qu'il est actif
                    if (roleTracke.EstSupprime)
                    {
                        // Réactiver le rôle existant
                        roleTracke.EstSupprime = false;
                        roleTracke.DateDebut = DateTime.Now;
                        roleTracke.DateFin = null;
                        roleTracke.DateModification = DateTime.Now;
                        roleTracke.ModifiePar = _currentUserService.Matricule;
                    }
                    // Si le rôle est déjà actif, ne rien faire
                }
                else
                {
                    // Le rôle n'est pas dans les entités trackées, vérifier dans le ChangeTracker
                    var roleDansChangeTracker = _db.ChangeTracker.Entries<UtilisateurRole>()
                        .FirstOrDefault(e => e.Entity.UtilisateurId == user.Id && 
                                            e.Entity.Role == roleSelectionne &&
                                            e.State != Microsoft.EntityFrameworkCore.EntityState.Deleted);

                    if (roleDansChangeTracker != null)
                    {
                        // L'entité est trackée mais pas dans la collection, la récupérer
                        var roleATraiter = roleDansChangeTracker.Entity;
                        if (roleATraiter.EstSupprime)
                        {
                            roleATraiter.EstSupprime = false;
                            roleATraiter.DateDebut = DateTime.Now;
                            roleATraiter.DateFin = null;
                            roleATraiter.DateModification = DateTime.Now;
                            roleATraiter.ModifiePar = _currentUserService.Matricule;
                        }
                    }
                    else
                    {
                        // Vérifier une dernière fois dans la base de données
                        var roleExistant = await _db.UtilisateurRoles
                            .AsNoTracking()
                            .FirstOrDefaultAsync(ur => ur.UtilisateurId == user.Id && ur.Role == roleSelectionne);

                        if (roleExistant != null)
                        {
                            // Le rôle existe en base mais n'est pas tracké, l'attacher
                            var roleATraiter = await _db.UtilisateurRoles
                                .FirstOrDefaultAsync(ur => ur.UtilisateurId == user.Id && ur.Role == roleSelectionne);

                            if (roleATraiter != null)
                            {
                                if (roleATraiter.EstSupprime)
                                {
                                    roleATraiter.EstSupprime = false;
                                    roleATraiter.DateDebut = DateTime.Now;
                                    roleATraiter.DateFin = null;
                                    roleATraiter.DateModification = DateTime.Now;
                                    roleATraiter.ModifiePar = _currentUserService.Matricule;
                                }
                            }
                        }
                        else
                        {
                            // Le rôle n'existe vraiment pas, le créer
                            var nouveauRole = new UtilisateurRole
                            {
                                Id = Guid.NewGuid(),
                                UtilisateurId = user.Id,
                                Role = roleSelectionne,
                                DateDebut = DateTime.Now,
                                DateFin = null,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                EstSupprime = false
                            };
                            _db.UtilisateurRoles.Add(nouveauRole);
                        }
                    }
                }
            }

            // Désactiver les rôles qui ne sont plus sélectionnés
            // Utiliser les entités déjà trackées
            foreach (var roleUtilisateur in user.UtilisateurRoles)
            {
                if (!rolesEnum.Contains(roleUtilisateur.Role) && !roleUtilisateur.EstSupprime)
                {
                    // Désactiver le rôle qui n'est plus sélectionné
                    roleUtilisateur.EstSupprime = true;
                    roleUtilisateur.DateModification = DateTime.Now;
                    roleUtilisateur.ModifiePar = _currentUserService.Matricule;
                }
            }

            await _db.SaveChangesAsync();

            // Log d'audit
            await _auditService.LogActionAsync("MODIFICATION_ROLES_UTILISATEUR", "Utilisateur", user.Id,
                new { AnciensRoles = string.Join(", ", rolesActuels) },
                new { NouveauxRoles = string.Join(", ", rolesEnum) });

            TempData["Success"] = "Rôles mis à jour avec succès.";
            return RedirectToAction(nameof(Users));
        }

        // ============ GESTION DIRECTIONS ============

        public async Task<IActionResult> Directions()
        {
            var directions = await _db.Directions
                .Include(d => d.DSI)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // Charger tous les utilisateurs actifs pour la sélection du responsable
            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View(directions);
        }

        // Fonction helper pour générer le code à partir du libellé
        private string GenerateCodeFromLibelle(string libelle)
        {
            if (string.IsNullOrWhiteSpace(libelle))
                return string.Empty;

            // Exception spéciale : Direction d'exploitation -> DEX
            var libelleNormalise = libelle.Trim().ToLowerInvariant()
                .Replace("'", " ")
                .Replace("-", " ")
                .Replace("  ", " ")
                .Trim();

            if (libelleNormalise.Contains("direction") && libelleNormalise.Contains("exploitation"))
            {
                return "DEX";
            }

            // Liste des mots à ignorer (articles, prépositions, conjonctions)
            var motsIgnorer = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "de", "des", "du", "d'", "le", "la", "les", "un", "une",
                "et", "ou", "à", "au", "aux", "en", "pour", "par", "avec",
                "sans", "sous", "sur", "dans", "entre", "vers"
            };

            // Nettoyer le libellé : remplacer les apostrophes et tirets par des espaces
            var texteNettoye = libelle
                .Replace("'", " ")
                .Replace("-", " ")
                .Replace("  ", " ")
                .Trim();

            // Diviser en mots
            var mots = texteNettoye.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Générer le code en prenant la première lettre de chaque mot significatif
            var code = new System.Text.StringBuilder();
            foreach (var mot in mots)
            {
                var motNettoye = mot.Trim();
                // Ignorer les mots trop courts (< 2 lettres) ou dans la liste d'exclusion
                if (motNettoye.Length >= 2 && !motsIgnorer.Contains(motNettoye))
                {
                    code.Append(char.ToUpperInvariant(motNettoye[0]));
                }
            }

            return code.ToString();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDirection(string Code, string Libelle, string? DSIId)
        {
            // Gérer le checkbox EstActive (peut venir comme "true" ou "false" string, ou plusieurs valeurs)
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                // Si plusieurs valeurs (checkbox + hidden), prendre la première qui n'est pas "false"
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            // Valider les champs requis
            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            // Générer automatiquement le code si vide ou si seulement le libellé est fourni
            string codeFinal = Code?.Trim();
            if (string.IsNullOrWhiteSpace(codeFinal) && !string.IsNullOrWhiteSpace(Libelle))
            {
                codeFinal = GenerateCodeFromLibelle(Libelle);
            }

            if (string.IsNullOrWhiteSpace(codeFinal))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            // Vérifier si le code existe déjà
            if (!string.IsNullOrWhiteSpace(codeFinal))
            {
                var codeExists = await _db.Directions
                    .AnyAsync(d => d.Code == codeFinal && !d.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de direction existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                // Créer une nouvelle direction manuellement pour éviter les problèmes de binding
                var direction = new Direction
                {
                    Id = Guid.NewGuid(),
                    Code = codeFinal,
                    Libelle = Libelle.Trim(),
                    EstActive = estActive,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null, // Explicitement null pour les nouvelles entités
                    DateModification = null
                };

                // Gérer DSIId (peut être vide)
                if (!string.IsNullOrWhiteSpace(DSIId) && Guid.TryParse(DSIId, out var dsiGuid))
                {
                    direction.DSIId = dsiGuid;
                }

                _db.Directions.Add(direction);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_DIRECTION", "Direction", direction.Id,
                    null,
                    new { Code = direction.Code, Libelle = direction.Libelle });

                TempData["Success"] = "Direction créée avec succès.";
                return RedirectToAction(nameof(Directions));
            }

            // En cas d'erreur, retourner à la vue avec les erreurs
            var directions = await _db.Directions
                .Include(d => d.DSI)
                .Where(d => !d.EstSupprime)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // Charger tous les utilisateurs actifs pour la sélection du responsable
            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();
            
            return View("Directions", directions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDirection(Guid id, string Code, string Libelle, string? DSIId)
        {
            var existingDirection = await _db.Directions.FindAsync(id);
            if (existingDirection == null)
                return NotFound();

            // Gérer le checkbox EstActive
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            // Valider les champs requis
            if (string.IsNullOrWhiteSpace(Code))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            // Vérifier si le code existe déjà (sauf pour la direction actuelle)
            if (!string.IsNullOrWhiteSpace(Code) && Code != existingDirection.Code)
            {
                var codeExists = await _db.Directions
                    .AnyAsync(d => d.Code == Code && d.Id != id && !d.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de direction existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                existingDirection.Code = Code.Trim();
                existingDirection.Libelle = Libelle.Trim();
                existingDirection.EstActive = estActive;
                
                // Gérer DSIId
                if (!string.IsNullOrWhiteSpace(DSIId) && Guid.TryParse(DSIId, out var dsiGuid))
                {
                    existingDirection.DSIId = dsiGuid;
                }
                else
                {
                    existingDirection.DSIId = null;
                }
                
                existingDirection.DateModification = DateTime.Now;
                existingDirection.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_DIRECTION", "Direction", existingDirection.Id,
                    new { AncienCode = existingDirection.Code, AncienLibelle = existingDirection.Libelle },
                    new { NouveauCode = Code, NouveauLibelle = Libelle });

                TempData["Success"] = "Direction modifiée avec succès.";
                return RedirectToAction(nameof(Directions));
            }

            var directions = await _db.Directions
                .Include(d => d.DSI)
                .Where(d => !d.EstSupprime)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // Charger tous les utilisateurs actifs pour la sélection du responsable
            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View("Directions", directions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDirection(Guid id)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            direction.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_DIRECTION", "Direction", direction.Id);

            TempData["Success"] = "Direction supprimée.";
            return RedirectToAction(nameof(Directions));
        }

        // ============ GESTION SERVICES ============

        public async Task<IActionResult> Services()
        {
            var services = await _db.Services
                .Include(s => s.Direction)
                .OrderBy(s => s.Libelle)
                .ToListAsync();

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View(services);
        }

        // Fonction helper pour générer le code de service (Direction + Libellé)
        private async Task<string> GenerateServiceCodeAsync(string libelle, Guid? directionId)
        {
            if (string.IsNullOrWhiteSpace(libelle) || !directionId.HasValue)
                return string.Empty;

            // Récupérer le code de la direction
            var direction = await _db.Directions.FindAsync(directionId.Value);
            if (direction == null || string.IsNullOrWhiteSpace(direction.Code))
                return string.Empty;

            // Générer les initiales du libellé du service
            var initialesService = GenerateCodeFromLibelle(libelle);

            // Combiner : CODE_DIRECTION-INITIALES_SERVICE
            return $"{direction.Code}-{initialesService}";
        }

        // Endpoint API pour récupérer le code d'une direction
        [HttpGet]
        public async Task<IActionResult> GetDirectionCode(Guid id)
        {
            var direction = await _db.Directions.FindAsync(id);
            if (direction == null)
                return NotFound();

            return Json(new { code = direction.Code ?? "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(string Code, string Libelle, string DirectionId)
        {
            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            if (string.IsNullOrWhiteSpace(DirectionId) || !Guid.TryParse(DirectionId, out var directionGuid))
            {
                ModelState.AddModelError("DirectionId", "La direction est requise.");
            }

            // Générer automatiquement le code si vide
            string codeFinal = Code?.Trim();
            if (string.IsNullOrWhiteSpace(codeFinal) && !string.IsNullOrWhiteSpace(Libelle) && Guid.TryParse(DirectionId, out directionGuid))
            {
                codeFinal = await GenerateServiceCodeAsync(Libelle, directionGuid);
            }

            if (string.IsNullOrWhiteSpace(codeFinal))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            // Vérifier si le code existe déjà
            if (!string.IsNullOrWhiteSpace(codeFinal))
            {
                var codeExists = await _db.Services
                    .AnyAsync(s => s.Code == codeFinal && !s.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de service existe déjà.");
                }
            }

            if (ModelState.IsValid && Guid.TryParse(DirectionId, out directionGuid))
            {
                var service = new Service
                {
                    Id = Guid.NewGuid(),
                    Code = codeFinal,
                    Libelle = Libelle.Trim(),
                    DirectionId = directionGuid,
                    EstActive = true,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };

                _db.Services.Add(service);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_SERVICE", "Service", service.Id,
                    null,
                    new { Code = service.Code, Libelle = service.Libelle, DirectionId = directionGuid });

                TempData["Success"] = "Service créé avec succès.";
                return RedirectToAction(nameof(Services));
            }

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View("Services", await _db.Services.Include(s => s.Direction).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateService(Guid id, string Code, string Libelle, string DirectionId)
        {
            var existingService = await _db.Services.FindAsync(id);
            if (existingService == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(Code))
            {
                ModelState.AddModelError("Code", "Le code est requis.");
            }

            if (string.IsNullOrWhiteSpace(Libelle))
            {
                ModelState.AddModelError("Libelle", "Le libellé est requis.");
            }

            if (string.IsNullOrWhiteSpace(DirectionId) || !Guid.TryParse(DirectionId, out var directionGuid))
            {
                ModelState.AddModelError("DirectionId", "La direction est requise.");
            }

            if (!string.IsNullOrWhiteSpace(Code) && Code != existingService.Code)
            {
                var codeExists = await _db.Services
                    .AnyAsync(s => s.Code == Code && s.Id != id && !s.EstSupprime);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Ce code de service existe déjà.");
                }
            }

            if (ModelState.IsValid && Guid.TryParse(DirectionId, out directionGuid))
            {
                existingService.Code = Code.Trim();
                existingService.Libelle = Libelle.Trim();
                existingService.DirectionId = directionGuid;
                existingService.DateModification = DateTime.Now;
                existingService.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_SERVICE", "Service", existingService.Id,
                    new { AncienCode = existingService.Code, AncienLibelle = existingService.Libelle },
                    new { NouveauCode = Code, NouveauLibelle = Libelle });

                TempData["Success"] = "Service modifié avec succès.";
                return RedirectToAction(nameof(Services));
            }

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View("Services", await _db.Services.Include(s => s.Direction).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(Guid id)
        {
            var service = await _db.Services.FindAsync(id);
            if (service == null)
                return NotFound();

            service.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_SERVICE", "Service", service.Id);

            TempData["Success"] = "Service supprimé.";
            return RedirectToAction(nameof(Services));
        }

        // ============ GESTION PARAMÈTRES ============

        public async Task<IActionResult> Parametres()
        {
            var parametres = await _db.ParametresSysteme
                .Where(p => !p.EstSupprime)
                .OrderBy(p => p.Cle)
                .ToListAsync();

            return View(parametres);
        }

        // API endpoint pour récupérer les données d'un paramètre
        [HttpGet]
        public async Task<IActionResult> GetParametre(Guid id)
        {
            var parametre = await _db.ParametresSysteme
                .FirstOrDefaultAsync(p => p.Id == id && !p.EstSupprime);

            if (parametre == null)
                return NotFound();

            return Json(new
            {
                id = parametre.Id,
                cle = parametre.Cle,
                valeur = parametre.Valeur,
                description = parametre.Description ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateParametre(string Cle, string Valeur, string Description)
        {
            // Validation manuelle
            if (string.IsNullOrWhiteSpace(Cle))
            {
                ModelState.AddModelError("Cle", "La clé est requise.");
            }

            if (string.IsNullOrWhiteSpace(Valeur))
            {
                ModelState.AddModelError("Valeur", "La valeur est requise.");
            }

            // Vérifier si la clé existe déjà
            if (!string.IsNullOrWhiteSpace(Cle))
            {
                var cleExists = await _db.ParametresSysteme
                    .AnyAsync(p => p.Cle == Cle && !p.EstSupprime);
                if (cleExists)
                {
                    ModelState.AddModelError("Cle", "Cette clé existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                var parametre = new ParametreSysteme
                {
                    Id = Guid.NewGuid(),
                    Cle = Cle.Trim(),
                    Valeur = Valeur?.Trim() ?? string.Empty,
                    Description = Description?.Trim() ?? string.Empty,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null,
                    DateModification = null
                };

                _db.ParametresSysteme.Add(parametre);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_PARAMETRE", "ParametreSysteme", parametre.Id);

                TempData["Success"] = "Paramètre créé avec succès.";
                return RedirectToAction(nameof(Parametres));
            }

            return View("Parametres", await _db.ParametresSysteme.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateParametre(Guid id, string Cle, string Valeur, string Description)
        {
            var existingParametre = await _db.ParametresSysteme.FindAsync(id);
            if (existingParametre == null)
                return NotFound();

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(Cle))
            {
                ModelState.AddModelError("Cle", "La clé est requise.");
            }

            if (string.IsNullOrWhiteSpace(Valeur))
            {
                ModelState.AddModelError("Valeur", "La valeur est requise.");
            }

            // Vérifier si la clé existe déjà (sauf pour le paramètre actuel)
            if (!string.IsNullOrWhiteSpace(Cle) && Cle != existingParametre.Cle)
            {
                var cleExists = await _db.ParametresSysteme
                    .AnyAsync(p => p.Cle == Cle && p.Id != id && !p.EstSupprime);
                if (cleExists)
                {
                    ModelState.AddModelError("Cle", "Cette clé existe déjà.");
                }
            }

            if (ModelState.IsValid)
            {
                existingParametre.Cle = Cle.Trim();
                existingParametre.Valeur = Valeur?.Trim() ?? string.Empty;
                existingParametre.Description = Description?.Trim() ?? string.Empty;
                existingParametre.DateModification = DateTime.Now;
                existingParametre.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_PARAMETRE", "ParametreSysteme", existingParametre.Id,
                    new { AncienneCle = existingParametre.Cle, AncienneValeur = existingParametre.Valeur },
                    new { NouvelleCle = Cle, NouvelleValeur = Valeur });

                TempData["Success"] = "Paramètre modifié avec succès.";
                return RedirectToAction(nameof(Parametres));
            }

            return View("Parametres", await _db.ParametresSysteme.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteParametre(Guid id)
        {
            var parametre = await _db.ParametresSysteme.FindAsync(id);
            if (parametre == null)
                return NotFound();

            parametre.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_PARAMETRE", "ParametreSysteme", parametre.Id);

            TempData["Success"] = "Paramètre supprimé.";
            return RedirectToAction(nameof(Parametres));
        }

        // ============ GESTION DÉLÉGATIONS (UNIFIÉE) ============

        public async Task<IActionResult> Delegations(string? tab = "dsi")
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Délégations DSI
            var delegationsDSI = await _db.DelegationsValidationDSI
                .Include(d => d.DSI)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime)
                .OrderByDescending(d => d.DateDebut)
                .ToListAsync();

            // Charger tous les utilisateurs actifs pour la sélection du responsable
            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            ViewBag.DeleguesDSI = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ResponsableSolutionsIT))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            // Délégations ChefProjet
            IQueryable<DelegationChefProjet> queryChefProjet = _db.DelegationsChefProjet
                .Include(d => d.Delegant)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime);

            // Si l'utilisateur est DSI ou ResponsableSolutionsIT, voir uniquement ses propres délégations
            if (userRole == "DSI" || userRole == "ResponsableSolutionsIT")
            {
                queryChefProjet = queryChefProjet.Where(d => d.DelegantId == userId);
            }
            // AdminIT voit toutes les délégations

            var delegationsChefProjet = await queryChefProjet
                .Include(d => d.Projet)
                .OrderByDescending(d => d.DateDebut)
                .ToListAsync();

            // Liste des projets actifs (non clôturés)
            ViewBag.Projets = await _db.Projets
                .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
                .OrderByDescending(p => p.DateCreation)
                .ToListAsync();

            // Liste des utilisateurs pouvant déléguer (DSI et ResponsableSolutionsIT)
            ViewBag.Delegants = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && 
                           (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            // Liste de tous les utilisateurs pouvant recevoir la délégation (tous sauf Demandeur)
            ViewBag.DeleguesChefProjet = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.CurrentUserId = userId;
            ViewBag.CurrentUserRole = userRole;
            ViewBag.ActiveTab = tab ?? "dsi";

            // Créer un modèle de vue pour les deux types
            var viewModel = new DelegationsViewModel
            {
                DelegationsDSI = delegationsDSI,
                DelegationsChefProjet = delegationsChefProjet
            };

            return View(viewModel);
        }

        // API endpoint pour récupérer les données d'une délégation
        [HttpGet]
        public async Task<IActionResult> GetDelegation(Guid id)
        {
            var delegation = await _db.DelegationsValidationDSI
                .FirstOrDefaultAsync(d => d.Id == id && !d.EstSupprime);

            if (delegation == null)
                return NotFound();

            return Json(new
            {
                id = delegation.Id,
                dsiId = delegation.DSIId.ToString(),
                delegueId = delegation.DelegueId.ToString(),
                dateDebut = delegation.DateDebut.ToString("yyyy-MM-ddTHH:mm:ss"),
                dateFin = delegation.DateFin.ToString("yyyy-MM-ddTHH:mm:ss"),
                estActive = delegation.EstActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelegation(string DSIId, string DelegueId, DateTime DateDebut, DateTime DateFin)
        {
            // Validation manuelle
            if (string.IsNullOrWhiteSpace(DSIId) || !Guid.TryParse(DSIId, out var dsiGuid))
            {
                ModelState.AddModelError("DSIId", "Le DSI est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            if (DateDebut >= DateFin)
            {
                ModelState.AddModelError("DateFin", "La date de fin doit être postérieure à la date de début.");
            }

            // Gérer EstActive (checkbox)
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(DSIId, out dsiGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                var delegation = new DelegationValidationDSI
                {
                    Id = Guid.NewGuid(),
                    DSIId = dsiGuid,
                    DelegueId = delegueGuid,
                    DateDebut = DateDebut,
                    DateFin = DateFin,
                    EstActive = estActive,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false,
                    ModifiePar = null,
                    DateModification = null
                };

                _db.DelegationsValidationDSI.Add(delegation);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("CREATION_DELEGATION_DSI", "DelegationValidationDSI", delegation.Id,
                    null,
                    new { DSIId = delegation.DSIId, DelegueId = delegation.DelegueId, DateDebut = delegation.DateDebut, DateFin = delegation.DateFin });

                TempData["Success"] = "Délégation DSI créée avec succès.";
                return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
            }

            // Recharger les données pour la vue unifiée
            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDelegation(Guid id, string DSIId, string DelegueId, DateTime DateDebut, DateTime DateFin)
        {
            var existingDelegation = await _db.DelegationsValidationDSI.FindAsync(id);
            if (existingDelegation == null)
                return NotFound();

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(DSIId) || !Guid.TryParse(DSIId, out var dsiGuid))
            {
                ModelState.AddModelError("DSIId", "Le DSI est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            if (DateDebut >= DateFin)
            {
                ModelState.AddModelError("DateFin", "La date de fin doit être postérieure à la date de début.");
            }

            // Gérer EstActive (checkbox)
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(DSIId, out dsiGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                existingDelegation.DSIId = dsiGuid;
                existingDelegation.DelegueId = delegueGuid;
                existingDelegation.DateDebut = DateDebut;
                existingDelegation.DateFin = DateFin;
                existingDelegation.EstActive = estActive;
                existingDelegation.DateModification = DateTime.Now;
                existingDelegation.ModifiePar = _currentUserService.Matricule;

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("MODIFICATION_DELEGATION", "DelegationValidationDSI", existingDelegation.Id);

                TempData["Success"] = "Délégation DSI modifiée avec succès.";
                return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
            }

            // Recharger les données pour la vue unifiée
            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelegation(Guid id)
        {
            var delegation = await _db.DelegationsValidationDSI.FindAsync(id);
            if (delegation == null)
                return NotFound();

            delegation.EstSupprime = true;
            delegation.EstActive = false;
            delegation.DateModification = DateTime.Now;
            delegation.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CLOTURE_DELEGATION_DSI", "DelegationValidationDSI", delegation.Id,
                new { DSIId = delegation.DSIId, DelegueId = delegation.DelegueId, DateDebut = delegation.DateDebut, DateFin = delegation.DateFin });

            TempData["Success"] = "Délégation DSI clôturée.";
            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        // ============ GESTION DÉLÉGATIONS CHEF PROJET ============

        public async Task<IActionResult> DelegationsChefProjet()
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            IQueryable<DelegationChefProjet> query = _db.DelegationsChefProjet
                .Include(d => d.Delegant)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime);

            // Si l'utilisateur est DSI ou ResponsableSolutionsIT, voir uniquement ses propres délégations
            if (userRole == "DSI" || userRole == "ResponsableSolutionsIT")
            {
                query = query.Where(d => d.DelegantId == userId);
            }
            // AdminIT voit toutes les délégations
            else if (userRole != "AdminIT")
            {
                return Forbid();
            }

            var delegations = await query
                .Include(d => d.Projet)
                .OrderByDescending(d => d.DateDebut)
                .ToListAsync();

            // Liste des projets actifs (non clôturés)
            ViewBag.Projets = await _db.Projets
                .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
                .OrderByDescending(p => p.DateCreation)
                .ToListAsync();

            // Liste des utilisateurs pouvant déléguer (DSI et ResponsableSolutionsIT)
            ViewBag.Delegants = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && (u.Role == RoleUtilisateur.DSI || u.Role == RoleUtilisateur.ResponsableSolutionsIT))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            // Liste de tous les utilisateurs pouvant recevoir la délégation (tous sauf Demandeur)
            ViewBag.Delegues = await _db.Utilisateurs
                .Where(u => !u.EstSupprime && u.Role != RoleUtilisateur.Demandeur)
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.CurrentUserId = userId;
            ViewBag.CurrentUserRole = userRole;

            return View(delegations);
        }

        // API endpoint pour récupérer les données d'une délégation ChefProjet
        [HttpGet]
        public async Task<IActionResult> GetDelegationChefProjet(Guid id)
        {
            var delegation = await _db.DelegationsChefProjet
                .FirstOrDefaultAsync(d => d.Id == id && !d.EstSupprime);

            if (delegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Vérifier les permissions
            if (userRole != "AdminIT" && delegation.DelegantId != userId)
                return Forbid();

            return Json(new
            {
                id = delegation.Id,
                projetId = delegation.ProjetId.ToString(),
                delegantId = delegation.DelegantId.ToString(),
                delegueId = delegation.DelegueId.ToString(),
                dateDebut = delegation.DateDebut.ToString("yyyy-MM-ddTHH:mm:ss"),
                dateFin = delegation.DateFin?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "",
                estActive = delegation.EstActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelegationChefProjet(string ProjetId, string DelegantId, string DelegueId, DateTime DateDebut)
        {
            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(ProjetId) || !Guid.TryParse(ProjetId, out var projetGuid))
            {
                ModelState.AddModelError("ProjetId", "Le projet est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegantId) || !Guid.TryParse(DelegantId, out var delegantGuid))
            {
                ModelState.AddModelError("DelegantId", "Le délégant est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            // Vérifier que le projet existe et n'est pas clôturé
            if (Guid.TryParse(ProjetId, out projetGuid))
            {
                var projet = await _db.Projets.FindAsync(projetGuid);
                if (projet == null || projet.EstSupprime)
                {
                    ModelState.AddModelError("ProjetId", "Le projet sélectionné n'existe pas.");
                }
                else if (projet.StatutProjet == StatutProjet.Cloture)
                {
                    ModelState.AddModelError("ProjetId", "Impossible de créer une délégation pour un projet clôturé.");
                }
            }

            // Vérifier que le délégant est DSI ou ResponsableSolutionsIT
            if (Guid.TryParse(DelegantId, out delegantGuid))
            {
                var delegant = await _db.Utilisateurs.FindAsync(delegantGuid);
                if (delegant == null || (delegant.Role != RoleUtilisateur.DSI && delegant.Role != RoleUtilisateur.ResponsableSolutionsIT))
                {
                    ModelState.AddModelError("DelegantId", "Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
                }

                // Si l'utilisateur n'est pas AdminIT, il ne peut déléguer que pour lui-même
                if (userRole != "AdminIT" && delegantGuid != userId)
                {
                    ModelState.AddModelError("DelegantId", "Vous ne pouvez créer une délégation que pour vous-même.");
                }
            }

            // Gérer EstActive (checkbox)
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(ProjetId, out projetGuid) && Guid.TryParse(DelegantId, out delegantGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                // Vérifier qu'il n'y a pas déjà une délégation active pour ce projet
                var delegationExistante = await _db.DelegationsChefProjet
                    .AnyAsync(d => d.ProjetId == projetGuid && 
                                 d.EstActive && 
                                 d.DateFin == null && 
                                 !d.EstSupprime);
                
                if (delegationExistante)
                {
                    ModelState.AddModelError("ProjetId", "Une délégation active existe déjà pour ce projet.");
                }
                else
                {
                    var delegation = new DelegationChefProjet
                    {
                        Id = Guid.NewGuid(),
                        ProjetId = projetGuid,
                        DelegantId = delegantGuid,
                        DelegueId = delegueGuid,
                        DateDebut = DateDebut,
                        DateFin = null, // Sera mis à jour lors de la clôture du projet
                        EstActive = estActive,
                        DateCreation = DateTime.Now,
                        CreePar = _currentUserService.Matricule ?? "SYSTEM",
                        EstSupprime = false,
                        ModifiePar = null,
                        DateModification = null
                    };

                    _db.DelegationsChefProjet.Add(delegation);
                    await _db.SaveChangesAsync();

                    await _auditService.LogActionAsync("CREATION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);

                    TempData["Success"] = "Délégation ChefProjet créée avec succès.";
                    return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
                }
            }

            // Recharger les données pour la vue unifiée
            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDelegationChefProjet(Guid id, string ProjetId, string DelegantId, string DelegueId, DateTime DateDebut)
        {
            var existingDelegation = await _db.DelegationsChefProjet.FindAsync(id);
            if (existingDelegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Vérifier les permissions
            if (userRole != "AdminIT" && existingDelegation.DelegantId != userId)
                return Forbid();

            // Validation manuelle
            if (string.IsNullOrWhiteSpace(ProjetId) || !Guid.TryParse(ProjetId, out var projetGuid))
            {
                ModelState.AddModelError("ProjetId", "Le projet est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegantId) || !Guid.TryParse(DelegantId, out var delegantGuid))
            {
                ModelState.AddModelError("DelegantId", "Le délégant est requis.");
            }

            if (string.IsNullOrWhiteSpace(DelegueId) || !Guid.TryParse(DelegueId, out var delegueGuid))
            {
                ModelState.AddModelError("DelegueId", "Le délégué est requis.");
            }

            // Vérifier que le projet existe et n'est pas clôturé
            if (Guid.TryParse(ProjetId, out projetGuid))
            {
                var projet = await _db.Projets.FindAsync(projetGuid);
                if (projet == null || projet.EstSupprime)
                {
                    ModelState.AddModelError("ProjetId", "Le projet sélectionné n'existe pas.");
                }
                else if (projet.StatutProjet == StatutProjet.Cloture)
                {
                    ModelState.AddModelError("ProjetId", "Impossible de modifier une délégation pour un projet clôturé.");
                }
            }

            // Vérifier que le délégant est DSI ou ResponsableSolutionsIT
            if (Guid.TryParse(DelegantId, out delegantGuid))
            {
                var delegant = await _db.Utilisateurs.FindAsync(delegantGuid);
                if (delegant == null || (delegant.Role != RoleUtilisateur.DSI && delegant.Role != RoleUtilisateur.ResponsableSolutionsIT))
                {
                    ModelState.AddModelError("DelegantId", "Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
                }

                // Si l'utilisateur n'est pas AdminIT, il ne peut modifier que ses propres délégations
                if (userRole != "AdminIT" && delegantGuid != userId)
                {
                    ModelState.AddModelError("DelegantId", "Vous ne pouvez modifier que vos propres délégations.");
                }
            }

            // Gérer EstActive (checkbox)
            bool estActive = true;
            if (Request.Form.ContainsKey("EstActive"))
            {
                var estActiveValues = Request.Form["EstActive"];
                var estActiveValue = estActiveValues.FirstOrDefault(v => v != "false");
                estActive = estActiveValue == "true" || estActiveValue == "True";
            }

            if (ModelState.IsValid && Guid.TryParse(ProjetId, out projetGuid) && Guid.TryParse(DelegantId, out delegantGuid) && Guid.TryParse(DelegueId, out delegueGuid))
            {
                // Vérifier qu'il n'y a pas déjà une autre délégation active pour ce projet (sauf celle en cours de modification)
                var autreDelegation = await _db.DelegationsChefProjet
                    .AnyAsync(d => d.ProjetId == projetGuid && 
                                 d.Id != id &&
                                 d.EstActive && 
                                 d.DateFin == null && 
                                 !d.EstSupprime);
                
                if (autreDelegation)
                {
                    ModelState.AddModelError("ProjetId", "Une autre délégation active existe déjà pour ce projet.");
                }
                else
                {
                    existingDelegation.ProjetId = projetGuid;
                    existingDelegation.DelegantId = delegantGuid;
                    existingDelegation.DelegueId = delegueGuid;
                    existingDelegation.DateDebut = DateDebut;
                    // DateFin reste null (sera mis à jour lors de la clôture du projet)
                    existingDelegation.EstActive = estActive;
                    existingDelegation.DateModification = DateTime.Now;
                    existingDelegation.ModifiePar = _currentUserService.Matricule;

                    await _db.SaveChangesAsync();

                    await _auditService.LogActionAsync("MODIFICATION_DELEGATION_CHEFPROJET", "DelegationChefProjet", existingDelegation.Id);

                    TempData["Success"] = "Délégation ChefProjet modifiée avec succès.";
                    return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
                }
            }

            // Recharger les données pour la vue unifiée
            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelegationChefProjet(Guid id)
        {
            var delegation = await _db.DelegationsChefProjet.FindAsync(id);
            if (delegation == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Vérifier les permissions
            if (userRole != "AdminIT" && delegation.DelegantId != userId)
                return Forbid();

            delegation.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);

            TempData["Success"] = "Délégation ChefProjet supprimée.";
            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }
    }
}


