using GestionProjects.Application.Common.Extensions;
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
        public async Task<IActionResult> Details(Guid id, string? tab = "synthese")
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

            var isReadOnly = ui.HasDmGovernanceAccess && !ui.IsProjectSponsor;

            await RecalculateProjectProgressAsync(projet, persistChanges: true);

            List<CasTestProjet> casTests = new();
            List<CampagneTestProjet> campagnes = new();
            CollaborationProjet? collaboration = null;
            List<DossierSignatureProjet> dossiersSignature = new();
            IEnumerable<AuditLog> auditLogs = Enumerable.Empty<AuditLog>();
            List<Utilisateur> chefsProjet = new();

            if (tab == "uat")
            {
                casTests = await _db.CasTestsProjets
                    .Include(c => c.Executions.OrderByDescending(e => e.DateExecution))
                    .Include(c => c.CampagneTestProjet)
                    .Where(c => c.ProjetId == id && !c.EstSupprime)
                    .OrderBy(c => c.Reference)
                    .ToListAsync();

                campagnes = await _db.CampagnesTestsProjets
                    .Where(c => c.ProjetId == id && !c.EstSupprime)
                    .OrderByDescending(c => c.DateLancement)
                    .ToListAsync();
            }

            if (tab == "collaboration" || tab == "execution")
            {
                collaboration = await _db.CollaborationsProjets
                    .Include(c => c.Taches.OrderBy(t => t.Phase))
                    .FirstOrDefaultAsync(c => c.ProjetId == id && !c.EstSupprime);
            }

            if (tab == "planification")
            {
                dossiersSignature = await _db.DossiersSignatureProjets
                    .Include(d => d.Signataires.OrderBy(s => s.OrdreSignature))
                        .ThenInclude(s => s.Utilisateur)
                    .Where(d => d.ProjetId == id && !d.EstSupprime)
                    .OrderByDescending(d => d.DateCreation)
                    .ToListAsync();
            }

            if (tab == "historique")
            {
                auditLogs = await _db.AuditLogs
                    .Include(a => a.Utilisateur)
                    .Where(a => a.Entite == "Projet" && a.EntiteId == id.ToString())
                    .OrderByDescending(a => a.DateAction)
                    .ToListAsync();
            }

            if (ui.CanReassignChefProjet)
            {
                var chefsProjetBase = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToListAsync();

                var delegationsActives = await _db.DelegationsChefProjet
                    .Include(d => d.Delegue)
                    .Where(d => !d.EstSupprime &&
                                d.EstActive &&
                                d.ProjetId == id &&
                                d.DateDebut <= DateTime.Now &&
                                d.DateFin == null)
                    .Select(d => d.Delegue!)
                    .Where(u => !u.EstSupprime)
                    .ToListAsync();

                chefsProjet = chefsProjetBase
                    .Union(delegationsActives)
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToList();
            }

            if (ui.IsAssignedChefProjet)
            {
                var priseEnChargeExiste = await _db.AuditLogs
                    .AnyAsync(a => a.Entite == "Projet" &&
                                  a.EntiteId == id.ToString() &&
                                  a.TypeAction == "PRISE_EN_CHARGE_PROJET" &&
                                  a.UtilisateurId == userId);

                if (!priseEnChargeExiste)
                {
                    await _auditService.LogActionAsync("PRISE_EN_CHARGE_PROJET", "Projet", projet.Id,
                        null,
                        new { ChefProjetId = userId, CodeProjet = projet.CodeProjet });
                }
            }

            var isDemandeurProject = projet.DemandeProjet?.DemandeurId == userId;
            var canAccessCharges = ui.CanOpenChargesTab;

            var vm = new ProjetDetailsViewModel
            {
                Projet             = projet,
                ActiveTab          = tab ?? "synthese",
                IsReadOnly         = isReadOnly,
                IsDemandeurProject = isDemandeurProject,
                CanAccessCharges   = canAccessCharges,
                CasTests           = casTests,
                Campagnes          = campagnes,
                Collaboration      = collaboration,
                DossiersSignature  = dossiersSignature,
                AuditLogs          = auditLogs,
                ChefsProjet        = chefsProjet,
            };

            return View(vm);
        }

        // POST: Mettre à jour le ResponsableSolutionsIT (ChefProjet)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateChefProjet(Guid id, Guid? chefProjetId)
        {
            var projet = await _db.Projets
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (!await HasPortfolioGovernanceAccessAsync())
            {
                return Forbid();
            }

            // Vérifier que le projet n'est pas clôturé
            if (projet.StatutProjet == StatutProjet.Cloture)
            {
                TempData["Error"] = "Impossible de modifier le ResponsableSolutionsIT d'un projet clôturé.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ancienChefProjetId = projet.ChefProjetId;
            var ancienChefProjetNom = projet.ChefProjet != null ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}" : "Aucun";

            // Si un chef de projet est spécifié, vérifier qu'il existe et est valide
            if (chefProjetId.HasValue)
            {
                var chefProjet = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles.Where(ur => !ur.EstSupprime))
                    .FirstOrDefaultAsync(u => u.Id == chefProjetId.Value && !u.EstSupprime);

                if (chefProjet == null)
                {
                    TempData["Error"] = "Le ResponsableSolutionsIT sélectionné n'existe pas.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Vérifier que l'utilisateur a le rôle ChefDeProjet OU a une délégation active pour ce projet
                bool isValidChefProjet = chefProjet.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet);

                if (!isValidChefProjet)
                {
                    // Vérifier s'il a une délégation active pour ce projet
                    var delegationActive = await _db.DelegationsChefProjet
                        .AnyAsync(d => d.DelegueId == chefProjetId.Value &&
                                      d.ProjetId == id &&
                                      d.EstActive &&
                                      d.DateFin == null &&
                                      !d.EstSupprime);

                    if (!delegationActive)
                    {
                        TempData["Error"] = "Le ResponsableSolutionsIT sélectionné n'est pas valide (doit être ChefDeProjet ou avoir une délégation active pour ce projet).";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }
            }

            // Si l'ancien chef de projet change, enregistrer la fin dans l'historique
            if (ancienChefProjetId.HasValue && ancienChefProjetId != chefProjetId)
            {
                var historiqueAncienChef = await _db.HistoriqueChefProjets
                    .Where(h => h.ProjetId == id && h.ChefProjetId == ancienChefProjetId.Value && h.DateFin == null)
                    .FirstOrDefaultAsync();

                if (historiqueAncienChef != null)
                {
                    historiqueAncienChef.DateFin = DateTime.Now;
                    historiqueAncienChef.DateModification = DateTime.Now;
                    historiqueAncienChef.ModifiePar = _currentUserService.Matricule;
                }
            }

            projet.ChefProjetId = chefProjetId;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule ?? "SYSTEM";

            await _db.SaveChangesAsync();

            // Si un nouveau chef de projet est assigné, créer une entrée dans l'historique
            if (chefProjetId.HasValue && ancienChefProjetId != chefProjetId)
            {
                var historiqueChefProjet = new HistoriqueChefProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    ChefProjetId = chefProjetId.Value,
                    DateDebut = DateTime.Now,
                    DateFin = null,
                    Commentaire = "Assignation comme chef de projet",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                };

                _db.HistoriqueChefProjets.Add(historiqueChefProjet);
                await _db.SaveChangesAsync();
            }

            var nouveauChefProjetNom = chefProjetId.HasValue
                ? await _db.Utilisateurs
                    .Where(u => u.Id == chefProjetId.Value)
                    .Select(u => $"{u.Nom} {u.Prenoms}")
                    .FirstOrDefaultAsync()
                : "Aucun";

            await _auditService.LogActionAsync("UPDATE_CHEFPROJET", "Projet", projet.Id,
                new { AncienChefProjetId = ancienChefProjetId, AncienChefProjet = ancienChefProjetNom },
                new { NouveauChefProjetId = chefProjetId, NouveauChefProjet = nouveauChefProjetNom });

            TempData["Success"] = chefProjetId.HasValue
                ? $"ResponsableSolutionsIT mis à jour : {nouveauChefProjetNom}"
                : "ResponsableSolutionsIT retiré du projet.";

            return RedirectToAction(nameof(Details), new { id });
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
        public async Task<IActionResult> AjouterMembre(Guid projetId, Guid utilisateurId, string roleDansProjet)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return NotFound();

            if (!await CanManageProjectMembersAsync(projet))
                return Forbid();

            var utilisateur = await _db.Utilisateurs.FindAsync(utilisateurId);
            if (utilisateur == null)
            {
                TempData["Error"] = "Utilisateur non trouvé.";
                return RedirectToAction(nameof(Details), new { id = projetId, tab = "analyse" });
            }

            var membre = new MembreProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Nom = utilisateur.Nom,
                Prenom = utilisateur.Prenoms,
                RoleDansProjet = roleDansProjet,
                Email = utilisateur.Email,
                DirectionLibelle = utilisateur.Direction?.Libelle ?? string.Empty,
                EstActif = true,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };

            _db.MembresProjets.Add(membre);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("AJOUT_MEMBRE", "MembreProjet", membre.Id);

            TempData["Success"] = "Membre ajouté au projet.";
            return RedirectToAction(nameof(Details), new { id = projetId, tab = "analyse" });
        }

        // POST: Retirer/Désactiver un membre du projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RetirerMembre(Guid id, Guid membreId)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanManageProjectMembersAsync(projet))
                return Forbid();

            var membre = await _db.MembresProjets.FindAsync(membreId);
            if (membre == null || membre.ProjetId != id)
                return NotFound();

            // Soft delete : marquer comme supprimé
            membre.EstActif = false;
            membre.EstSupprime = true;
            membre.DateModification = DateTime.Now;
            membre.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("RETRAIT_MEMBRE_PROJET", "MembreProjet", membre.Id,
                new { ProjetId = id, MembreNom = $"{membre.Nom} {membre.Prenom}" },
                new { Action = "Retiré/Désactivé" });

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

            await RecalculateProjectProgressAsync(projet);

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
