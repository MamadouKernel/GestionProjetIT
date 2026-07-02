using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // GET: Détails projet avec onglets
        public async Task<IActionResult> Details(Guid id, [FromServices] IProjetDetailsWorkflowService detailsWorkflow, string? tab = "synthese")
        {
            var userId = User.GetUserIdOrThrow();

            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .Include(p => p.Anomalies)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .Include(p => p.HistoriquePhases)
                    .ThenInclude(h => h.ModifieParUtilisateur)
                .Include(p => p.DemandesCloture)
                .Include(p => p.DernierCommentaireTechniquePar)
                .Include(p => p.CharteValideeParDMUtilisateur)
                .Include(p => p.CharteValideeParDSIUtilisateur)
                .Include(p => p.CharteProjet)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanViewProject)
            {
                return Forbid();
            }

            var isReadOnly = ui.HasDmGovernanceAccess && !ui.IsProjectSponsor && !ui.HasDsiGovernanceAccess;

            await _projetProgress.RecalculateAsync(projet, persistChanges: true);

            var vm = await detailsWorkflow.BuildDetailsViewModelAsync(
                projet,
                userId,
                tab,
                isReadOnly,
                ui.CanReassignChefProjet,
                ui.IsAssignedChefProjet,
                ui.CanOpenChargesTab);

            return View(vm);
        }

        // POST: Mettre à jour le ResponsableSolutionsIT (ChefProjet)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateChefProjet(Guid id, Guid? chefProjetId, [FromServices] IProjetDetailsWorkflowService detailsWorkflow)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanReassignChefProjet)
                return Forbid();

            var result = await detailsWorkflow.UpdateChefProjetAsync(id, chefProjetId);
            if (result.IsNotFound)
                return NotFound();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Demarrer operationnellement le projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DemarrerProjet(Guid id, [FromServices] IProjetDetailsWorkflowService detailsWorkflow)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanStartProject)
                return Forbid();

            var result = await detailsWorkflow.DemarrerProjetAsync(id, User.GetUserIdOrThrow());
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // POST: Suspendre (mettre en pause) le projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SuspendreProjet(Guid id, string motif, [FromServices] IProjetDetailsWorkflowService detailsWorkflow)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanChangePhase)
                return Forbid();

            var result = await detailsWorkflow.SuspendreProjetAsync(id, User.GetUserIdOrThrow(), motif);
            if (result.IsNotFound)
                return NotFound();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // POST: Reprendre le projet suspendu
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ReprendreProjet(Guid id, [FromServices] IProjetDetailsWorkflowService detailsWorkflow)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanChangePhase)
                return Forbid();

            var result = await detailsWorkflow.ReprendreProjetAsync(id, User.GetUserIdOrThrow());
            if (result.IsNotFound)
                return NotFound();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // POST: Valider Phase Analyse (Go/No-Go vers Planification)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ValiderPhaseAnalyse(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);

            if (!ui.CanValidateAnalysePhase && !ui.CanChangePhase)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La phase Analyse ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.PlanificationValidation);

            var blocagesAnalyse = BuildAnalyseBlockingItems(projet, validationLivrables);
            if (blocagesAnalyse.Count > 0)
            {
                TempData["ErrorHtml"] = BuildAnalyseBlockingAlertHtml(blocagesAnalyse);
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }

            if (!projet.CharteValidee)
            {
                projet.CharteValidee = true;
                projet.DateCharteValidee = DateTime.Now;
            }

            // Passer à la phase Planification
            projet.PhaseActuelle = PhaseProjet.PlanificationValidation;
            if (projet.StatutProjet == StatutProjet.NonDemarre || projet.StatutProjet == StatutProjet.EnCours)
            {
                projet.StatutProjet = StatutProjet.EnCours;
            }

            // Ajouter une entrée à l'historique des phases
            _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.PlanificationValidation,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Validation de la phase Analyse - Passage en phase Planification",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            });

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PHASE_ANALYSE", "Projet", projet.Id,
                new { AnciennePhase = PhaseProjet.AnalyseClarification },
                new { NouvellePhase = PhaseProjet.PlanificationValidation });

            TempData["Success"] = "Phase Analyse validée. Le projet passe en phase Planification.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // POST: Ajouter Membre
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterMembre(Guid projetId, Guid utilisateurId, string roleDansProjet, [FromServices] IMembreProjetService membreService)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageProjectMembersAsync(projet))
                return Forbid();

            if (!await membreService.AjouterMembreAsync(projetId, utilisateurId, roleDansProjet))
            {
                TempData["Error"] = "Utilisateur non trouvé.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "analyse" });
            }

            TempData["Success"] = "Membre ajouté au projet.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "analyse" });
        }

        // POST: Retirer/Désactiver un membre du projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RetirerMembre(Guid id, Guid membreId, [FromServices] IMembreProjetService membreService)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanManageProjectMembersAsync(projet))
                return Forbid();

            if (!await membreService.RetirerMembreAsync(id, membreId))
                return NotFound();

            TempData["Success"] = "Membre retiré du projet.";
            return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
        }

        // POST: Mettre à jour pourcentage avancement
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateAvancement(Guid id, int? pourcentageAvancement = null, EtatProjet? etatProjet = null)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .Include(p => p.CharteProjet)
                .Include(p => p.FicheProjet)
                .Include(p => p.DemandesCloture)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            if (!await CanUpdateProjectProgressAsync(projet))
                return Forbid();

            var ancienPourcentage = projet.PourcentageAvancement;

            if (etatProjet.HasValue)
            {
                projet.EtatProjet = etatProjet.Value;
            }

            await _projetProgress.RecalculateAsync(projet);

            // Calcul automatique du RAG
            projet.IndicateurRAG = await _ragCalculationService.CalculerRAGAsync(projet);
            projet.DateDernierCalculRAG = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_AVANCEMENT", "Projet", projet.Id,
                new { PourcentageAvancement = ancienPourcentage },
                new { PourcentageAvancement = projet.PourcentageAvancement, IndicateurRAG = projet.IndicateurRAG, EtatProjet = projet.EtatProjet });

            TempData["Success"] = "Statut mis à jour. L'avancement est recalculé automatiquement.";
            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // POST: Ajouter Risque
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterRisque(
            Guid projetId,
            string description,
            ProbabiliteRisque probabilite,
            ImpactRisque impact,
            string? planMitigation,
            string? responsable)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageAnalyseAsync(projet))
                return Forbid();

            if (!string.IsNullOrWhiteSpace(description))
            {
                var risque = new RisqueProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    Description = description.Trim(),
                    Probabilite = probabilite,
                    Impact = impact,
                    PlanMitigation = string.IsNullOrWhiteSpace(planMitigation) ? string.Empty : planMitigation.Trim(),
                    Responsable = string.IsNullOrWhiteSpace(responsable) ? string.Empty : responsable.Trim(),
                    EstSupprime = false,
                    DateCreationRisque = DateTime.Now,
                    Statut = StatutRisque.Identifie,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? User.Identity?.Name ?? "SYSTEM"
                };

                _db.RisquesProjets.Add(risque);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("AJOUT_RISQUE", "RisqueProjet", risque.Id);

                TempData["Success"] = "Risque ajouté.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "synthese" });
            }

            TempData["Error"] = "La description du risque est obligatoire.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "synthese" });
        }

        // POST: Mettre à jour un risque
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateRisque(Guid id, Guid risqueId, string? description, ProbabiliteRisque? probabilite, ImpactRisque? impact, StatutRisque? statut, string? planMitigation, string? responsable, DateTime? echeance)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanManageAnalyseAsync(projet))
                return Forbid();

            var risque = await _db.RisquesProjets.FindAsync(risqueId);
            if (risque == null || risque.ProjetId != id)
                return NotFound();

            // Mettre à jour les champs fournis
            if (!string.IsNullOrWhiteSpace(description))
                risque.Description = description;

            if (probabilite.HasValue)
                risque.Probabilite = probabilite.Value;

            if (impact.HasValue)
                risque.Impact = impact.Value;

            if (statut.HasValue)
            {
                risque.Statut = statut.Value;
                if (statut.Value == StatutRisque.Clos && !risque.DateCloture.HasValue)
                {
                    risque.DateCloture = DateTime.Now;
                }
            }

            if (!string.IsNullOrWhiteSpace(planMitigation))
                risque.PlanMitigation = string.IsNullOrWhiteSpace(planMitigation) ? string.Empty : planMitigation.Trim();

            if (!string.IsNullOrWhiteSpace(responsable))
                risque.Responsable = string.IsNullOrWhiteSpace(responsable) ? string.Empty : responsable.Trim();

            risque.DateModification = DateTime.Now;
            risque.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MISE_A_JOUR_RISQUE", "RisqueProjet", risque.Id,
                new { ProjetId = id },
                new { Statut = risque.Statut, Probabilite = risque.Probabilite, Impact = risque.Impact });

            TempData["Success"] = "Risque mis à jour avec succès.";
            return RedirectToAction(nameof(Details), new { id, tab = "synthese" });
        }

        // POST: Ajouter Anomalie
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AjouterAnomalie(Guid projetId, AnomalieProjet anomalie)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            if (ModelState.IsValid)
            {
                // Réinitialiser les champs système pour éviter le mass assignment
                anomalie.Id = Guid.NewGuid();
                anomalie.ProjetId = projetId;
                anomalie.EstSupprime = false;
                anomalie.Reference = $"ANOM-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
                anomalie.DateCreationAnomalie = DateTime.Now;
                anomalie.Statut = StatutAnomalie.Ouverte;
                anomalie.RapporteePar = User.FindFirstValue("Nom") + " " + User.FindFirstValue("Prenoms");
                anomalie.DateCreation = DateTime.Now;
                anomalie.CreePar = _currentUserService.Matricule ?? User.Identity?.Name ?? "SYSTEM";

                _db.AnomaliesProjets.Add(anomalie);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("AJOUT_ANOMALIE", "AnomalieProjet", anomalie.Id);

                // Notifier les Responsables Solutions IT si l'anomalie est critique ou haute priorité (demande de support technique)
                if (anomalie.Priorite == PrioriteAnomalie.Critique || anomalie.Priorite == PrioriteAnomalie.Haute)
                {
                    await _notificationService.NotifierResponsablesSolutionsITAsync(
                        TypeNotification.DemandeSupportTechnique,
                        $"Projet {projet.CodeProjet} - Anomalie {anomalie.Priorite}",
                        $"Une anomalie {anomalie.Priorite} a été signalée sur le projet '{projet.Titre}': {anomalie.Description}. Référence: {anomalie.Reference}",
                        "AnomalieProjet",
                        anomalie.Id,
                        new {
                            CodeProjet = projet.CodeProjet,
                            Titre = projet.Titre,
                            Reference = anomalie.Reference,
                            Priorite = anomalie.Priorite.ToString(),
                            Environnement = anomalie.Environnement.ToString()
                        });
                }

                TempData["Success"] = "Anomalie ajoutée.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "execution" });
            }

            TempData["Error"] = "Erreur lors de l'ajout de l'anomalie.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "execution" });
        }

        // POST: Modifier une anomalie
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateAnomalie(Guid projetId, Guid anomalieId,
            StatutAnomalie statut, PrioriteAnomalie? priorite,
            string? assigneeA, string? moduleConcerne, string? commentaireResolution)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            var anomalie = await _db.AnomaliesProjets.FindAsync(anomalieId);
            if (anomalie == null || anomalie.ProjetId != projetId || anomalie.EstSupprime)
                return NotFound();

            anomalie.Statut = statut;
            if (priorite.HasValue) anomalie.Priorite = priorite.Value;
            if (!string.IsNullOrWhiteSpace(assigneeA)) anomalie.AssigneeA = assigneeA.Trim();
            if (!string.IsNullOrWhiteSpace(moduleConcerne)) anomalie.ModuleConcerne = moduleConcerne.Trim();
            if (commentaireResolution != null) anomalie.CommentaireResolution = commentaireResolution.Trim();

            if ((statut == StatutAnomalie.Corrigee || statut == StatutAnomalie.Fermee) && !anomalie.DateResolution.HasValue)
                anomalie.DateResolution = DateTime.Now;

            anomalie.DateModification = DateTime.Now;
            anomalie.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("MISE_A_JOUR_ANOMALIE", "AnomalieProjet", anomalie.Id,
                null, new { Statut = statut });

            TempData["Success"] = "Anomalie mise à jour.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "execution" });
        }

        // POST: Supprimer une anomalie (soft delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SupprimerAnomalie(Guid projetId, Guid anomalieId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null) return NotFound();

            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            var anomalie = await _db.AnomaliesProjets.FindAsync(anomalieId);
            if (anomalie == null || anomalie.ProjetId != projetId || anomalie.EstSupprime)
                return NotFound();

            anomalie.EstSupprime = true;
            anomalie.DateModification = DateTime.Now;
            anomalie.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("SUPPRESSION_ANOMALIE", "AnomalieProjet", anomalie.Id);

            TempData["Success"] = "Anomalie supprimée.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "execution" });
        }
    }
}
