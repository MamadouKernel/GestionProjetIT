using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels.Admin;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace GestionProjects.Infrastructure.Services;

public sealed class UserImportService : IUserImportService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUtilisateurService _utilisateurService;
    private readonly ILogger<UserImportService> _logger;

    public UserImportService(
        ApplicationDbContext db,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IUtilisateurService utilisateurService,
        ILogger<UserImportService> logger)
    {
        _db = db;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _utilisateurService = utilisateurService;
        _logger = logger;
    }

    public async Task<UserImportResult> ImportUsersAsync(
        Stream? fichierExcel,
        string? fileName,
        long fileLength,
        string motDePasseParDefaut,
        bool ignorerDoublons)
    {
        var resultats = new List<ImportResultat>();
        var erreurs = new List<string>();

        ImportUsersViewModel BuildVm() => new() { Resultats = resultats, Erreurs = erreurs };

        if (fichierExcel == null || fileLength == 0)
        {
            erreurs.Add("Aucun fichier n'a Ã©tÃ© sÃ©lectionnÃ©.");
            return new UserImportResult(BuildVm());
        }

        const long maxFileSize = 5 * 1024 * 1024;
        if (fileLength > maxFileSize)
        {
            erreurs.Add("Le fichier dÃ©passe la taille maximale autorisÃ©e (5 Mo).");
            return new UserImportResult(BuildVm());
        }

        if (string.IsNullOrWhiteSpace(motDePasseParDefaut) ||
            motDePasseParDefaut.Length < 12 ||
            !motDePasseParDefaut.Any(char.IsUpper) ||
            !motDePasseParDefaut.Any(char.IsDigit))
        {
            erreurs.Add("Le mot de passe par dÃ©faut doit contenir au moins 12 caractÃ¨res, une majuscule et un chiffre.");
            return new UserImportResult(BuildVm());
        }

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
        {
            erreurs.Add("Le fichier doit Ãªtre au format Excel (.xlsx ou .xls).");
            return new UserImportResult(BuildVm());
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
                return new UserImportResult(BuildVm());
            }

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            if (rowCount < 2)
            {
                erreurs.Add("Le fichier Excel doit contenir au moins une ligne de donnÃ©es (en plus de l'en-tÃªte).");
                return new UserImportResult(BuildVm());
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
                for (var row = 2; row <= rowCount; row++)
                {
                    var resultat = new ImportResultat { Ligne = row };

                    try
                    {
                        var matricule = worksheet.Cells[row, 1].Text?.Trim() ?? "";
                        var nom = worksheet.Cells[row, 2].Text?.Trim() ?? "";
                        var prenoms = worksheet.Cells[row, 3].Text?.Trim() ?? "";
                        var email = worksheet.Cells[row, 4].Text?.Trim() ?? "";
                        var codeDirection = worksheet.Cells[row, 5].Text?.Trim() ?? "";
                        var libelleDirection = worksheet.Cells[row, 6].Text?.Trim() ?? "";
                        var rolesStr = worksheet.Cells[row, 7].Text?.Trim() ?? "";
                        var peutCreerStr = worksheet.Cells[row, 8].Text?.Trim() ?? "";

                        resultat.Matricule = matricule;
                        resultat.Nom = nom;

                        if (string.IsNullOrWhiteSpace(matricule)) { resultat.Statut = "Erreur"; resultat.Message = "Le matricule est requis."; resultats.Add(resultat); continue; }
                        if (string.IsNullOrWhiteSpace(nom)) { resultat.Statut = "Erreur"; resultat.Message = "Le nom est requis."; resultats.Add(resultat); continue; }
                        if (string.IsNullOrWhiteSpace(prenoms)) { resultat.Statut = "Erreur"; resultat.Message = "Les prÃ©noms sont requis."; resultats.Add(resultat); continue; }
                        if (string.IsNullOrWhiteSpace(email)) { resultat.Statut = "Erreur"; resultat.Message = "L'email est requis."; resultats.Add(resultat); continue; }

                        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                        { resultat.Statut = "Erreur"; resultat.Message = "Format d'email invalide."; resultats.Add(resultat); continue; }

                        var matriculeExists = existingMatricules.Contains(matricule.ToLower());
                        var emailExists = existingEmails.Contains(email.ToLower());

                        if (matriculeExists || emailExists)
                        {
                            var msg = matriculeExists && emailExists ? "Matricule et email existent dÃ©jÃ ."
                                : matriculeExists ? "Matricule existe dÃ©jÃ ."
                                : "Email existe dÃ©jÃ .";
                            resultat.Statut = ignorerDoublons ? "IgnorÃ©" : "Erreur";
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
                                    DateCreation = DateTime.UtcNow,
                                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                    EstSupprime = false
                                };
                                _db.Directions.Add(newDir);
                                await _db.SaveChangesAsync();
                                directionId = newDir.Id;
                                directionCache[codeDirection] = newDir.Id;
                                directionsDb[codeDirection] = newDir.Id;
                                avertissements.Add($"Direction '{codeDirection}' crÃ©Ã©e.");
                            }
                            else
                            {
                                avertissements.Add($"Direction '{codeDirection}' introuvable â€” utilisateur crÃ©Ã© sans direction.");
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
                                {
                                    roles.Add(roleEnum);
                                }
                                else
                                {
                                    rolesInvalides.Add(roleTrimmed);
                                }
                            }
                        }

                        if (rolesInvalides.Any())
                        {
                            avertissements.Add($"RÃ´le(s) inconnu(s) ignorÃ©(s) : {string.Join(", ", rolesInvalides)}.");
                        }

                        if (!roles.Any())
                        {
                            roles.Add(RoleUtilisateur.Demandeur);
                        }

                        var peutCreerDemande = string.IsNullOrWhiteSpace(peutCreerStr) ||
                            new[] { "oui", "yes", "1", "true" }.Contains(peutCreerStr.Trim().ToLowerInvariant());

                        var userCreated = await _utilisateurService.CreateUserAsync(
                            matricule,
                            nom,
                            prenoms,
                            email,
                            motDePasseParDefaut,
                            directionId,
                            roles,
                            peutCreerDemande);

                        await _db.SaveChangesAsync();

                        await _auditService.LogActionAsync(
                            "CREATION_UTILISATEUR_IMPORT",
                            "Utilisateur",
                            userCreated.Id,
                            null,
                            new
                            {
                                userCreated.Matricule,
                                userCreated.Nom,
                                userCreated.Prenoms,
                                userCreated.Email,
                                Roles = string.Join(", ", roles)
                            });

                        existingMatricules.Add(matricule.ToLower());
                        existingEmails.Add(email.ToLower());

                        var messageOk = $"Utilisateur crÃ©Ã©. RÃ´les : {string.Join(", ", roles)}.";
                        if (avertissements.Any())
                        {
                            messageOk += " âš  " + string.Join(" ", avertissements);
                        }

                        resultat.Statut = "CrÃ©Ã©";
                        resultat.Message = messageOk;
                        resultats.Add(resultat);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ImportUsers: erreur ligne {Row}", row);
                        resultat.Statut = "Erreur";
                        resultat.Message = "Erreur technique lors du traitement de cette ligne.";
                        resultats.Add(resultat);
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                erreurs.Add("Une erreur critique s'est produite. Aucun utilisateur n'a Ã©tÃ© importÃ©.");
            }

            var successMessage = $"Import terminÃ©. {resultats.Count(r => r.Statut == "CrÃ©Ã©")} utilisateur(s) crÃ©Ã©(s), {resultats.Count(r => r.Statut == "IgnorÃ©")} ignorÃ©(s), {resultats.Count(r => r.Statut == "Erreur")} erreur(s).";
            return new UserImportResult(BuildVm(), successMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportUsers: Ã©chec lecture fichier Excel");
            erreurs.Add("Erreur lors de la lecture du fichier Excel. VÃ©rifiez que le fichier n'est pas corrompu.");
        }

        return new UserImportResult(BuildVm());
    }
}
