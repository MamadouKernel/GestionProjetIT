using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace GestionProjects.Controllers
{
    // Import Excel des utilisateurs — concern volumineux et distinct du CRUD,
    // isolé ici. Candidat à une extraction vers un IUserImportService dédié.
    public partial class AdminController
    {
        [HttpGet]
        public IActionResult ImportUsers()
        {
            return View(new ImportUsersViewModel());
        }

        [HttpGet]
        public IActionResult DownloadModeleImportUsers()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Utilisateurs");

                worksheet.Cells[1, 1].Value = "Matricule";
                worksheet.Cells[1, 2].Value = "Nom";
                worksheet.Cells[1, 3].Value = "Prénoms";
                worksheet.Cells[1, 4].Value = "Email";
                worksheet.Cells[1, 5].Value = "Code Direction";
                worksheet.Cells[1, 6].Value = "Libellé Direction";
                worksheet.Cells[1, 7].Value = "Rôles";
                worksheet.Cells[1, 8].Value = "Peut Créer Demande";

                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 129, 192));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

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

                worksheet.Column(1).Width = 15;
                worksheet.Column(2).Width = 20;
                worksheet.Column(3).Width = 20;
                worksheet.Column(4).Width = 30;
                worksheet.Column(5).Width = 15;
                worksheet.Column(6).Width = 35;
                worksheet.Column(7).Width = 25;
                worksheet.Column(8).Width = 18;

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

            ImportUsersViewModel BuildVm() => new ImportUsersViewModel { Resultats = resultats, Erreurs = erreurs };

            if (fichierExcel == null || fichierExcel.Length == 0)
            {
                erreurs.Add("Aucun fichier n'a été sélectionné.");
                return View(BuildVm());
            }

            const long MaxFileSize = 5 * 1024 * 1024;
            if (fichierExcel.Length > MaxFileSize)
            {
                erreurs.Add("Le fichier dépasse la taille maximale autorisée (5 Mo).");
                return View(BuildVm());
            }

            if (string.IsNullOrWhiteSpace(motDePasseParDefaut) || motDePasseParDefaut.Length < 12 ||
                !motDePasseParDefaut.Any(char.IsUpper) || !motDePasseParDefaut.Any(char.IsDigit))
            {
                erreurs.Add("Le mot de passe par défaut doit contenir au moins 12 caractères, une majuscule et un chiffre.");
                return View(BuildVm());
            }

            var extension = Path.GetExtension(fichierExcel.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                erreurs.Add("Le fichier doit être au format Excel (.xlsx ou .xls).");
                return View(BuildVm());
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
                    return View(BuildVm());
                }

                var rowCount = worksheet.Dimension?.Rows ?? 0;
                if (rowCount < 2)
                {
                    erreurs.Add("Le fichier Excel doit contenir au moins une ligne de données (en plus de l'en-tête).");
                    return View(BuildVm());
                }

                var existingMatricules = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime)
                    .Select(u => u.Matricule.ToLower())
                    .ToHashSetAsync();

                var existingEmails = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime)
                    .Select(u => u.Email.ToLower())
                    .ToHashSetAsync();

                var directionCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

                var directionsDb = await _db.Directions
                    .Where(d => !d.EstSupprime)
                    .ToDictionaryAsync(d => d.Code, d => d.Id, StringComparer.OrdinalIgnoreCase);

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

                            if (string.IsNullOrWhiteSpace(matricule)) { resultat.Statut = "Erreur"; resultat.Message = "Le matricule est requis."; resultats.Add(resultat); continue; }
                            if (string.IsNullOrWhiteSpace(nom))       { resultat.Statut = "Erreur"; resultat.Message = "Le nom est requis."; resultats.Add(resultat); continue; }
                            if (string.IsNullOrWhiteSpace(prenoms))   { resultat.Statut = "Erreur"; resultat.Message = "Les prénoms sont requis."; resultats.Add(resultat); continue; }
                            if (string.IsNullOrWhiteSpace(email))     { resultat.Statut = "Erreur"; resultat.Message = "L'email est requis."; resultats.Add(resultat); continue; }

                            if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                            { resultat.Statut = "Erreur"; resultat.Message = "Format d'email invalide."; resultats.Add(resultat); continue; }

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
                                    avertissements.Add($"Direction '{codeDirection}' introuvable — utilisateur créé sans direction.");
                                }
                            }

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

                            bool peutCreerDemande = string.IsNullOrWhiteSpace(peutCreerStr) || new[] { "oui", "yes", "1", "true" }
                                .Contains(peutCreerStr.Trim().ToLowerInvariant());

                            var userCreated = await _utilisateurService.CreateUserAsync(
                                matricule, nom, prenoms, email,
                                motDePasseParDefaut, directionId, roles, peutCreerDemande);

                            await _db.SaveChangesAsync();

                            await _auditService.LogActionAsync("CREATION_UTILISATEUR_IMPORT", "Utilisateur", userCreated.Id,
                                null,
                                new { userCreated.Matricule, userCreated.Nom, userCreated.Prenoms, userCreated.Email, Roles = string.Join(", ", roles) });

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

            return View(BuildVm());
        }
    }
}
