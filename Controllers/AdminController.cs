using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Helpers;
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
using OfficeOpenXml;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    public class DelegationsViewModel
    {
        public IEnumerable<DelegationValidationDSI> DelegationsDSI { get; set; } = new List<DelegationValidationDSI>();
        public IEnumerable<DelegationChefProjet> DelegationsChefProjet { get; set; } = new List<DelegationChefProjet>();
    }

    [Authorize]
    public class AdminController : Controller
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
            // Délégation vers IUtilisateurService — logique centralisée dans le service
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

            // Règle métier : AdminIT est exclusif
            if (parsedRoles.Contains(RoleUtilisateur.AdminIT))
                return new List<RoleUtilisateur> { RoleUtilisateur.AdminIT };

            return parsedRoles;
        }

        private async Task SynchronizeUserRolesAsync(Utilisateur user, IEnumerable<RoleUtilisateur> selectedRoles)
        {
            // Délégué au service centralisé
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

        // ============ GESTION UTILISATEURS ============

        public async Task<IActionResult> Users(string? recherche = null, Guid? directionId = null, RoleUtilisateur? role = null, int page = 1, int pageSize = 5)
        {
            page     = Math.Max(1, page);
            pageSize = pageSize is 5 or 10 or 15 or 20 or 25 or 50 ? pageSize : 5;

            var query = _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime)
                .AsQueryable();

            // Filtre recherche textuelle (nom, prénom, matricule, email)
            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(u =>
                    u.Nom.Contains(recherche) ||
                    u.Prenoms.Contains(recherche) ||
                    u.Matricule.Contains(recherche) ||
                    u.Email.Contains(recherche));

            // Filtre direction
            if (directionId.HasValue)
                query = query.Where(u => u.DirectionId == directionId.Value);

            // Filtre rôle
            if (role.HasValue)
                query = query.Where(u => u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == role.Value));

            query = query.OrderBy(u => u.Nom).ThenBy(u => u.Prenoms);

            var pagedResult = await query.ToPagedResultAsync(page, pageSize);

            ViewBag.PageNumber  = pagedResult.PageNumber;
            ViewBag.TotalPages  = pagedResult.TotalPages;
            ViewBag.TotalCount  = pagedResult.TotalCount;
            ViewBag.PageSize    = pagedResult.PageSize;
            ViewBag.Recherche   = recherche;
            ViewBag.SelectedDirectionId = directionId;
            ViewBag.SelectedRole        = role;

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();
            ViewBag.AllRoles = Enum.GetValues(typeof(RoleUtilisateur)).Cast<RoleUtilisateur>().ToList();

            return View(pagedResult.Items);
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
            var erreurs   = new List<string>();

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // ── Validations fichier ───────────────────────────────────────────────
            if (fichierExcel == null || fichierExcel.Length == 0)
            {
                erreurs.Add("Aucun fichier n'a été sélectionné.");
                ViewBag.Resultats = resultats; ViewBag.Erreurs = erreurs;
                return View();
            }

            const long MaxFileSize = 5 * 1024 * 1024; // 5 Mo
            if (fichierExcel.Length > MaxFileSize)
            {
                erreurs.Add("Le fichier dépasse la taille maximale autorisée (5 Mo).");
                ViewBag.Resultats = resultats; ViewBag.Erreurs = erreurs;
                return View();
            }

            if (string.IsNullOrWhiteSpace(motDePasseParDefaut) || motDePasseParDefaut.Length < 12 ||
                !motDePasseParDefaut.Any(char.IsUpper) || !motDePasseParDefaut.Any(char.IsDigit))
            {
                erreurs.Add("Le mot de passe par défaut doit contenir au moins 12 caractères, une majuscule et un chiffre.");
                ViewBag.Resultats = resultats; ViewBag.Erreurs = erreurs;
                return View();
            }

            var extension = Path.GetExtension(fichierExcel.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                erreurs.Add("Le fichier doit être au format Excel (.xlsx ou .xls).");
                ViewBag.Resultats = resultats; ViewBag.Erreurs = erreurs;
                return View();
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var stream = new MemoryStream();
                await fichierExcel.CopyToAsync(stream);
                stream.Position = 0;

                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];

                if (worksheet == null)
                {
                    erreurs.Add("Le fichier Excel ne contient aucune feuille de calcul.");
                    ViewBag.Resultats = resultats; ViewBag.Erreurs = erreurs;
                    return View();
                }

                var rowCount = worksheet.Dimension?.Rows ?? 0;
                if (rowCount < 2)
                {
                    erreurs.Add("Le fichier Excel doit contenir au moins une ligne de données (en plus de l'en-tête).");
                    ViewBag.Resultats = resultats; ViewBag.Erreurs = erreurs;
                    return View();
                }

                // ── Pré-charger les matricules / emails existants ─────────────────
                // Évite N×2 requêtes SQL (une par ligne) — on charge une fois toute la liste.
                var existingMatricules = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime)
                    .Select(u => u.Matricule.ToLower())
                    .ToHashSetAsync();

                var existingEmails = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime)
                    .Select(u => u.Email.ToLower())
                    .ToHashSetAsync();

                // Cache des directions déjà traitées dans ce fichier (code → id)
                var directionCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

                // Pré-charger les directions existantes
                var directionsDb = await _db.Directions
                    .Where(d => !d.EstSupprime)
                    .ToDictionaryAsync(d => d.Code, d => d.Id, StringComparer.OrdinalIgnoreCase);

                // ── Traitement dans une transaction ───────────────────────────────
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var resultat = new ImportResultat { Ligne = row };

                        try
                        {
                            var matricule        = worksheet.Cells[row, 1].Text?.Trim() ?? "";
                            var nom              = worksheet.Cells[row, 2].Text?.Trim() ?? "";
                            var prenoms          = worksheet.Cells[row, 3].Text?.Trim() ?? "";
                            var email            = worksheet.Cells[row, 4].Text?.Trim() ?? "";
                            var codeDirection    = worksheet.Cells[row, 5].Text?.Trim() ?? "";
                            var libelleDirection = worksheet.Cells[row, 6].Text?.Trim() ?? "";
                            var rolesStr         = worksheet.Cells[row, 7].Text?.Trim() ?? "";
                            var peutCreerStr     = worksheet.Cells[row, 8].Text?.Trim() ?? "";

                            resultat.Matricule = matricule;
                            resultat.Nom       = nom;

                            // ── Champs obligatoires ───────────────────────────────
                            if (string.IsNullOrWhiteSpace(matricule)) { resultat.Statut = "Erreur"; resultat.Message = "Le matricule est requis."; resultats.Add(resultat); continue; }
                            if (string.IsNullOrWhiteSpace(nom))       { resultat.Statut = "Erreur"; resultat.Message = "Le nom est requis."; resultats.Add(resultat); continue; }
                            if (string.IsNullOrWhiteSpace(prenoms))   { resultat.Statut = "Erreur"; resultat.Message = "Les prénoms sont requis."; resultats.Add(resultat); continue; }
                            if (string.IsNullOrWhiteSpace(email))     { resultat.Statut = "Erreur"; resultat.Message = "L'email est requis."; resultats.Add(resultat); continue; }

                            if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                            { resultat.Statut = "Erreur"; resultat.Message = "Format d'email invalide."; resultats.Add(resultat); continue; }

                            // ── Détection doublons (en mémoire, 0 requête SQL) ────
                            var matriculeExists = existingMatricules.Contains(matricule.ToLower());
                            var emailExists     = existingEmails.Contains(email.ToLower());

                            if (matriculeExists || emailExists)
                            {
                                var msg = matriculeExists && emailExists ? "Matricule et email existent déjà."
                                        : matriculeExists ? "Matricule existe déjà."
                                        : "Email existe déjà.";
                                resultat.Statut  = ignorerDoublons ? "Ignoré" : "Erreur";
                                resultat.Message = msg;
                                resultats.Add(resultat);
                                continue;
                            }

                            // ── Direction ─────────────────────────────────────────
                            Guid? directionId = null;
                            var avertissements = new List<string>();

                            if (!string.IsNullOrWhiteSpace(codeDirection))
                            {
                                if (directionCache.TryGetValue(codeDirection, out var cachedId))
                                {
                                    directionId = cachedId;
                                }
                                else if (directionsDb.TryGetValue(codeDirection, out var dbId))
                                {
                                    directionId = dbId;
                                    directionCache[codeDirection] = dbId;
                                }
                                else if (!string.IsNullOrWhiteSpace(libelleDirection))
                                {
                                    // Créer la direction — elle sera committée avec la transaction
                                    var newDir = new Direction
                                    {
                                        Id = Guid.NewGuid(),
                                        Code = codeDirection,
                                        Libelle = libelleDirection,
                                        EstActive = true,
                                        DateCreation = DateTime.Now,
                                        CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                        EstSupprime = false
                                    };
                                    _db.Directions.Add(newDir);
                                    await _db.SaveChangesAsync();
                                    directionId = newDir.Id;
                                    directionCache[codeDirection] = newDir.Id;
                                    directionsDb[codeDirection]   = newDir.Id;
                                    avertissements.Add($"Direction '{codeDirection}' créée.");
                                }
                                else
                                {
                                    // Code direction fourni mais introuvable et libellé absent
                                    avertissements.Add($"Direction '{codeDirection}' introuvable — utilisateur créé sans direction.");
                                }
                            }

                            // ── Rôles ─────────────────────────────────────────────
                            var roles = new List<RoleUtilisateur>();
                            var rolesInvalides = new List<string>();

                            if (!string.IsNullOrWhiteSpace(rolesStr))
                            {
                                foreach (var roleStr in rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    var roleTrimmed = roleStr.Trim();
                                    if (Enum.TryParse<RoleUtilisateur>(roleTrimmed, true, out var roleEnum))
                                        roles.Add(roleEnum);
                                    else
                                        rolesInvalides.Add(roleTrimmed);
                                }
                            }

                            if (rolesInvalides.Any())
                                avertissements.Add($"Rôle(s) inconnu(s) ignoré(s) : {string.Join(", ", rolesInvalides)}.");

                            if (!roles.Any())
                                roles.Add(RoleUtilisateur.Demandeur);

                            // ── Peut créer demande ────────────────────────────────
                            bool peutCreerDemande = string.IsNullOrWhiteSpace(peutCreerStr) || new[] { "oui", "yes", "1", "true" }
                                .Contains(peutCreerStr.Trim().ToLowerInvariant());

                            // ── Création utilisateur ──────────────────────────────
                            var userCreated = await _utilisateurService.CreateUserAsync(
                                matricule, nom, prenoms, email,
                                motDePasseParDefaut, directionId, roles, peutCreerDemande);

                            await _db.SaveChangesAsync();

                            await _auditService.LogActionAsync("CREATION_UTILISATEUR_IMPORT", "Utilisateur", userCreated.Id,
                                null,
                                new { userCreated.Matricule, userCreated.Nom, userCreated.Prenoms, userCreated.Email, Roles = string.Join(", ", roles) });

                            // Mettre à jour les sets en mémoire pour détecter les doublons intra-fichier
                            existingMatricules.Add(matricule.ToLower());
                            existingEmails.Add(email.ToLower());

                            var messageOk = $"Utilisateur créé. Rôles : {string.Join(", ", roles)}.";
                            if (avertissements.Any()) messageOk += " ⚠ " + string.Join(" ", avertissements);

                            resultat.Statut  = "Créé";
                            resultat.Message = messageOk;
                            resultats.Add(resultat);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "ImportUsers: erreur ligne {Row}", row);
                            resultat.Statut  = "Erreur";
                            resultat.Message = "Erreur technique lors du traitement de cette ligne.";
                            resultats.Add(resultat);
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    erreurs.Add("Une erreur critique s'est produite. Aucun utilisateur n'a été importé.");
                }

                TempData["Success"] = $"Import terminé. {resultats.Count(r => r.Statut == "Créé")} utilisateur(s) créé(s), {resultats.Count(r => r.Statut == "Ignoré")} ignoré(s), {resultats.Count(r => r.Statut == "Erreur")} erreur(s).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImportUsers: échec lecture fichier Excel");
                erreurs.Add("Erreur lors de la lecture du fichier Excel. Vérifiez que le fichier n'est pas corrompu.");
            }

            ViewBag.Resultats = resultats;
            ViewBag.Erreurs   = erreurs;
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
                peutCreerDemandeProjet = user.PeutCreerDemandeProjet,
                profilRessource = user.ProfilRessource.HasValue ? (int)user.ProfilRessource.Value : (int?)null,
                capaciteHebdomadaire = user.CapaciteHebdomadaire
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(string Matricule, string Nom, string Prenoms, string Email, string? DirectionId, string motDePasse, string confirmMotDePasse, string? Roles = null, bool PeutCreerDemandeProjet = true)
        {
            // Validation des champs obligatoires
            if (string.IsNullOrWhiteSpace(Matricule))
                ModelState.AddModelError("Matricule", "Le matricule est requis.");
            if (string.IsNullOrWhiteSpace(Nom))
                ModelState.AddModelError("Nom", "Le nom est requis.");
            if (string.IsNullOrWhiteSpace(Prenoms))
                ModelState.AddModelError("Prenoms", "Les prénoms sont requis.");
            if (string.IsNullOrWhiteSpace(Email))
                ModelState.AddModelError("Email", "L'email est requis.");
            if (string.IsNullOrWhiteSpace(motDePasse))
                ModelState.AddModelError("motDePasse", "Le mot de passe est requis.");
            else if (!ValidationHelper.IsStrongPassword(motDePasse))
                ModelState.AddModelError("motDePasse", ValidationHelper.StrongPasswordPolicyMessage);
            if (string.IsNullOrWhiteSpace(confirmMotDePasse))
                ModelState.AddModelError("confirmMotDePasse", "La confirmation du mot de passe est requise.");
            if (!string.IsNullOrWhiteSpace(motDePasse) && !string.IsNullOrWhiteSpace(confirmMotDePasse) && motDePasse != confirmMotDePasse)
                ModelState.AddModelError("confirmMotDePasse", "Les mots de passe ne correspondent pas.");

            // Unicité via le service
            if (!string.IsNullOrWhiteSpace(Matricule) && await _utilisateurService.MatriculeExisteAsync(Matricule))
                ModelState.AddModelError("Matricule", "Ce matricule existe déjà.");
            if (!string.IsNullOrWhiteSpace(Email) && await _utilisateurService.EmailExisteAsync(Email))
                ModelState.AddModelError("Email", "Cet email existe déjà.");

            if (!ModelState.IsValid)
            {
                ViewBag.Directions = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
                ViewBag.AllRoles = Enum.GetValues(typeof(RoleUtilisateur)).Cast<RoleUtilisateur>().ToList();
                return View("Users", await _db.Utilisateurs.Include(u => u.Direction).Include(u => u.UtilisateurRoles).Where(u => !u.EstSupprime).ToListAsync());
            }

            Guid? directionGuid = Guid.TryParse(DirectionId, out var dg) ? dg : (Guid?)null;
            var rolesSelectionnes = ParseSelectedRoles(Roles);

            var user = await _utilisateurService.CreateUserAsync(
                Matricule, Nom, Prenoms, Email, motDePasse,
                directionGuid, rolesSelectionnes, PeutCreerDemandeProjet);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CreationUtilisateur", "Utilisateur", user.Id,
                null, new { user.Matricule, user.Nom, user.Prenoms, user.Email, Roles = rolesSelectionnes });

            TempData["Success"] = "Utilisateur créé avec succès.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(Guid id, string Matricule, string Nom, string Prenoms, string Email, string? DirectionId, string? nouveauMotDePasse, string? confirmNouveauMotDePasse, string? Roles = null, bool PeutCreerDemandeProjet = true, GestionProjects.Domain.Enums.ProfilRessource? ProfilRessource = null, decimal? CapaciteHebdomadaire = null)
        {
            var existingUser = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (existingUser == null)
                return NotFound();

            // Validation des champs obligatoires
            if (string.IsNullOrWhiteSpace(Matricule))
                ModelState.AddModelError("Matricule", "Le matricule est requis.");
            if (string.IsNullOrWhiteSpace(Nom))
                ModelState.AddModelError("Nom", "Le nom est requis.");
            if (string.IsNullOrWhiteSpace(Prenoms))
                ModelState.AddModelError("Prenoms", "Les prénoms sont requis.");
            if (string.IsNullOrWhiteSpace(Email))
                ModelState.AddModelError("Email", "L'email est requis.");

            // Unicité via le service (exclure l'utilisateur courant)
            if (!string.IsNullOrWhiteSpace(Matricule) && Matricule != existingUser.Matricule
                && await _utilisateurService.MatriculeExisteAsync(Matricule, id))
                ModelState.AddModelError("Matricule", "Ce matricule existe déjà.");
            if (!string.IsNullOrWhiteSpace(Email) && Email != existingUser.Email
                && await _utilisateurService.EmailExisteAsync(Email, id))
                ModelState.AddModelError("Email", "Cet email existe déjà.");

            // Validation mot de passe si fourni
            if (!string.IsNullOrEmpty(nouveauMotDePasse))
            {
                if (string.IsNullOrWhiteSpace(confirmNouveauMotDePasse))
                    ModelState.AddModelError("confirmNouveauMotDePasse", "La confirmation du mot de passe est requise.");
                else if (nouveauMotDePasse != confirmNouveauMotDePasse)
                    ModelState.AddModelError("confirmNouveauMotDePasse", "Les mots de passe ne correspondent pas.");
                else if (!ValidationHelper.IsStrongPassword(nouveauMotDePasse))
                    ModelState.AddModelError("nouveauMotDePasse", ValidationHelper.StrongPasswordPolicyMessage);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Directions = await _db.Directions.Where(d => !d.EstSupprime && d.EstActive).OrderBy(d => d.Libelle).ToListAsync();
                ViewBag.AllRoles = Enum.GetValues(typeof(RoleUtilisateur)).Cast<RoleUtilisateur>().ToList();
                return View("Users", await _db.Utilisateurs.Include(u => u.Direction).Include(u => u.UtilisateurRoles).Where(u => !u.EstSupprime).ToListAsync());
            }

            Guid? directionGuid = Guid.TryParse(DirectionId, out var dg) ? dg : (Guid?)null;
            var rolesSelectionnes = ParseSelectedRoles(Roles);

            await _utilisateurService.UpdateUserAsync(
                id, Matricule, Nom, Prenoms, Email, directionGuid,
                rolesSelectionnes, nouveauMotDePasse, PeutCreerDemandeProjet,
                ProfilRessource, CapaciteHebdomadaire);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("ModificationUtilisateur", "Utilisateur", id,
                new { AncienMatricule = existingUser.Matricule },
                new { NouveauMatricule = Matricule, MotDePasseModifie = !string.IsNullOrEmpty(nouveauMotDePasse), Roles = rolesSelectionnes });

            TempData["Success"] = "Utilisateur modifié avec succès.";
            return RedirectToAction(nameof(Users));
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

            await _auditService.LogActionAsync("DesactivationUtilisateur", "Utilisateur", user.Id,
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

            if (!ValidationHelper.IsStrongPassword(nouveauMotDePasse))
            {
                TempData["Error"] = ValidationHelper.StrongPasswordPolicyMessage;
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

        public async Task<IActionResult> ListeRoles(string? recherche = null, Guid? directionId = null, RoleUtilisateur? role = null, int page = 1, int pageSize = 20)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Utilisateurs
                .Include(u => u.Direction)
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(u => u.Nom.Contains(recherche) || u.Prenoms.Contains(recherche) || u.Matricule.Contains(recherche));

            if (directionId.HasValue)
                query = query.Where(u => u.DirectionId == directionId.Value);

            if (role.HasValue)
                query = query.Where(u => u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == role.Value));

            query = query.OrderBy(u => u.Nom).ThenBy(u => u.Prenoms);

            var paged = await query.ToPagedResultAsync(page, pageSize);

            ViewBag.PageNumber          = paged.PageNumber;
            ViewBag.TotalPages          = paged.TotalPages;
            ViewBag.TotalCount          = paged.TotalCount;
            ViewBag.PageSize            = paged.PageSize;
            ViewBag.Recherche           = recherche;
            ViewBag.SelectedDirectionId = directionId;
            ViewBag.SelectedRole        = role;

            ViewBag.AllRoles   = Enum.GetValues(typeof(RoleUtilisateur)).Cast<RoleUtilisateur>().ToList();
            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            // Comptage des rôles sur l'ensemble (non paginé) pour les stats
            var allUsers = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime)
                .ToListAsync();
            ViewBag.RoleCounts = Enum.GetValues(typeof(RoleUtilisateur))
                .Cast<RoleUtilisateur>()
                .ToDictionary(r => r, r => allUsers.Count(u => u.GetRolesActifs().Contains(r)));

            return View(paged.Items);
        }

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

            // Déléguer le parsing + règle AdminIT exclusif au service
            var rolesEnum = _utilisateurService.ParseSelectedRoles(Roles);

            if (!rolesEnum.Any())
            {
                TempData["Error"] = "Veuillez sélectionner au moins un rôle.";
                return RedirectToAction(nameof(GererRoles), new { id });
            }

            // Informer si AdminIT a forcé l'exclusivité
            var rolesInput = (Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => int.TryParse(r.Trim(), out var rid) ? rid : 0)
                .Where(r => r > 0).ToList();

            if (rolesEnum.Contains(RoleUtilisateur.AdminIT) && rolesInput.Count > 1)
                TempData["Info"] = "Le rôle Admin IT est exclusif : les autres rôles ont été retirés automatiquement.";

            var rolesActuels = user.GetRolesActifs().ToList();

            // Synchronisation via le service — plus de triple vérification EF
            await _utilisateurService.SynchronizeUserRolesAsync(user, rolesEnum);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MODIFICATION_ROLES_UTILISATEUR", "Utilisateur", user.Id,
                new { AnciensRoles = string.Join(", ", rolesActuels) },
                new { NouveauxRoles = string.Join(", ", rolesEnum) });

            TempData["Success"] = $"Rôles de {user.Nom} {user.Prenoms} mis à jour avec succès.";
            return RedirectToAction(nameof(ListeRoles));
        }

        // ============ GESTION DIRECTIONS ============

        public async Task<IActionResult> Directions(string? recherche = null, int page = 1, int pageSize = 20)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Directions
                .Include(d => d.DSI)
                .Where(d => !d.EstSupprime)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d => d.Libelle.Contains(recherche) || d.Code.Contains(recherche));

            query = query.OrderBy(d => d.Libelle);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber = paged.PageNumber;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            ViewBag.PageSize   = paged.PageSize;
            ViewBag.Recherche  = recherche;

            ViewBag.DSIs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View(paged.Items);
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
            string codeFinal = Code?.Trim() ?? string.Empty;
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

                await _auditService.LogActionAsync("CreationDirection", "Direction", direction.Id,
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

                await _auditService.LogActionAsync("ModificationDirection", "Direction", existingDirection.Id,
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

        public async Task<IActionResult> Services(string? recherche = null, Guid? directionId = null, int page = 1, int pageSize = 20)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Services
                .Include(s => s.Direction)
                .Where(s => !s.EstSupprime)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(s => s.Libelle.Contains(recherche) || s.Code.Contains(recherche));

            if (directionId.HasValue)
                query = query.Where(s => s.DirectionId == directionId.Value);

            query = query.OrderBy(s => s.Libelle);

            var paged = await query.ToPagedResultAsync(page, pageSize);
            ViewBag.PageNumber  = paged.PageNumber;
            ViewBag.TotalPages  = paged.TotalPages;
            ViewBag.TotalCount  = paged.TotalCount;
            ViewBag.PageSize    = paged.PageSize;
            ViewBag.Recherche   = recherche;
            ViewBag.SelectedDirectionId = directionId;

            ViewBag.Directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .ToListAsync();

            return View(paged.Items);
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
            string codeFinal = Code?.Trim() ?? string.Empty;
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

            ViewBag.DSIPrincipalId = parametres.FirstOrDefault(p => p.Cle == "DSIPrincipalId")?.Valeur;
            ViewBag.DSIDelegueId = parametres.FirstOrDefault(p => p.Cle == "DSIDelegueId")?.Valeur;
            ViewBag.DelaiInactiviteSessionMinutes = parametres.FirstOrDefault(p => p.Cle == "DelaiInactiviteSessionMinutes")?.Valeur;
            ViewBag.RepertoireStockageRacine = parametres.FirstOrDefault(p => p.Cle == "RepertoireStockageRacine")?.Valeur;
            ViewBag.TypesLivrables = parametres.FirstOrDefault(p => p.Cle == "TypesLivrables")?.Valeur;

            ViewBag.UtilisateursDsi = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                                (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();

            return View(parametres);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnregistrerParametresWorkflow(
            string? dsiPrincipalId,
            string? dsiDelegueId,
            int? delaiInactiviteSessionMinutes,
            string? repertoireStockageRacine,
            string? typesLivrables)
        {
            await UpsertParametreSystemeAsync("DSIPrincipalId", dsiPrincipalId?.Trim() ?? string.Empty, "Identifiant du DSI principal");
            await UpsertParametreSystemeAsync("DSIDelegueId", dsiDelegueId?.Trim() ?? string.Empty, "Identifiant du délégué DSI");
            await UpsertParametreSystemeAsync("DelaiInactiviteSessionMinutes", (delaiInactiviteSessionMinutes ?? 30).ToString(), "Délai d'inactivité de session en minutes");
            await UpsertParametreSystemeAsync("RepertoireStockageRacine", repertoireStockageRacine?.Trim() ?? string.Empty, "Répertoire racine de stockage documentaire");
            await UpsertParametreSystemeAsync("TypesLivrables", typesLivrables?.Trim() ?? string.Empty, "Liste des types de livrables obligatoires");

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("ModificationParametre", "ParametreSysteme", Guid.Empty,
                null,
                new
                {
                    DSIPrincipalId = dsiPrincipalId,
                    DSIDelegueId = dsiDelegueId,
                    DelaiInactiviteSessionMinutes = delaiInactiviteSessionMinutes,
                    RepertoireStockageRacine = repertoireStockageRacine,
                    TypesLivrables = typesLivrables
                });

            TempData["Success"] = "Paramètres workflow enregistrés avec succès.";
            return RedirectToAction(nameof(Parametres));
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

                await _auditService.LogActionAsync("ModificationParametre", "ParametreSysteme", parametre.Id);

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

                await _auditService.LogActionAsync("ModificationParametre", "ParametreSysteme", existingParametre.Id,
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTeamsWebhook(Guid? parametreId, string? webhookUrl)
        {
            var url = webhookUrl?.Trim() ?? string.Empty;

            if (parametreId.HasValue)
            {
                var param = await _db.ParametresSysteme.FindAsync(parametreId.Value);
                if (param != null)
                {
                    param.Valeur = url;
                    param.DateModification = DateTime.Now;
                    param.ModifiePar = _currentUserService.Matricule;
                    await _db.SaveChangesAsync();
                    await _auditService.LogActionAsync("MAJ_TEAMS_WEBHOOK", "ParametreSysteme", param.Id);
                }
            }
            else
            {
                var param = new ParametreSysteme
                {
                    Id = Guid.NewGuid(),
                    Cle = "TeamsWebhookUrl",
                    Valeur = url,
                    Description = "URL du webhook entrant Microsoft Teams pour les notifications",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };
                _db.ParametresSysteme.Add(param);
                await _db.SaveChangesAsync();
                await _auditService.LogActionAsync("CREATION_TEAMS_WEBHOOK", "ParametreSysteme", param.Id);
            }

            TempData["Success"] = string.IsNullOrWhiteSpace(url)
                ? "Webhook Teams supprimé."
                : "Webhook Teams enregistré avec succès.";
            return RedirectToAction(nameof(Parametres));
        }

        // ============ GESTION DÉLÉGATIONS (UNIFIÉE) ============

        public async Task<IActionResult> Delegations(
            string? tab = "dsi",
            string? rechercheDsi = null,
            string? rechercheChef = null,
            int pageDsi = 1, int pageChef = 1,
            int pageSize = 15)
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();
            pageDsi   = Math.Max(1, pageDsi);
            pageChef  = Math.Max(1, pageChef);
            pageSize  = Math.Clamp(pageSize, 5, 100);

            // Délégations DSI
            var delegationsDsiQuery = _db.DelegationsValidationDSI
                .Include(d => d.DSI)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime)
                .AsQueryable();

            if (!hasFullAdminScope)
                delegationsDsiQuery = delegationsDsiQuery.Where(d => d.DSIId == userId || d.DelegueId == userId);

            if (!string.IsNullOrWhiteSpace(rechercheDsi))
                delegationsDsiQuery = delegationsDsiQuery.Where(d =>
                    (d.DSI != null && (d.DSI.Nom.Contains(rechercheDsi) || d.DSI.Prenoms.Contains(rechercheDsi))) ||
                    (d.Delegue != null && (d.Delegue.Nom.Contains(rechercheDsi) || d.Delegue.Prenoms.Contains(rechercheDsi))));

            var pagedDsi = await delegationsDsiQuery.OrderByDescending(d => d.DateDebut).ToPagedResultAsync(pageDsi, pageSize);
            var delegationsDSI = pagedDsi.Items;

            ViewBag.PageNumberDsi = pagedDsi.PageNumber;
            ViewBag.TotalPagesDsi = pagedDsi.TotalPages;
            ViewBag.TotalCountDsi = pagedDsi.TotalCount;
            ViewBag.PageSizeDsi   = pagedDsi.PageSize;
            ViewBag.RechercheDsi  = rechercheDsi;

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
                .Where(d => !d.EstSupprime)
                .AsQueryable();

            if (!hasFullAdminScope)
                queryChefProjet = queryChefProjet.Where(d => d.DelegantId == userId || d.DelegueId == userId);

            if (!string.IsNullOrWhiteSpace(rechercheChef))
                queryChefProjet = queryChefProjet.Where(d =>
                    (d.Delegant != null && (d.Delegant.Nom.Contains(rechercheChef) || d.Delegant.Prenoms.Contains(rechercheChef))) ||
                    (d.Delegue  != null && (d.Delegue.Nom.Contains(rechercheChef)  || d.Delegue.Prenoms.Contains(rechercheChef))));

            var pagedChef = await queryChefProjet.Include(d => d.Projet).OrderByDescending(d => d.DateDebut).ToPagedResultAsync(pageChef, pageSize);
            var delegationsChefProjet = pagedChef.Items;

            ViewBag.PageNumberChef = pagedChef.PageNumber;
            ViewBag.TotalPagesChef = pagedChef.TotalPages;
            ViewBag.TotalCountChef = pagedChef.TotalCount;
            ViewBag.PageSizeChef   = pagedChef.PageSize;
            ViewBag.RechercheChef  = rechercheChef;

            // Liste des projets actifs (non clôturés)
            ViewBag.Projets = await _db.Projets
                .Where(p => !p.EstSupprime && p.StatutProjet != StatutProjet.Cloture)
                .OrderByDescending(p => p.DateCreation)
                .ToListAsync();

            // Liste des utilisateurs pouvant déléguer (DSI et ResponsableSolutionsIT)
            ViewBag.Delegants = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && 
                           (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)) &&
                           (hasFullAdminScope || u.Id == userId))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            // Liste de tous les utilisateurs pouvant recevoir la délégation (tous sauf Demandeur)
            ViewBag.DeleguesChefProjet = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.CurrentUserId = userId;
            ViewBag.CanAdminDelegations = hasFullAdminScope;
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

            var userId = User.GetUserIdOrThrow();
            if (!await HasFullAdminScopeAsync() &&
                delegation.DSIId != userId &&
                delegation.DelegueId != userId)
            {
                return Forbid();
            }

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
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

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
                if (!hasFullAdminScope && dsiGuid != userId)
                {
                    ModelState.AddModelError("DSIId", "Vous ne pouvez créer une délégation DSI que pour vous-même.");
                }
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

                await _auditService.LogActionAsync("CreationDelegationDSI", "DelegationValidationDSI", delegation.Id,
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

            var userId = User.GetUserIdOrThrow();
            if (!await HasFullAdminScopeAsync() && existingDelegation.DSIId != userId)
            {
                return Forbid();
            }

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

                await _auditService.LogActionAsync("ModificationDelegationDSI", "DelegationValidationDSI", existingDelegation.Id);

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

            var userId = User.GetUserIdOrThrow();
            if (!await HasFullAdminScopeAsync() && delegation.DSIId != userId)
            {
                return Forbid();
            }

            delegation.EstSupprime = true;
            delegation.EstActive = false;
            delegation.DateModification = DateTime.Now;
            delegation.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("ClotureDelegationDSI", "DelegationValidationDSI", delegation.Id,
                new { DSIId = delegation.DSIId, DelegueId = delegation.DelegueId, DateDebut = delegation.DateDebut, DateFin = delegation.DateFin });

            TempData["Success"] = "Délégation DSI clôturée.";
            return RedirectToAction(nameof(Delegations), new { tab = "dsi" });
        }

        // ============ GESTION DÉLÉGATIONS CHEF PROJET ============

        public async Task<IActionResult> DelegationsChefProjet()
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            IQueryable<DelegationChefProjet> query = _db.DelegationsChefProjet
                .Include(d => d.Delegant)
                .Include(d => d.Delegue)
                .Where(d => !d.EstSupprime);

            // Si l'utilisateur est DSI ou ResponsableSolutionsIT, voir uniquement ses propres délégations
            if (!hasFullAdminScope)
            {
                query = query.Where(d => d.DelegantId == userId || d.DelegueId == userId);
            }
            // AdminIT voit toutes les délégations

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
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime &&
                            u.UtilisateurRoles.Any(ur => !ur.EstSupprime &&
                                                         (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)) &&
                            (hasFullAdminScope || u.Id == userId))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            // Liste de tous les utilisateurs pouvant recevoir la délégation (tous sauf Demandeur)
            ViewBag.Delegues = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .Where(u => !u.EstSupprime && !u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.Demandeur))
                .OrderBy(u => u.Nom)
                .ToListAsync();

            ViewBag.CurrentUserId = userId;
            ViewBag.CanAdminDelegations = hasFullAdminScope;

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
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            // Vérifier les permissions
            if (!hasFullAdminScope && delegation.DelegantId != userId)
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
            var hasFullAdminScope = await HasFullAdminScopeAsync();

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
                if (delegant == null || !await _db.UtilisateurRoles.AnyAsync(ur =>
                        ur.UtilisateurId == delegantGuid &&
                        !ur.EstSupprime &&
                        (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                {
                    ModelState.AddModelError("DelegantId", "Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
                }

                // Si l'utilisateur n'est pas AdminIT, il ne peut déléguer que pour lui-même
                if (!hasFullAdminScope && delegantGuid != userId)
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
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            // Vérifier les permissions
            if (!hasFullAdminScope && existingDelegation.DelegantId != userId)
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
                if (delegant == null || !await _db.UtilisateurRoles.AnyAsync(ur =>
                        ur.UtilisateurId == delegantGuid &&
                        !ur.EstSupprime &&
                        (ur.Role == RoleUtilisateur.DSI || ur.Role == RoleUtilisateur.ResponsableSolutionsIT)))
                {
                    ModelState.AddModelError("DelegantId", "Le délégant doit être un DSI ou un ResponsableSolutionsIT.");
                }

                // Si l'utilisateur n'est pas AdminIT, il ne peut modifier que ses propres délégations
                if (!hasFullAdminScope && delegantGuid != userId)
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
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            // Vérifier les permissions
            if (!hasFullAdminScope && delegation.DelegantId != userId)
                return Forbid();

            delegation.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_DELEGATION_CHEFPROJET", "DelegationChefProjet", delegation.Id);

            TempData["Success"] = "Délégation ChefProjet supprimée.";
            return RedirectToAction(nameof(Delegations), new { tab = "chefprojet" });
        }

        // ============ WORKFLOW CRÉATION DE COMPTES ============

        /// <summary>Liste des demandes de création de compte — DM voit les siennes, DSI/AdminIT/Responsable voit tout.</summary>
        public async Task<IActionResult> DemandesCreationCompte()
        {
            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();
            var canValidateAsDsi = await CanValidateAccountRequestAsDsiAsync();
            var canValidateAsDm = await CanValidateAccountRequestAsDmAsync();

            IQueryable<DemandeCreationCompte> query = _db.DemandesCreationCompte
                .Include(d => d.Direction)
                .Include(d => d.DirecteurMetier)
                .Where(d => !d.EstSupprime);

            if (hasFullAdminScope || canValidateAsDsi)
            {
            }
            else if (canValidateAsDm)
            {
                query = query.Where(d => d.DirecteurMetierId == userId);
            }
            else
            {
                return Forbid();
            }

            var demandes = await query.OrderByDescending(d => d.DateSoumission).ToListAsync();
            return View(demandes);
        }

        /// <summary>DM valide une demande de création de compte.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ValiderDemandeCreationCompteDM(Guid id, string? commentaire)
        {
            var demande = await _db.DemandesCreationCompte
                .Include(d => d.Direction)
                .Include(d => d.DirecteurMetier)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (demande == null) return NotFound();

            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (!hasFullAdminScope && !await CanValidateAccountRequestAsDmAsync())
                return Forbid();

            if (!hasFullAdminScope && demande.DirecteurMetierId != userId)
                return Forbid();

            if (demande.Statut != StatutDemandeCompte.EnAttenteValidationDM)
            {
                TempData["Error"] = "Cette demande n'est pas en attente de validation DM.";
                return RedirectToAction(nameof(DemandesCreationCompte));
            }

            demande.Statut = StatutDemandeCompte.ValideeParDM;
            demande.CommentaireDM = commentaire;
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_DM_COMPTE", "DemandeCreationCompte", demande.Id);

            // Notifier tous les DSI/AdminIT/ResponsableSolutionsIT
            var roles = new[] { RoleUtilisateur.DSI, RoleUtilisateur.AdminIT, RoleUtilisateur.ResponsableSolutionsIT };
            var destinataires = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .Join(_db.UtilisateurRoles.Where(r => roles.Contains(r.Role) && !r.EstSupprime),
                      u => u.Id, r => r.UtilisateurId, (u, r) => u)
                .Where(u => u.Email != null)
                .Select(u => u.Email!)
                .Distinct()
                .ToListAsync();

            var nomDM = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            foreach (var email in destinataires)
                _ = _email.EnvoyerDemandeCreationCompteAuDSIAsync(
                    email,
                    $"{demande.Nom} {demande.Prenoms}",
                    nomDM,
                    demande.Direction?.Libelle ?? "—",
                    demande.Service);

            TempData["Success"] = $"Demande de {demande.Nom} {demande.Prenoms} validée. La DSI a été notifiée.";
            return RedirectToAction(nameof(DemandesCreationCompte));
        }

        /// <summary>DM refuse une demande de création de compte.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RefuserDemandeCreationCompteDM(Guid id, string? commentaire)
        {
            var demande = await _db.DemandesCreationCompte
                .Include(d => d.DirecteurMetier)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (demande == null) return NotFound();

            var userId = User.GetUserIdOrThrow();
            var hasFullAdminScope = await HasFullAdminScopeAsync();

            if (!hasFullAdminScope && !await CanValidateAccountRequestAsDmAsync())
                return Forbid();

            if (!hasFullAdminScope && demande.DirecteurMetierId != userId)
                return Forbid();

            demande.Statut = StatutDemandeCompte.RefuseeParDM;
            demande.CommentaireDM = commentaire;
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            var acteur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _email.EnvoyerRefusCreationCompteAsync(
                demande.Email, $"{demande.Nom} {demande.Prenoms}", acteur, commentaire);

            TempData["Success"] = "Demande refusée. L'intéressé a été notifié.";
            return RedirectToAction(nameof(DemandesCreationCompte));
        }

        /// <summary>DSI/Responsable valide et crée le compte.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ValiderDemandeCreationCompteDSI(Guid id, string? commentaire, RoleUtilisateur role = RoleUtilisateur.Demandeur)
        {
            if (!await HasFullAdminScopeAsync() && !await CanValidateAccountRequestAsDsiAsync())
                return Forbid();

            var demande = await _db.DemandesCreationCompte
                .Include(d => d.Direction)
                .Include(d => d.DirecteurMetier)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (demande == null) return NotFound();

            if (demande.Statut != StatutDemandeCompte.ValideeParDM)
            {
                TempData["Error"] = "Cette demande doit d'abord être validée par le Directeur Métier.";
                return RedirectToAction(nameof(DemandesCreationCompte));
            }

            // Vérifier qu'il n'existe pas déjà un utilisateur avec cet email
            var emailExistant = await _db.Utilisateurs
                .AnyAsync(u => u.Email == demande.Email && !u.EstSupprime);
            if (emailExistant)
            {
                TempData["Error"] = "Un compte avec cet email existe déjà.";
                return RedirectToAction(nameof(DemandesCreationCompte));
            }

            // Générer matricule (préfixe prenom.nom) + mot de passe
            var baseMatricule = $"{demande.Prenoms[0]}{demande.Nom}".ToLower()
                .Replace(" ", "").Replace("-", "");
            var matricule = baseMatricule;
            var compteur = 1;
            while (await _db.Utilisateurs.AnyAsync(u => u.Matricule == matricule))
                matricule = $"{baseMatricule}{compteur++}";

            var motDePasse = GenererMotDePasse();
            var hash = BCrypt.Net.BCrypt.HashPassword(motDePasse);

            var utilisateur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = matricule,
                MotDePasse = hash,
                Nom = demande.Nom,
                Prenoms = demande.Prenoms,
                Email = demande.Email,
                DirectionId = demande.DirectionId,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                ModifiePar = string.Empty,
                EstSupprime = false,
                NombreConnexion = 0
            };
            _db.Utilisateurs.Add(utilisateur);

            _db.UtilisateurRoles.Add(new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = utilisateur.Id,
                Role = role,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                EstSupprime = false
            });

            demande.Statut = StatutDemandeCompte.CompteCree;
            demande.CommentaireDSI = commentaire;
            demande.UtilisateurCreePar = utilisateur.Id;
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CREATION_COMPTE_DSI", "DemandeCreationCompte", demande.Id,
                null, new { MatriculeCreated = matricule, Role = role.ToString() });

            var nomComplet = $"{demande.Nom} {demande.Prenoms}";

            // Envoyer les credentials au nouvel utilisateur
            _ = _email.EnvoyerCredentielsAsync(demande.Email, nomComplet, matricule, motDePasse);

            // Informer le DM
            if (demande.DirecteurMetier?.Email != null)
                _ = _email.EnvoyerConfirmationCreationCompteAuDMAsync(
                    demande.DirecteurMetier.Email,
                    $"{demande.DirecteurMetier.Nom} {demande.DirecteurMetier.Prenoms}",
                    nomComplet);

            TempData["Success"] = $"Compte créé pour {nomComplet}. Les identifiants ont été envoyés par email.";
            return RedirectToAction(nameof(DemandesCreationCompte));
        }

        /// <summary>DSI refuse la création de compte.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RefuserDemandeCreationCompteDSI(Guid id, string? commentaire)
        {
            if (!await HasFullAdminScopeAsync() && !await CanValidateAccountRequestAsDsiAsync())
                return Forbid();

            var demande = await _db.DemandesCreationCompte.FindAsync(id);
            if (demande == null) return NotFound();

            demande.Statut = StatutDemandeCompte.RefuseeParDSI;
            demande.CommentaireDSI = commentaire;
            demande.DateModification = DateTime.Now;
            demande.ModifiePar = _currentUserService.Matricule;
            await _db.SaveChangesAsync();

            var acteur = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim();
            _ = _email.EnvoyerRefusCreationCompteAsync(
                demande.Email, $"{demande.Nom} {demande.Prenoms}", acteur, commentaire);

            TempData["Success"] = "Demande refusée par la DSI. L'intéressé a été notifié.";
            return RedirectToAction(nameof(DemandesCreationCompte));
        }

        private static string GenererMotDePasse()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghjkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@#$!";
            var pwd = new System.Text.StringBuilder();
            pwd.Append(upper[System.Security.Cryptography.RandomNumberGenerator.GetInt32(upper.Length)]);
            pwd.Append(lower[System.Security.Cryptography.RandomNumberGenerator.GetInt32(lower.Length)]);
            pwd.Append(digits[System.Security.Cryptography.RandomNumberGenerator.GetInt32(digits.Length)]);
            pwd.Append(special[System.Security.Cryptography.RandomNumberGenerator.GetInt32(special.Length)]);
            var all = upper + lower + digits + special;
            for (int i = 0; i < 8; i++) pwd.Append(all[System.Security.Cryptography.RandomNumberGenerator.GetInt32(all.Length)]);
            var chars = pwd.ToString().ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            { var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
            return new string(chars);
        }
    }
}
