using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
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
