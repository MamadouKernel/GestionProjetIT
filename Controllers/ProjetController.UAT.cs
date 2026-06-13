using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
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
        // POST: Prêt pour UAT
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> PretUAT(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.ExecutionSuivi)
            {
                TempData["Error"] = "Le projet doit être en phase Exécution.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.UatMep);
            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
                return RedirectToAction(nameof(Details), new { id, tab = "execution" });
            }

            projet.PhaseActuelle = PhaseProjet.UatMep;

            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.UatMep,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Projet prêt pour UAT",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("PASSAGE_UAT", "Projet", projet.Id);

            // Notifier les Responsables Solutions IT de l'entrée en phase UAT
            await _notificationService.NotifierResponsablesSolutionsITAsync(
                TypeNotification.ProjetEntreEnUAT,
                $"Projet {projet.CodeProjet} - Entrée en phase UAT",
                $"Le projet '{projet.Titre}' est entré en phase UAT & MEP. Veuillez suivre les tests et la mise en production.",
                "Projet",
                projet.Id,
                new { CodeProjet = projet.CodeProjet, Titre = projet.Titre, Phase = "UatMep" });

            TempData["Success"] = "Projet passé en phase UAT & MEP.";
            return RedirectToAction(nameof(Details), new { id, tab = "uat" });
        }

        // POST: Valider Recette
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
        public async Task<IActionResult> ValiderRecette(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderRecette") ||
                !await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.UatMep)
            {
                TempData["Error"] = "Le projet doit être en phase UAT & MEP.";
                return RedirectToAction(nameof(Details), new { id });
            }

            projet.RecetteValidee = true;
            projet.DateRecetteValidee = DateTime.Now;
            projet.RecetteValideeParId = userId;
            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_RECETTE", "Projet", projet.Id);

            // Notifier les Responsables Solutions IT que la recette est validée (MEP possible)
            await _notificationService.NotifierResponsablesSolutionsITAsync(
                TypeNotification.ProjetEntreEnMEP,
                $"Projet {projet.CodeProjet} - Recette validée, prêt pour MEP",
                $"La recette du projet '{projet.Titre}' a été validée. Le projet est prêt pour la mise en production (MEP).",
                "Projet",
                projet.Id,
                new { CodeProjet = projet.CodeProjet, Titre = projet.Titre, RecetteValidee = true });

            TempData["Success"] = "Recette validée.";
            return RedirectToAction(nameof(Details), new { id, tab = "uat" });
        }

        // POST: Fin UAT - Prêt pour Clôture
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> FinUAT(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageUatAsync(projet))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.UatMep)
            {
                TempData["Error"] = "Le projet doit être en phase UAT & MEP.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!projet.RecetteValidee)
            {
                TempData["Error"] = "La recette doit être validée avant de passer à la clôture.";
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            if (!projet.MepEffectuee)
            {
                TempData["Error"] = "La MEP doit être enregistrée avant de passer à la clôture.";
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            // ⛔ VALIDATION DES LIVRABLES OBLIGATOIRES (PRD - Blocage automatique)
            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.ClotureLeconsApprises);

            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            if (projet.FicheProjet == null ||
                !projet.FicheProjet.HypercareTermine ||
                string.IsNullOrWhiteSpace(projet.FicheProjet.PeriodeHypercare) ||
                string.IsNullOrWhiteSpace(projet.FicheProjet.StatutHypercare))
            {
                TempData["Error"] = "Le passage en clôture est bloqué tant que l'hypercare n'est pas renseigné et terminé.";
                return RedirectToAction(nameof(Details), new { id, tab = "uat" });
            }

            var anciennePhase = projet.PhaseActuelle;
            projet.PhaseActuelle = PhaseProjet.ClotureLeconsApprises;
            projet.StatutProjet = StatutProjet.ClotureEnCours;

            // Ajouter une entrée à l'historique des phases
            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.ClotureLeconsApprises,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Fin UAT - Passage en phase Clôture",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("FIN_UAT", "Projet", projet.Id,
                new { AnciennePhase = anciennePhase },
                new { NouvellePhase = PhaseProjet.ClotureLeconsApprises });

            TempData["Success"] = "Projet passé en phase Clôture & Leçons apprises.";
            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }

        // ══════════════════════════════════════════════════════════════════════
        // UAT — Gestion des cas de test
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterCasTest(Guid projetId, string titre, string description,
            string resultAttendu, PrioriteAnomalie priorite, bool estObligatoire, Guid? campagneId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageUatAsync(projet)) return Forbid();

            var result = await _uatWorkflow.AjouterCasTestAsync(
                projetId, titre, description, resultAttendu, priorite, estObligatoire, campagneId);
            return MapUatWorkflowToDetails(result, projetId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ExecuterCasTest(Guid projetId, Guid casTestId,
            StatutExecutionTest statut, string? commentaire, Guid? campagneId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            var casTest = await _db.CasTestsProjets.FindAsync(casTestId);
            if (casTest == null || casTest.ProjetId != projetId) return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanEditUat && !ui.CanValidateRecette && !ui.HasDsiGovernanceAccess)
            {
                return Forbid();
            }

            var result = await _uatWorkflow.ExecuterCasTestAsync(
                projetId, casTestId, statut, commentaire, campagneId, userId);
            return MapUatWorkflowToDetails(result, projetId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterCampagneTest(Guid projetId, string nom, string? descriptionCampagne,
            Environnement environnement, DateTime dateLancement)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageUatAsync(projet)) return Forbid();

            var result = await _uatWorkflow.AjouterCampagneTestAsync(
                projetId, nom, descriptionCampagne, environnement, dateLancement);
            return MapUatWorkflowToDetails(result, projetId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerCasTest(Guid projetId, Guid casTestId)
        {
            var casTest = await _db.CasTestsProjets.FindAsync(casTestId);
            if (casTest == null || casTest.ProjetId != projetId) return NotFound();

            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();
            if (!await CanManageUatAsync(projet)) return Forbid();

            var result = await _uatWorkflow.SupprimerCasTestAsync(projetId, casTestId);
            return MapUatWorkflowToDetails(result, projetId);
        }

        private IActionResult MapUatWorkflowToDetails(WorkflowResult result, Guid projetId)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;
            else if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id = projetId, tab = "uat" });
        }
    }
}
