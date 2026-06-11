using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // ══════════════════════════════════════════════════════════════════════
        // Collaboration Teams
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ConfigurerCollaboration(Guid projetId,
            ModeCollaborationProjet mode,
            string? nomEquipeTeams, string? teamId, string? teamUrl,
            string? nomCanalTeams, string? channelId, string? channelUrl,
            string? nomPlanPlanner, string? planId, string? planUrl, string? nomBucketPlanner, string? bucketId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageCollaborationAsync(projet)) return Forbid();

            var request = new CollaborationProjetConfigurationRequest
            {
                Mode = mode,
                NomEquipeTeams = nomEquipeTeams ?? $"{projet.CodeProjet} — {projet.Titre}",
                TeamId = teamId,
                TeamUrl = teamUrl,
                NomCanalTeams = nomCanalTeams ?? "Général",
                ChannelId = channelId,
                ChannelUrl = channelUrl,
                NomPlanPlanner = nomPlanPlanner ?? $"Planification {projet.CodeProjet}",
                PlanId = planId,
                PlanUrl = planUrl,
                NomBucketPlanner = nomBucketPlanner,
                BucketId = bucketId
            };

            try
            {
                await _collaboration.ConfigurerAsync(projetId, request, _currentUserService.Matricule);
                TempData["Success"] = "Collaboration configurée avec succès.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la configuration : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "collaboration" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SynchroniserCollaboration(Guid projetId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageCollaborationAsync(projet)) return Forbid();

            try
            {
                var result = await _collaboration.SynchroniserAsync(projetId, _currentUserService.Matricule);
                if (result.Success)
                    TempData["Success"] = $"Synchronisation effectuée : {result.NombreMembres} membres, {result.NombreTaches} tâches.";
                else
                    TempData["Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la synchronisation : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "collaboration" });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Dossier de Signature Électronique
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> InitialiserDossierSignature(Guid projetId,
            FournisseurSignatureElectronique fournisseur)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature) return Forbid();

            try
            {
                var result = await _electronicSignature.InitialiserCharteAsync(projetId, fournisseur, _currentUserService.Matricule);
                if (result != null)
                    TempData["Success"] = "Dossier de signature initialisé.";
                else
                    TempData["Error"] = "Impossible d'initialiser le dossier.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "planification" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnvoyerDossierSignature(Guid projetId, Guid dossierId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature) return Forbid();

            try
            {
                var result = await _electronicSignature.EnvoyerDossierAsync(dossierId, _currentUserService.Matricule);
                if (result.Success)
                    TempData["Success"] = "Dossier envoyé pour signature.";
                else
                    TempData["Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "planification" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DecisionSignataire(Guid projetId, Guid dossierId, Guid signataireId, bool approuver)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null) return NotFound();

            var dossier = await _db.DossiersSignatureProjets
                .Include(d => d.Signataires)
                .FirstOrDefaultAsync(d => d.Id == dossierId && d.ProjetId == projetId && !d.EstSupprime);
            if (dossier == null) return NotFound();

            var signataire = dossier.Signataires.FirstOrDefault(s => s.Id == signataireId);
            if (signataire == null) return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            var canManage = ui.CanManageDossierSignature;
            var canSponsorDecision = await CanValidateCharteAsDirecteurMetierAsync(projet, userId) &&
                                     projet.SponsorId == userId &&
                                     signataire.Role == RoleSignataireProjet.Sponsor;
            var isCurrentSignatory = signataire.UtilisateurId == userId;
            if (!canManage && !canSponsorDecision && !isCurrentSignatory)
            {
                return Forbid();
            }

            try
            {
                var result = await _electronicSignature.EnregistrerDecisionSignataireAsync(
                    dossierId, signataireId, approuver, _currentUserService.Matricule);

                TempData[result.Success ? "Success" : "Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur : {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "planification" });
        }
    }
}
