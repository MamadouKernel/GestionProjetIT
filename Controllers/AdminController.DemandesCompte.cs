using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class AdminController
    {
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

            var emailExistant = await _db.Utilisateurs
                .AnyAsync(u => u.Email == demande.Email && !u.EstSupprime);
            if (emailExistant)
            {
                TempData["Error"] = "Un compte avec cet email existe déjà.";
                return RedirectToAction(nameof(DemandesCreationCompte));
            }

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

            _ = _email.EnvoyerCredentielsAsync(demande.Email, nomComplet, matricule, motDePasse);

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
