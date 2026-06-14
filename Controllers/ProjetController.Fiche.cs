using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // GET: Afficher/Éditer la fiche projet CIT
        [Authorize]
        public async Task<IActionResult> FicheProjet(Guid id, [FromServices] IFicheProjetService ficheService)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var fiche = await ficheService.ObtenirPourAffichageAsync(projet);

            return View(new GestionProjects.Application.ViewModels.Projet.FicheProjetPageViewModel { Fiche = fiche, Projet = projet });
        }

        // POST: Sauvegarder la fiche projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SauvegarderFicheProjet(Guid id, FicheProjet fiche, [FromServices] IFicheProjetService ficheService)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanEditFicheProjetAsync(projet))
            {
                return Forbid();
            }

            var result = await ficheService.SauvegarderAsync(id, fiche, User.GetUserIdOrThrow());
            if (result.IsNotFound)
                return NotFound();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(FicheProjet), new { id });
        }

        // POST: Mettre à jour les données structurées de planification
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdatePlanification(
            Guid id,
            FicheProjet fiche,
            string? ganttPayload = null,
            string? raciPayload = null,
            string? communicationPayload = null,
            string? budgetPayload = null,
            string? kickOffPayload = null)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManagePlanificationAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);
            ficheProjet.ProchainJalon = fiche.ProchainJalon;
            ficheProjet.JalonsPrincipaux = fiche.JalonsPrincipaux;
            ficheProjet.DecoupageLotsTravail = fiche.DecoupageLotsTravail;
            ficheProjet.PlanificationRessources = fiche.PlanificationRessources;
            ficheProjet.RaciParActivite = fiche.RaciParActivite;
            ficheProjet.FrequenceReunions = fiche.FrequenceReunions;
            ficheProjet.ParticipantsReunions = fiche.ParticipantsReunions;
            ficheProjet.CanalCommunication = fiche.CanalCommunication;
            ficheProjet.CopilPrevu = fiche.CopilPrevu;
            ficheProjet.BudgetPrevisionnel = fiche.BudgetPrevisionnel;
            ficheProjet.CommentaireBudgetPlanification = fiche.CommentaireBudgetPlanification;
            ficheProjet.CommentaireValidationPlanification = fiche.CommentaireValidationPlanification;
            ficheProjet.SyntheseRisques = fiche.SyntheseRisques;
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            IReadOnlyList<PlanningTacheInputViewModel> planningTasks;
            IReadOnlyList<RaciLigneInputViewModel> raciLines;
            IReadOnlyList<CommunicationLigneInputViewModel> communicationLines;
            IReadOnlyList<BudgetLigneInputViewModel> budgetLines;
            PvKickOffInputViewModel kickOff;
            try
            {
                planningTasks = ParsePlanningTaches(ganttPayload);
                raciLines = ParseRaciLignes(raciPayload);
                communicationLines = ParseCommunicationLignes(communicationPayload);
                budgetLines = ParseBudgetLignes(budgetPayload);
                kickOff = ParsePvKickOff(kickOffPayload);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id, tab = "planification" });
            }

            if (fiche.BudgetPrevisionnel.HasValue && ficheProjet.BudgetConsomme.HasValue)
            {
                ficheProjet.EcartsBudget = ficheProjet.BudgetConsomme.Value - fiche.BudgetPrevisionnel.Value;
            }

            if (planningTasks.Count > 0)
            {
                SynchronizePlanningSummary(projet, ficheProjet, planningTasks);
            }

            SynchronizeRaciSummary(ficheProjet, raciLines);
            SynchronizeCommunicationSummary(ficheProjet, communicationLines);
            SynchronizeBudgetSummary(ficheProjet, budgetLines);

            await ReplacePlanningTasksAsync(projet, userId, planningTasks);
            await ReplaceRaciLinesAsync(projet, raciLines);
            await ReplaceCommunicationLinesAsync(projet, communicationLines);
            await ReplaceBudgetLinesAsync(projet, budgetLines);
            await UpsertPvKickOffAsync(projet, userId, kickOff);

            await _db.SaveChangesAsync();

            var generation = await GenerateNativePlanificationLivrablesAsync(
                projet,
                userId,
                planningTasks,
                raciLines,
                communicationLines,
                budgetLines,
                kickOff);

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_PLANIFICATION", "Projet", projet.Id);

            TempData["Success"] = generation.Generated.Any()
                ? $"Planification mise à jour. Livrables natifs générés : {string.Join(", ", generation.Generated)}."
                : "Planification mise à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererLivrablesPlanificationDepuisGantt(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.TachesPlanning)
                .Include(p => p.LignesRaci)
                .Include(p => p.LignesCommunication)
                .Include(p => p.LignesBudgetPlanification)
                .Include(p => p.PvKickOff)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManagePlanificationAsync(projet))
                return Forbid();

            var tasks = projet.TachesPlanning
                .Where(t => !t.EstSupprime)
                .OrderBy(t => t.Ordre)
                .ThenBy(t => t.DateDebutPrevue)
                .Select(t => new PlanningTacheInputViewModel
                {
                    CodeWbs = t.CodeWbs,
                    Libelle = t.Libelle,
                    Responsable = t.Responsable,
                    DateDebutPrevue = t.DateDebutPrevue,
                    DateFinPrevue = t.DateFinPrevue,
                    Avancement = t.Avancement,
                    EstJalon = t.EstJalon,
                    Dependances = t.Dependances,
                    Commentaire = t.Commentaire,
                    Ordre = t.Ordre
                })
                .ToList();

            var raciLines = projet.LignesRaci
                .Where(l => !l.EstSupprime)
                .OrderBy(l => l.Ordre)
                .Select(l => new RaciLigneInputViewModel
                {
                    CodeActivite = l.CodeActivite,
                    Activite = l.Activite,
                    Responsable = l.Responsable,
                    Approbateur = l.Approbateur,
                    Consulte = l.Consulte,
                    Informe = l.Informe,
                    Ordre = l.Ordre
                })
                .ToList();

            var communicationLines = projet.LignesCommunication
                .Where(l => !l.EstSupprime)
                .OrderBy(l => l.Ordre)
                .Select(l => new CommunicationLigneInputViewModel
                {
                    Instance = l.Instance,
                    Objectif = l.Objectif,
                    Frequence = l.Frequence,
                    Canal = l.Canal,
                    Participants = l.Participants,
                    Responsable = l.Responsable,
                    EstCopil = l.EstCopil,
                    Ordre = l.Ordre
                })
                .ToList();

            var budgetLines = projet.LignesBudgetPlanification
                .Where(l => !l.EstSupprime)
                .OrderBy(l => l.Ordre)
                .Select(l => new BudgetLigneInputViewModel
                {
                    Poste = l.Poste,
                    Description = l.Description,
                    Montant = l.Montant,
                    Commentaire = l.Commentaire,
                    Ordre = l.Ordre
                })
                .ToList();

            var kickOff = projet.PvKickOff == null || projet.PvKickOff.EstSupprime
                ? new PvKickOffInputViewModel()
                : new PvKickOffInputViewModel
                {
                    DateReunion = projet.PvKickOff.DateReunion,
                    Heure = projet.PvKickOff.Heure,
                    Lieu = projet.PvKickOff.Lieu,
                    Animateur = projet.PvKickOff.Animateur,
                    Objectifs = projet.PvKickOff.Objectifs,
                    Participants = projet.PvKickOff.Participants,
                    OrdreDuJour = projet.PvKickOff.OrdreDuJour,
                    Decisions = projet.PvKickOff.Decisions,
                    Actions = projet.PvKickOff.Actions,
                    Commentaires = projet.PvKickOff.Commentaires
                };

            var generation = await GenerateNativePlanificationLivrablesAsync(
                projet,
                userId,
                tasks,
                raciLines,
                communicationLines,
                budgetLines,
                kickOff);

            if (!generation.Generated.Any())
            {
                TempData["Error"] = "Complétez d'abord les sections natives de planification avant de générer les livrables.";
                return RedirectToAction(nameof(Details), new { id, tab = "planification" });
            }

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("GENERATE_PLANIFICATION_LIVRABLES_FROM_GANTT", "Projet", projet.Id);

            TempData["Success"] = $"Les livrables natifs ont été générés : {string.Join(", ", generation.Generated)}.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // POST: Mettre à jour les données de pilotage d'exécution
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateExecution(Guid id, FicheProjet fiche, EtatProjet etatProjet, DateTime? dateFinPrevue, string? prochainJalon, int? pourcentageAvancement = null)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageExecutionAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);
            ficheProjet.DateDebutReelleExecution = fiche.DateDebutReelleExecution;
            ficheProjet.DateFinEstimeeExecution = fiche.DateFinEstimeeExecution ?? dateFinPrevue;
            ficheProjet.JustificationRetardExecution = fiche.JustificationRetardExecution;
            ficheProjet.CommentaireAvancementExecution = fiche.CommentaireAvancementExecution;
            ficheProjet.ActionsRealiseesExecution = fiche.ActionsRealiseesExecution;
            ficheProjet.ActionsAVenirExecution = fiche.ActionsAVenirExecution;
            ficheProjet.ProblemesBlocagesExecution = fiche.ProblemesBlocagesExecution;
            ficheProjet.BudgetConsomme = fiche.BudgetConsomme;
            ficheProjet.JustificationBudgetExecution = fiche.JustificationBudgetExecution;
            ficheProjet.SyntheseChargesExecution = fiche.SyntheseChargesExecution;
            ficheProjet.DecisionsExecution = fiche.DecisionsExecution;
            ficheProjet.ProchainJalon = prochainJalon;
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            projet.EtatProjet = etatProjet;
            projet.DateFinPrevue = dateFinPrevue;

            if (ficheProjet.BudgetPrevisionnel.HasValue && ficheProjet.BudgetConsomme.HasValue)
            {
                ficheProjet.EcartsBudget = ficheProjet.BudgetConsomme.Value - ficheProjet.BudgetPrevisionnel.Value;
            }

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_EXECUTION", "Projet", projet.Id);

            TempData["Success"] = "Suivi d'exécution mis à jour. L'avancement est recalculé automatiquement.";
            return RedirectToAction(nameof(Details), new { id, tab = "execution" });
        }

        // POST: Mettre à jour les données UAT / MEP
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateUat(Guid id, FicheProjet fiche, DateTime? dateMepReelle, bool mepEffectuee)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageUatAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);
            ficheProjet.DateDebutRecette = fiche.DateDebutRecette;
            ficheProjet.DateFinRecette = fiche.DateFinRecette;
            ficheProjet.UtilisateursTesteurs = fiche.UtilisateursTesteurs;
            ficheProjet.PerimetreTeste = fiche.PerimetreTeste;
            ficheProjet.DateMepPrevue = fiche.DateMepPrevue;
            ficheProjet.PrerequisMep = fiche.PrerequisMep;
            ficheProjet.PlanMep = fiche.PlanMep;
            ficheProjet.PlanRollback = fiche.PlanRollback;
            ficheProjet.ChangeRequis = fiche.ChangeRequis;
            ficheProjet.ReferenceChange = fiche.ReferenceChange;
            ficheProjet.StatutValidationChange = fiche.StatutValidationChange;
            ficheProjet.ResultatMep = fiche.ResultatMep;
            ficheProjet.IncidentsMep = fiche.IncidentsMep;
            ficheProjet.PeriodeHypercare = fiche.PeriodeHypercare;
            ficheProjet.IncidentsPostMep = fiche.IncidentsPostMep;
            ficheProjet.StatutHypercare = fiche.StatutHypercare;
            ficheProjet.HypercareTermine = fiche.HypercareTermine;
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            projet.MepEffectuee = mepEffectuee;
            projet.DateMep = mepEffectuee ? dateMepReelle ?? projet.DateMep ?? DateTime.Now : null;

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_UAT_MEP", "Projet", projet.Id);

            TempData["Success"] = "Données UAT / MEP mises à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "uat" });
        }

        // POST: Mettre à jour Bilan
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateBilan(
            Guid id,
            string? bilanPerimetre,
            string? bilanPlanning,
            string? bilanBudget,
            string? bilanDifficultes,
            string? bilanReussites,
            string? leconsReussites,
            string? leconsEchecs,
            string? leconsRecommandations,
            bool transfertRunDocumentation,
            bool transfertRunAcces,
            bool transfertRunSupportInforme,
            bool transfertRunExploitationPrete,
            string? statutFinalCloture,
            string? commentaireStatutFinal)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.Charges)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanManageClotureAsync(projet))
                return Forbid();

            var ficheProjet = await GetOrCreateFicheProjetAsync(id, userId);

            projet.BilanPerimetre = bilanPerimetre?.Trim();
            projet.BilanPlanning = bilanPlanning?.Trim();
            projet.BilanBudget = bilanBudget?.Trim();
            projet.BilanDifficultes = bilanDifficultes?.Trim();
            projet.BilanReussites = bilanReussites?.Trim();
            projet.LeconsReussites = leconsReussites?.Trim();
            projet.LeconsEchecs = leconsEchecs?.Trim();
            projet.LeconsRecommandations = leconsRecommandations?.Trim();

            ficheProjet.TransfertRunDocumentation = transfertRunDocumentation;
            ficheProjet.TransfertRunAcces = transfertRunAcces;
            ficheProjet.TransfertRunSupportInforme = transfertRunSupportInforme;
            ficheProjet.TransfertRunExploitationPrete = transfertRunExploitationPrete;
            ficheProjet.StatutFinalCloture = string.IsNullOrWhiteSpace(statutFinalCloture) ? "Clôturé" : statutFinalCloture.Trim();
            ficheProjet.CommentaireStatutFinal = commentaireStatutFinal?.Trim();
            ficheProjet.DateDerniereMiseAJour = DateTime.Now;
            ficheProjet.DerniereMiseAJourParId = userId;
            ficheProjet.DateModification = DateTime.Now;
            ficheProjet.ModifiePar = _currentUserService.Matricule;

            var chargePrevue = projet.Charges.Where(c => !c.EstSupprime).Sum(c => c.ChargePrevisionnelle);
            var chargeReelle = projet.Charges.Where(c => !c.EstSupprime).Sum(c => c.ChargeReelle ?? 0m);
            projet.BilanCloture =
                $"Périmètre: {projet.BilanPerimetre}\n" +
                $"Planning: {projet.BilanPlanning}\n" +
                $"Budget: {projet.BilanBudget}\n" +
                $"Charge: prévu {chargePrevue:N1} h / réel {chargeReelle:N1} h\n" +
                $"Difficultés: {projet.BilanDifficultes}\n" +
                $"Réussites: {projet.BilanReussites}";

            projet.LeconsApprises =
                $"Réussites: {projet.LeconsReussites}\n" +
                $"Échecs: {projet.LeconsEchecs}\n" +
                $"Recommandations: {projet.LeconsRecommandations}";

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_BILAN", "Projet", projet.Id);

            TempData["Success"] = "Bilan de clôture mis à jour.";
            return RedirectToAction(nameof(Details), new { id, tab = "cloture" });
        }

        // POST: Valider Planification par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
        public async Task<IActionResult> ValiderPlanifDM(Guid id)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderPlanificationDM") ||
                !await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.PlanificationValidation)
            {
                TempData["Error"] = "La planification ne peut être validée qu'en phase Planification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            projet.PlanningValideParDM = true;
            projet.DatePlanningValideParDM = DateTime.Now;
            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PLANIF_DM", "Projet", projet.Id);

            TempData["Success"] = "Planification validée par le Directeur Métier.";
            return RedirectToAction(nameof(Details), new { id, tab = "planification" });
        }

        // POST: Valider Planification par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        public async Task<IActionResult> ValiderPlanifDSI(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            if (!await CanViewProjectAsync(projet))
            {
                return Forbid();
            }

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            if (projet.PhaseActuelle != PhaseProjet.PlanificationValidation)
            {
                TempData["Error"] = "La planification ne peut être validée qu'en phase Planification.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!projet.PlanningValideParDM)
            {
                TempData["Error"] = "La planification doit d'abord être validée par le Directeur Métier.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // VALIDATION DES LIVRABLES OBLIGATOIRES (PRD - Blocage automatique)
            var validationLivrables = await _livrableValidationService.ValiderLivrablesObligatoiresAsync(
                projet, PhaseProjet.ExecutionSuivi);

            if (!validationLivrables.EstValide)
            {
                TempData["Error"] = validationLivrables.MessageErreur;
                return RedirectToAction(nameof(Details), new { id, tab = "planification" });
            }

            projet.PlanningValideParDSI = true;
            projet.DatePlanningValideParDSI = DateTime.Now;

            // Passer à la phase Exécution
            projet.PhaseActuelle = PhaseProjet.ExecutionSuivi;
            projet.StatutProjet = StatutProjet.EnCours;
            if (!projet.DateDebut.HasValue)
                projet.DateDebut = DateTime.Now;

            // Enregistrer dans l'historique
            var historique = new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = PhaseProjet.ExecutionSuivi,
                StatutProjet = projet.StatutProjet,
                DateDebut = DateTime.Now,
                ModifieParId = userId,
                Commentaire = "Passage en phase Exécution après validation planification DSI",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };
            _db.HistoriquePhasesProjets.Add(historique);

            await RecalculateProjectProgressAsync(projet);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_PLANIF_DSI", "Projet", projet.Id);
            await _auditService.LogActionAsync("VALIDATION_PHASE_PLANIFICATION", "Projet", projet.Id,
                new { AnciennePhase = PhaseProjet.PlanificationValidation },
                new { NouvellePhase = PhaseProjet.ExecutionSuivi });

            TempData["Success"] = "Planification validée par la DSI. Le projet passe en phase Exécution.";
            return RedirectToAction(nameof(Details), new { id, tab = "execution" });
        }

        // GET: Écran de validation de charte pour DM et DSI
        [Authorize]
        public async Task<IActionResult> ValidationsProjet(int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var canPortfolioAccess = await HasPortfolioGovernanceAccessAsync();
            var hasDmScope = await CurrentUserHasPermissionAsync("Projet", "ValidationsProjet");
            var canValidateAsDsi = await CanValidateCharteAsDsiAsync(userId);

            if (!hasDmScope && !canValidateAsDsi && !canPortfolioAccess)
            {
                return Forbid();
            }

            IQueryable<Projet> query = _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Where(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification &&
                           (!p.CharteValideeParDM || !p.CharteValideeParDSI));

            if (hasDmScope && !canValidateAsDsi && !canPortfolioAccess)
            {
                var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
                if (currentUserDirectionId.HasValue)
                {
                    query = query.Where(p => p.SponsorId == userId || p.DirectionId == currentUserDirectionId.Value);
                }
                else
                {
                    query = query.Where(p => p.SponsorId == userId);
                }
            }

            // Pagination
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            query = query.OrderByDescending(p => p.DateCreation);

            var pagedResult = await query.ToPagedResultAsync(page, pageSize);

            var projets = pagedResult.Items;

            return View(new GestionProjects.Application.ViewModels.Projet.ValidationsProjetViewModel
            {
                Projets          = projets,
                CanValidateAsDsi = canValidateAsDsi,
                PageNumber       = pagedResult.PageNumber,
                TotalPages       = pagedResult.TotalPages,
                TotalCount       = pagedResult.TotalCount,
                PageSize         = pagedResult.PageSize,
            });
        }
    }
}
