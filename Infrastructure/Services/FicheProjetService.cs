using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class FicheProjetService : IFicheProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public FicheProjetService(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Récupère la fiche du projet pour affichage, en la créant à la volée si absente,
        /// et synchronise les indicateurs de livrables obligatoires. Le projet doit être
        /// chargé avec Livrables, Membres, ChefProjet et DemandeProjet.
        /// </summary>
        public async Task<FicheProjet> ObtenirPourAffichageAsync(Projet projet)
        {
            var fiche = await _db.FicheProjets
                .Include(f => f.DerniereMiseAJourPar)
                .FirstOrDefaultAsync(f => f.ProjetId == projet.Id && !f.EstSupprime);

            if (fiche == null)
            {
                fiche = new FicheProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    TitreCourt = projet.Titre,
                    TitreLong = projet.Titre,
                    ObjectifPrincipal = projet.Objectif ?? projet.DemandeProjet?.Objectifs ?? string.Empty,
                    ContexteProblemeAdresse = projet.DemandeProjet?.Contexte ?? string.Empty,
                    DescriptionSynthetique = projet.DemandeProjet?.Description ?? string.Empty,
                    ResultatsAttendus = projet.DemandeProjet?.AvantagesAttendus ?? string.Empty,
                    CriticiteUrgence = $"{projet.DemandeProjet?.Criticite} / {projet.DemandeProjet?.Urgence}",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };

                SynchroniserLivrablesObligatoires(fiche, projet);

                var equipeProjet = new List<string>();
                if (projet.ChefProjet != null)
                    equipeProjet.Add($"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms} - Chef de Projet");
                foreach (var membre in projet.Membres?.Where(m => !m.EstSupprime) ?? Enumerable.Empty<MembreProjet>())
                {
                    equipeProjet.Add($"{membre.Nom} {membre.Prenom} - {membre.RoleDansProjet}");
                }
                fiche.EquipeProjet = string.Join("\n", equipeProjet);

                _db.FicheProjets.Add(fiche);
                await _db.SaveChangesAsync();
            }
            else
            {
                SynchroniserLivrablesObligatoires(fiche, projet);
                await _db.SaveChangesAsync();
            }

            return fiche;
        }

        private static void SynchroniserLivrablesObligatoires(FicheProjet fiche, Projet projet)
        {
            fiche.CharteProjetPresente = projet.Livrables?.Any(l => l.TypeLivrable == TypeLivrable.CharteProjet) ?? false;
            fiche.WBSPlanningRACIBudgetPresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.PlanificationValidation &&
                (l.TypeLivrable == TypeLivrable.Wbs || l.TypeLivrable == TypeLivrable.PlanningDetaille || l.TypeLivrable == TypeLivrable.MatriceRaci || l.TypeLivrable == TypeLivrable.BudgetPrevisionnel)) ?? false;
            fiche.CRReunionsPresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.ExecutionSuivi && l.TypeLivrable == TypeLivrable.CompteRenduReunion) ?? false;
            fiche.CahierTestsPVRecettePVMEPPresent = projet.Livrables?.Any(l => (l.Phase == PhaseProjet.UatMep || l.Phase == PhaseProjet.ClotureLeconsApprises) &&
                (l.TypeLivrable == TypeLivrable.CahierTests || l.TypeLivrable == TypeLivrable.PvRecette || l.TypeLivrable == TypeLivrable.PvMep)) ?? false;
            fiche.RapportLeconsApprisesPVCloturePresent = projet.Livrables?.Any(l => l.Phase == PhaseProjet.ClotureLeconsApprises &&
                (l.TypeLivrable == TypeLivrable.RapportCloture || l.TypeLivrable == TypeLivrable.PvCloture)) ?? false;
        }

        public async Task<WorkflowResult> SauvegarderAsync(Guid projetId, FicheProjet fiche, Guid userId)
        {
            var projet = await _db.Projets.FindAsync(projetId);
            if (projet == null)
                return WorkflowResult.NotFound();

            var ficheExistante = await _db.FicheProjets
                .FirstOrDefaultAsync(f => f.ProjetId == projetId && !f.EstSupprime);

            if (ficheExistante == null)
            {
                fiche.Id = Guid.NewGuid();
                fiche.ProjetId = projetId;
                fiche.DateCreation = DateTime.Now;
                fiche.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                fiche.EstSupprime = false;
                fiche.DateDerniereMiseAJour = DateTime.Now;
                fiche.DerniereMiseAJourParId = userId;
                _db.FicheProjets.Add(fiche);
            }
            else
            {
                ficheExistante.TitreCourt = fiche.TitreCourt;
                ficheExistante.TitreLong = fiche.TitreLong;
                ficheExistante.ObjectifPrincipal = fiche.ObjectifPrincipal;
                ficheExistante.ContexteProblemeAdresse = fiche.ContexteProblemeAdresse;
                ficheExistante.DescriptionSynthetique = fiche.DescriptionSynthetique;
                ficheExistante.ResultatsAttendus = fiche.ResultatsAttendus;
                ficheExistante.PerimetreInclus = fiche.PerimetreInclus;
                ficheExistante.PerimetreExclu = fiche.PerimetreExclu;
                ficheExistante.BeneficesAttendus = fiche.BeneficesAttendus;
                ficheExistante.CriticiteUrgence = fiche.CriticiteUrgence;
                ficheExistante.TypeProjet = fiche.TypeProjet;
                ficheExistante.ProchainJalon = fiche.ProchainJalon;
                ficheExistante.JalonsPrincipaux = fiche.JalonsPrincipaux;
                ficheExistante.DecoupageLotsTravail = fiche.DecoupageLotsTravail;
                ficheExistante.PlanificationRessources = fiche.PlanificationRessources;
                ficheExistante.RaciParActivite = fiche.RaciParActivite;
                ficheExistante.FrequenceReunions = fiche.FrequenceReunions;
                ficheExistante.ParticipantsReunions = fiche.ParticipantsReunions;
                ficheExistante.CanalCommunication = fiche.CanalCommunication;
                ficheExistante.CopilPrevu = fiche.CopilPrevu;
                ficheExistante.CommentaireBudgetPlanification = fiche.CommentaireBudgetPlanification;
                ficheExistante.CommentaireValidationPlanification = fiche.CommentaireValidationPlanification;
                ficheExistante.SyntheseRisques = fiche.SyntheseRisques;
                ficheExistante.EquipeProjet = fiche.EquipeProjet;
                ficheExistante.PartiesPrenantesCles = fiche.PartiesPrenantesCles;
                ficheExistante.BudgetPrevisionnel = fiche.BudgetPrevisionnel;
                ficheExistante.BudgetConsomme = fiche.BudgetConsomme;
                ficheExistante.EcartsBudget = ficheExistante.BudgetPrevisionnel.HasValue && ficheExistante.BudgetConsomme.HasValue
                    ? ficheExistante.BudgetConsomme.Value - ficheExistante.BudgetPrevisionnel.Value
                    : null;

                // Validation : justification obligatoire si écart > 10%
                if (ficheExistante.BudgetPrevisionnel.HasValue && ficheExistante.BudgetConsomme.HasValue && ficheExistante.BudgetPrevisionnel.Value > 0)
                {
                    var ecartPourcentage = Math.Abs((ficheExistante.BudgetConsomme.Value - ficheExistante.BudgetPrevisionnel.Value) / ficheExistante.BudgetPrevisionnel.Value * 100);
                    if (ecartPourcentage > 10 && string.IsNullOrWhiteSpace(fiche.JustificationEcartBudget))
                    {
                        return WorkflowResult.Error($"Un écart de {ecartPourcentage:F1}% a été détecté. Une justification est obligatoire pour les écarts supérieurs à 10%.");
                    }

                    if (ecartPourcentage > 10 && !string.IsNullOrWhiteSpace(fiche.JustificationEcartBudget))
                    {
                        ficheExistante.JustificationEcartBudget = fiche.JustificationEcartBudget;
                        ficheExistante.DateJustificationEcart = DateTime.Now;
                        ficheExistante.JustificationParId = userId;
                    }
                }
                ficheExistante.DateDebutReelleExecution = fiche.DateDebutReelleExecution;
                ficheExistante.DateFinEstimeeExecution = fiche.DateFinEstimeeExecution;
                ficheExistante.JustificationRetardExecution = fiche.JustificationRetardExecution;
                ficheExistante.CommentaireAvancementExecution = fiche.CommentaireAvancementExecution;
                ficheExistante.ActionsRealiseesExecution = fiche.ActionsRealiseesExecution;
                ficheExistante.ActionsAVenirExecution = fiche.ActionsAVenirExecution;
                ficheExistante.ProblemesBlocagesExecution = fiche.ProblemesBlocagesExecution;
                ficheExistante.JustificationBudgetExecution = fiche.JustificationBudgetExecution;
                ficheExistante.SyntheseChargesExecution = fiche.SyntheseChargesExecution;
                ficheExistante.DecisionsExecution = fiche.DecisionsExecution;
                ficheExistante.DateDebutRecette = fiche.DateDebutRecette;
                ficheExistante.DateFinRecette = fiche.DateFinRecette;
                ficheExistante.UtilisateursTesteurs = fiche.UtilisateursTesteurs;
                ficheExistante.PerimetreTeste = fiche.PerimetreTeste;
                ficheExistante.DateMepPrevue = fiche.DateMepPrevue;
                ficheExistante.PrerequisMep = fiche.PrerequisMep;
                ficheExistante.PlanMep = fiche.PlanMep;
                ficheExistante.PlanRollback = fiche.PlanRollback;
                ficheExistante.ChangeRequis = fiche.ChangeRequis;
                ficheExistante.ReferenceChange = fiche.ReferenceChange;
                ficheExistante.StatutValidationChange = fiche.StatutValidationChange;
                ficheExistante.ResultatMep = fiche.ResultatMep;
                ficheExistante.IncidentsMep = fiche.IncidentsMep;
                ficheExistante.PeriodeHypercare = fiche.PeriodeHypercare;
                ficheExistante.IncidentsPostMep = fiche.IncidentsPostMep;
                ficheExistante.StatutHypercare = fiche.StatutHypercare;
                ficheExistante.HypercareTermine = fiche.HypercareTermine;
                ficheExistante.TransfertRunDocumentation = fiche.TransfertRunDocumentation;
                ficheExistante.TransfertRunAcces = fiche.TransfertRunAcces;
                ficheExistante.TransfertRunSupportInforme = fiche.TransfertRunSupportInforme;
                ficheExistante.TransfertRunExploitationPrete = fiche.TransfertRunExploitationPrete;
                ficheExistante.StatutFinalCloture = fiche.StatutFinalCloture;
                ficheExistante.CommentaireStatutFinal = fiche.CommentaireStatutFinal;
                ficheExistante.PointsForts = fiche.PointsForts;
                ficheExistante.PointsVigilance = fiche.PointsVigilance;
                ficheExistante.DecisionsAttendues = fiche.DecisionsAttendues;
                ficheExistante.DemandesArbitrage = fiche.DemandesArbitrage;
                ficheExistante.DateDerniereMiseAJour = DateTime.Now;
                ficheExistante.DerniereMiseAJourParId = userId;
                ficheExistante.DateModification = DateTime.Now;
                ficheExistante.ModifiePar = _currentUserService.Matricule;
            }

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SAUVEGARDE_FICHE_PROJET", "FicheProjet", ficheExistante?.Id ?? fiche.Id);

            return WorkflowResult.Success("Fiche projet sauvegardée avec succès.");
        }
    }
}
