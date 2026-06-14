using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // GET: Gestion des charges du projet
        [Authorize]
        public async Task<IActionResult> Charges(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.Membres)
                .Include(p => p.Charges)
                    .ThenInclude(c => c.Ressource)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == userId);
            var ui = await BuildProjectUiAsync(projet);
            var isPilotage = ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
            var isProjectMember = user != null &&
                                  projet.Membres.Any(m => !m.EstSupprime &&
                                                          !string.IsNullOrWhiteSpace(m.Email) &&
                                                          m.Email == user.Email);

            if (!isPilotage && !isProjectMember)
                return Forbid();

            var viewModel = await _chargeProjetService.BuildChargesViewModelAsync(projet, userId, isPilotage, isProjectMember);

            return View(viewModel);
        }

        // POST: Saisir / mettre à jour une charge
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SaisirCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut, decimal? chargePrevisionnelle, decimal? chargeReelle, string? commentaire, string? typeActivite, string? activite)
        {
            var projet = await _db.Projets
                .Include(p => p.Membres)
                .FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            var isPilotage = ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
            var isResource = userId == ressourceId;

            if (!isPilotage && !isResource)
                return Forbid();

            var canEditForecast = isPilotage;
            var canEditActual = isPilotage || isResource;

            var charge = await _chargeProjetService.SaisirAsync(
                projetId,
                ressourceId,
                semaineDebut,
                chargePrevisionnelle,
                chargeReelle,
                commentaire,
                typeActivite,
                activite,
                userId,
                canEditForecast,
                canEditActual);

            return Json(new
            {
                success = true,
                planned = charge.ChargePrevisionnelle,
                actual = charge.ChargeReelle,
                comment = charge.Commentaire,
                typeActivite = charge.TypeActivite,
                activite = charge.Activite,
                validationStatus = GetValidationChargeLabel(charge.StatutValidation)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SoumettreCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut)
        {
            var projet = await _db.Projets
                .Include(p => p.Membres)
                .FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var isPilotage = await CanValidateChargesAsync(projet);
            var isResource = userId == ressourceId;
            if (!isPilotage && !isResource)
                return Forbid();

            var charge = await _chargeProjetService.SoumettreAsync(projetId, ressourceId, semaineDebut);
            if (charge == null)
                return NotFound();

            return Json(new { success = true, status = GetValidationChargeLabel(charge.StatutValidation) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MettreAJourValidationCharge(Guid projetId, Guid ressourceId, DateTime semaineDebut, StatutValidationCharge statut, string? commentaireValidation)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateChargesAsync(projet))
                return Forbid();

            var lundiSemaine = NormalizeToMonday(semaineDebut);
            var charge = await _db.ChargesProjets.FirstOrDefaultAsync(c => c.ProjetId == projetId && c.RessourceId == ressourceId && c.SemaineDebut.Date == lundiSemaine.Date);
            if (charge == null)
                return NotFound();

            if (statut == StatutValidationCharge.Brouillon)
                return BadRequest();

            charge = await _chargeProjetService.MettreAJourValidationAsync(
                projetId,
                ressourceId,
                semaineDebut,
                statut,
                commentaireValidation,
                userId);
            if (charge == null)
                return NotFound();

            return Json(new
            {
                success = true,
                status = GetValidationChargeLabel(charge.StatutValidation),
                statusClass = GetValidationChargeClass(charge.StatutValidation),
                comment = charge.CommentaireValidation
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ExportChargesCsv(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Charges)
                    .ThenInclude(c => c.Ressource)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            if (!await CanAccessChargesAsync(projet, User.GetUserIdOrThrow()))
                return Forbid();

            var builder = new StringBuilder();
            builder.AppendLine("Projet;Ressource;Semaine;Charge prévue;Charge réelle;Ecart;Type activité;Activité;Commentaire;Validation");

            foreach (var charge in projet.Charges.Where(c => !c.EstSupprime).OrderBy(c => c.SemaineDebut).ThenBy(c => c.Ressource.Nom))
            {
                var line = string.Join(";",
                    EscapeCsv(projet.CodeProjet),
                    EscapeCsv($"{charge.Ressource?.Nom} {charge.Ressource?.Prenoms}".Trim()),
                    EscapeCsv(charge.SemaineDebut.ToString("dd/MM/yyyy")),
                    EscapeCsv(charge.ChargePrevisionnelle.ToString("N1")),
                    EscapeCsv((charge.ChargeReelle ?? 0m).ToString("N1")),
                    EscapeCsv(((charge.ChargeReelle ?? 0m) - charge.ChargePrevisionnelle).ToString("N1")),
                    EscapeCsv(charge.TypeActivite),
                    EscapeCsv(charge.Activite),
                    EscapeCsv(charge.Commentaire),
                    EscapeCsv(GetValidationChargeLabel(charge.StatutValidation)));
                builder.AppendLine(line);
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv; charset=utf-8", $"charges-{projet.CodeProjet}.csv");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers charges
        // ══════════════════════════════════════════════════════════════════════

        private static DateTime NormalizeToMonday(DateTime date)
        {
            return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        }

        private static (string Label, string CssClass) GetCapacityStatus(double utilizationRate)
        {
            if (utilizationRate > 100)
                return ("Surchargé", "badge-modern-danger");

            if (utilizationRate > 80)
                return ("Saturé", "badge-modern-warning");

            if (utilizationRate > 50)
                return ("Presque saturé", "badge-modern-info");

            return ("Disponible", "badge-modern-success");
        }

        private static string GetValidationChargeLabel(StatutValidationCharge statut)
        {
            return statut switch
            {
                StatutValidationCharge.Brouillon => "Brouillon",
                StatutValidationCharge.EnAttente => "En attente",
                StatutValidationCharge.Validee => "Validée",
                StatutValidationCharge.Commentee => "Commentée",
                StatutValidationCharge.Rejetee => "Rejetée",
                _ => "Brouillon"
            };
        }

        private static string GetValidationChargeClass(StatutValidationCharge statut)
        {
            return statut switch
            {
                StatutValidationCharge.Brouillon => "badge-modern-secondary",
                StatutValidationCharge.EnAttente => "badge-modern-warning",
                StatutValidationCharge.Validee => "badge-modern-success",
                StatutValidationCharge.Commentee => "badge-modern-info",
                StatutValidationCharge.Rejetee => "badge-modern-danger",
                _ => "badge-modern-secondary"
            };
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string GetProfilRessourceLabel(ProfilRessource? profil)
        {
            return profil switch
            {
                ProfilRessource.Developpement => "Développement",
                ProfilRessource.Infrastructure => "Infrastructure",
                ProfilRessource.Support => "Support",
                ProfilRessource.DBA => "DBA",
                ProfilRessource.ChefProjet => "Chefferie projet",
                ProfilRessource.Architecte => "Architecture",
                ProfilRessource.Analyste => "Analyse",
                ProfilRessource.Autre => "Autre",
                _ => "Non défini"
            };
        }

        private async Task<bool> CanAccessChargesAsync(Projet projet, Guid userId, Guid? ressourceId = null)
        {
            var ui = await BuildProjectUiAsync(projet);
            if (ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess)
            {
                return true;
            }

            if (ressourceId.HasValue && ressourceId.Value == userId)
            {
                return true;
            }

            var userEmail = await _db.Utilisateurs
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.EstSupprime)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return !string.IsNullOrWhiteSpace(userEmail) &&
                   projet.Membres.Any(m => !m.EstSupprime &&
                                          !string.IsNullOrWhiteSpace(m.Email) &&
                                          m.Email == userEmail);
        }

        private async Task<bool> CanValidateChargesAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.IsAssignedChefProjet || ui.HasDsiGovernanceAccess;
        }
    }
}
