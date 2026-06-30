using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Assistant scripté : aucune génération de texte libre, uniquement des règles
    /// déterministes appliquées aux données réelles du projet (livrables, validations,
    /// anomalies, charges). Pas de dépendance à un LLM externe.
    /// </summary>
    public class AssistantService : IAssistantService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILivrableValidationService _livrableValidation;

        public AssistantService(ApplicationDbContext db, ILivrableValidationService livrableValidation)
        {
            _db = db;
            _livrableValidation = livrableValidation;
        }

        public async Task<ProchainesEtapesResult?> ObtenirProchainesEtapesAsync(Guid projetId, Guid userId)
        {
            var projet = await _db.Projets
                .Include(p => p.Livrables)
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
            {
                return null;
            }

            var manquants = new List<string>();
            string prochaineAction;
            var estCloture = projet.StatutProjet == StatutProjet.Cloture;

            switch (projet.PhaseActuelle)
            {
                case PhaseProjet.AnalyseClarification:
                    AjouterLivrablesManquants(projet, PhaseProjet.PlanificationValidation, manquants);
                    if (projet.CharteProjet?.SignatureSponsor != true)
                        manquants.Add("Signature du Sponsor / Directeur Métier sur la charte signée");
                    if (projet.CharteProjet?.SignatureChefProjet != true)
                        manquants.Add("Signature du Chef de Projet sur la charte signée");
                    if (!projet.CharteValideeParDM)
                        manquants.Add("Validation de la charte par le Directeur Métier");
                    if (!projet.CharteValideeParDSI)
                        manquants.Add("Validation de la charte par la DSI");
                    prochaineAction = manquants.Count == 0
                        ? "Tout est prêt : validez la phase Analyse pour passer en Planification."
                        : "Complétez la charte puis faites-la valider par le DM et la DSI.";
                    break;

                case PhaseProjet.PlanificationValidation:
                    AjouterLivrablesManquants(projet, PhaseProjet.ExecutionSuivi, manquants);
                    if (!projet.PlanningValideParDM)
                        manquants.Add("Validation de la planification par le Directeur Métier");
                    if (!projet.PlanningValideParDSI)
                        manquants.Add("Validation de la planification par la DSI");
                    prochaineAction = manquants.Count == 0
                        ? "Tout est prêt : la planification peut être validée par la DSI pour passer en Exécution."
                        : "Complétez le planning, la RACI, la communication, le budget et le PV de kick-off, puis faites valider.";
                    break;

                case PhaseProjet.ExecutionSuivi:
                    AjouterLivrablesManquants(projet, PhaseProjet.UatMep, manquants);
                    prochaineAction = manquants.Count == 0
                        ? "Tout est prêt : cliquez sur \"Prêt pour UAT\" pour passer en phase UAT & MEP."
                        : "Déposez les comptes-rendus de réunion manquants avant de passer en UAT.";
                    break;

                case PhaseProjet.UatMep:
                    if (!projet.RecetteValidee)
                        manquants.Add("Validation de la recette par le Directeur Métier");
                    if (!projet.MepEffectuee)
                        manquants.Add("Enregistrement de la mise en production (MEP)");
                    if (projet.FicheProjet == null || !projet.FicheProjet.HypercareTermine ||
                        string.IsNullOrWhiteSpace(projet.FicheProjet.PeriodeHypercare) ||
                        string.IsNullOrWhiteSpace(projet.FicheProjet.StatutHypercare))
                        manquants.Add("Renseignement et clôture de la période d'hypercare");
                    AjouterLivrablesManquants(projet, PhaseProjet.ClotureLeconsApprises, manquants);
                    prochaineAction = manquants.Count == 0
                        ? "Tout est prêt : terminez l'UAT pour passer en phase Clôture."
                        : "Validez la recette, enregistrez la MEP et déposez les livrables de fin d'UAT manquants.";
                    break;

                case PhaseProjet.ClotureLeconsApprises:
                    if (estCloture)
                    {
                        prochaineAction = "Le projet est clôturé.";
                    }
                    else
                    {
                        var demandeCloture = await _db.DemandesClotureProjets
                            .Where(d => d.ProjetId == projetId && !d.EstTerminee)
                            .OrderByDescending(d => d.DateDemande)
                            .FirstOrDefaultAsync();

                        var aLivrableCloture = projet.Livrables.Any(l =>
                            !l.EstSupprime && l.Phase == PhaseProjet.ClotureLeconsApprises);

                        if (!aLivrableCloture)
                            manquants.Add("Dépôt d'au moins un livrable de clôture (rapport, PV ou dossier d'exploitation)");

                        var aAuMoinsUnBenefice = await _db.BeneficesProjets.AnyAsync(b => b.ProjetId == projetId && !b.EstSupprime);
                        if (!aAuMoinsUnBenefice)
                            manquants.Add("Définition d'au moins un bénéfice attendu (onglet Bénéfices)");

                        if (demandeCloture == null)
                        {
                            prochaineAction = "Complétez le bilan puis lancez la demande de clôture.";
                        }
                        else
                        {
                            if (demandeCloture.StatutValidationDemandeur != StatutValidationCloture.Validee)
                                manquants.Add("Votre propre validation de la demande de clôture");
                            if (demandeCloture.StatutValidationDirecteurMetier != StatutValidationCloture.Validee)
                                manquants.Add("Validation de la clôture par le Directeur Métier");
                            if (demandeCloture.StatutValidationDSI != StatutValidationCloture.Validee)
                                manquants.Add("Validation de la clôture par la DSI");
                            prochaineAction = manquants.Count == 0
                                ? "Toutes les validations sont faites, la clôture devrait être finalisée."
                                : "Suivez le circuit de validation de clôture (Demandeur → DM → DSI).";
                        }
                    }
                    break;

                default:
                    prochaineAction = "Aucune information disponible pour cette phase.";
                    break;
            }

            var alertes = await ObtenirAlertesComplementairesAsync(projet, userId);

            return new ProchainesEtapesResult
            {
                CodeProjet = projet.CodeProjet,
                Titre = projet.Titre,
                PhaseActuelle = projet.PhaseActuelle,
                PhaseLabel = projet.PhaseWorkflowLabel,
                OngletCible = OngletPourPhase(projet.PhaseActuelle),
                EstCloture = estCloture,
                ElementsManquants = manquants,
                ProchaineAction = prochaineAction,
                AlertesComplementaires = alertes
            };
        }

        /// <summary>
        /// Alertes non bloquantes (charges, bénéfices, avenant) : mêmes règles que
        /// RappelsAutomatiquesBackgroundService, mais calculées à la demande pour
        /// l'affichage immédiat dans le chat, sans attendre le cycle de notification.
        /// </summary>
        private async Task<List<string>> ObtenirAlertesComplementairesAsync(Projet projet, Guid userId)
        {
            var alertes = new List<string>();

            if (projet.StatutProjet != StatutProjet.EnCours)
            {
                return alertes;
            }

            var aujourdhui = DateTime.Today;
            var lundiSemaineCourante = aujourdhui.AddDays(-(int)aujourdhui.DayOfWeek + (int)DayOfWeek.Monday).Date;
            if (lundiSemaineCourante > aujourdhui)
            {
                lundiSemaineCourante = lundiSemaineCourante.AddDays(-7);
            }

            var estRessourceSuivie = await _db.ChargesProjets.AnyAsync(c =>
                c.ProjetId == projet.Id && c.RessourceId == userId && !c.EstSupprime);
            if (estRessourceSuivie)
            {
                var aDejaSaisi = await _db.ChargesProjets.AnyAsync(c =>
                    c.ProjetId == projet.Id && c.RessourceId == userId && !c.EstSupprime &&
                    c.SemaineDebut.Date == lundiSemaineCourante);
                if (!aDejaSaisi)
                {
                    alertes.Add($"Vous n'avez pas encore saisi votre charge de la semaine du {lundiSemaineCourante:dd/MM/yyyy}.");
                }
            }

            var beneficesProches = await _db.BeneficesProjets
                .Where(b => b.ProjetId == projet.Id && !b.EstSupprime &&
                            b.Statut == StatutBenefice.Attendu &&
                            b.DateCibleRealisation.HasValue &&
                            b.DateCibleRealisation.Value.Date <= aujourdhui.AddDays(14))
                .OrderBy(b => b.DateCibleRealisation)
                .Select(b => new { b.Libelle, b.DateCibleRealisation })
                .ToListAsync();

            foreach (var benefice in beneficesProches)
            {
                var enRetard = benefice.DateCibleRealisation!.Value.Date < aujourdhui;
                alertes.Add(enRetard
                    ? $"Bénéfice \"{benefice.Libelle}\" en retard d'évaluation (échéance {benefice.DateCibleRealisation:dd/MM/yyyy})."
                    : $"Bénéfice \"{benefice.Libelle}\" à évaluer prochainement (échéance {benefice.DateCibleRealisation:dd/MM/yyyy}).");
            }

            const decimal seuilEcartBudgetaire = 0.15m;
            const int seuilEcartJours = 15;
            var motifsAvenant = new List<string>();

            if (projet.FicheProjet?.BudgetPrevisionnel is decimal prevu && prevu > 0 &&
                projet.FicheProjet?.BudgetConsomme is decimal consomme)
            {
                var ecart = Math.Abs(consomme - prevu) / prevu;
                if (ecart > seuilEcartBudgetaire)
                {
                    motifsAvenant.Add($"écart budgétaire de {ecart:P0}");
                }
            }

            if (projet.EcartJoursDelai is int ecartJours && ecartJours > seuilEcartJours)
            {
                motifsAvenant.Add($"retard de {ecartJours} jour(s) par rapport à la baseline");
            }

            if (motifsAvenant.Count > 0)
            {
                var aUnAvenantEnCours = await _db.AvenantsProjets.AnyAsync(a =>
                    a.ProjetId == projet.Id && !a.EstSupprime &&
                    (a.Statut == StatutAvenant.EnAttenteValidationDM || a.Statut == StatutAvenant.EnAttenteValidationDSI));

                if (!aUnAvenantEnCours)
                {
                    alertes.Add($"Un avenant pourrait être nécessaire : {string.Join(" et ", motifsAvenant)}.");
                }
            }

            return alertes;
        }

        private static string OngletPourPhase(PhaseProjet phase) => phase switch
        {
            PhaseProjet.AnalyseClarification => "analyse",
            PhaseProjet.PlanificationValidation => "planification",
            PhaseProjet.ExecutionSuivi => "execution",
            PhaseProjet.UatMep => "uat",
            PhaseProjet.ClotureLeconsApprises => "cloture",
            _ => "synthese"
        };

        private void AjouterLivrablesManquants(Projet projet, PhaseProjet phaseCible, List<string> destination)
        {
            var obligatoires = _livrableValidation.GetLivrablesObligatoires(projet.PhaseActuelle, phaseCible);
            var deposes = projet.Livrables.Where(l => !l.EstSupprime).Select(l => l.TypeLivrable).ToHashSet();

            foreach (var type in obligatoires)
            {
                if (!deposes.Contains(type))
                {
                    destination.Add($"Livrable manquant : {LibelleLivrable(type)}");
                }
            }
        }

        private static string LibelleLivrable(TypeLivrable type) => type switch
        {
            TypeLivrable.CahierCharges => "Cahier des charges",
            TypeLivrable.CahierAnalyseTechnique => "Cahier d'analyse technique",
            TypeLivrable.CharteProjet => "Charte projet",
            TypeLivrable.CharteProjetSignee => "Charte projet signée",
            TypeLivrable.NoteCadrage => "Note de cadrage",
            TypeLivrable.Wbs => "WBS",
            TypeLivrable.PlanningDetaille => "Planning détaillé",
            TypeLivrable.MatriceRaci => "Matrice RACI",
            TypeLivrable.SchemaCommunication => "Schéma de communication",
            TypeLivrable.BudgetPrevisionnel => "Budget prévisionnel",
            TypeLivrable.PvKickOff => "PV de kick-off",
            TypeLivrable.CompteRenduReunion => "Compte-rendu de réunion",
            TypeLivrable.CahierTests => "Cahier de tests",
            TypeLivrable.FeuilleAnomalies => "Feuille d'anomalies",
            TypeLivrable.PvRecette => "PV de recette",
            TypeLivrable.DossierMep => "Dossier MEP",
            TypeLivrable.PvMep => "PV MEP",
            TypeLivrable.RapportHypercare => "Rapport hypercare",
            TypeLivrable.RapportCloture => "Rapport de clôture",
            TypeLivrable.PvCloture => "PV de clôture",
            TypeLivrable.DossierExploitation => "Dossier d'exploitation",
            _ => type.ToString()
        };

        public async Task<BrouillonBilanResult?> GenererBrouillonBilanAsync(Guid projetId, Guid userId)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.Anomalies)
                .Include(p => p.Charges)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
            {
                return null;
            }

            return new BrouillonBilanResult
            {
                BilanPerimetre = GenererBilanPerimetre(projet),
                BilanPlanning = GenererBilanPlanning(projet),
                BilanBudget = GenererBilanBudget(projet),
                BilanDifficultes = GenererBilanDifficultes(projet),
                BilanReussites = GenererBilanReussites(projet),
                LeconsReussites = GenererLeconsReussites(projet),
                LeconsEchecs = GenererLeconsEchecs(projet)
            };
        }

        private static string GenererBilanPerimetre(Projet projet)
        {
            var perimetre = projet.FicheProjet?.PerimetreInclus;
            return string.IsNullOrWhiteSpace(perimetre)
                ? $"Le périmètre livré correspond au périmètre cadré pour le projet \"{projet.Titre}\"."
                : $"Périmètre livré : {perimetre.Trim()}";
        }

        private static string GenererBilanPlanning(Projet projet)
        {
            if (!projet.DateFinPrevue.HasValue)
            {
                return "Aucune date de fin prévue n'a été renseignée, l'écart de planning ne peut pas être calculé automatiquement.";
            }

            var dateReference = projet.DateFinReelle ?? DateTime.Now;
            var ecartJours = (dateReference.Date - projet.DateFinPrevue.Value.Date).Days;
            var libelleStatut = projet.DateFinReelle.HasValue ? "Date de fin réelle" : "À ce jour";

            if (ecartJours <= 0)
            {
                return $"{libelleStatut} : projet livré dans les délais (fin prévue le {projet.DateFinPrevue:dd/MM/yyyy}).";
            }

            return $"{libelleStatut} : retard de {ecartJours} jour(s) par rapport à la date de fin prévue ({projet.DateFinPrevue:dd/MM/yyyy}).";
        }

        private static string GenererBilanBudget(Projet projet)
        {
            var fiche = projet.FicheProjet;
            if (fiche?.BudgetPrevisionnel is not decimal prevu || prevu == 0)
            {
                return "Aucun budget prévisionnel renseigné, l'écart budgétaire ne peut pas être calculé automatiquement.";
            }

            if (fiche.BudgetConsomme is not decimal consomme)
            {
                return $"Budget prévisionnel : {prevu:N0}. Budget consommé non renseigné.";
            }

            var ecartPourcentage = (consomme - prevu) / prevu * 100m;
            var sens = ecartPourcentage <= 0 ? "en deçà" : "au-delà";
            return $"Budget prévisionnel : {prevu:N0}, consommé : {consomme:N0} " +
                   $"({Math.Abs(ecartPourcentage):N1}% {sens} du prévisionnel).";
        }

        private static string GenererBilanDifficultes(Projet projet)
        {
            var anomalies = projet.Anomalies.Where(a => !a.EstSupprime).ToList();
            var critiques = anomalies.Count(a => a.Priorite == PrioriteAnomalie.Critique || a.Priorite == PrioriteAnomalie.Haute);

            var phrases = new List<string>();
            if (anomalies.Count > 0)
            {
                phrases.Add($"{anomalies.Count} anomalie(s) recensée(s) sur le projet, dont {critiques} de priorité haute ou critique.");
            }

            var ecartChargeImportant = projet.Charges
                .Where(c => c.ChargeReelle.HasValue && c.ChargePrevisionnelle > 0)
                .Where(c => Math.Abs(c.ChargeReelle!.Value - c.ChargePrevisionnelle) / c.ChargePrevisionnelle > 0.2m)
                .Any();
            if (ecartChargeImportant)
            {
                phrases.Add("Des écarts significatifs (> 20%) ont été constatés entre charge prévisionnelle et charge réelle sur certaines semaines.");
            }

            return phrases.Count == 0
                ? "Aucune difficulté majeure identifiée sur la base des anomalies et charges enregistrées."
                : string.Join(" ", phrases);
        }

        private static string GenererBilanReussites(Projet projet)
        {
            var anomalies = projet.Anomalies.Where(a => !a.EstSupprime).ToList();
            var resolues = anomalies.Count(a => a.Statut == StatutAnomalie.Corrigee || a.Statut == StatutAnomalie.Fermee);
            var phrases = new List<string> { $"Le projet \"{projet.Titre}\" a atteint la phase {projet.PhaseWorkflowLabel}." };

            if (anomalies.Count > 0)
            {
                phrases.Add($"{resolues}/{anomalies.Count} anomalie(s) résolue(s) avant clôture.");
            }

            return string.Join(" ", phrases);
        }

        private static string GenererLeconsReussites(Projet projet)
        {
            return projet.DateFinReelle.HasValue && projet.DateFinPrevue.HasValue &&
                   projet.DateFinReelle.Value.Date <= projet.DateFinPrevue.Value.Date
                ? "Le respect du planning initial a été un facteur clé de réussite."
                : "À compléter par le chef de projet en fonction du retour des parties prenantes.";
        }

        private static string GenererLeconsEchecs(Projet projet)
        {
            var anomalies = projet.Anomalies.Where(a => !a.EstSupprime &&
                (a.Priorite == PrioriteAnomalie.Critique || a.Priorite == PrioriteAnomalie.Haute)).ToList();

            return anomalies.Count > 0
                ? $"Anticiper davantage les risques techniques : {anomalies.Count} anomalie(s) de priorité haute/critique ont été détectées en cours de projet."
                : "À compléter par le chef de projet en fonction du retour des parties prenantes.";
        }

        public async Task<BrouillonAnalyseResult?> GenererBrouillonAnalyseAsync(Guid projetId, Guid userId)
        {
            var projet = await _db.Projets
                .Include(p => p.DemandeProjet)
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
            {
                return null;
            }

            var demande = projet.DemandeProjet;

            return new BrouillonAnalyseResult
            {
                NotesClarification = string.IsNullOrWhiteSpace(demande?.Contexte)
                    ? $"Besoin exprimé pour le projet \"{projet.Titre}\"."
                    : $"Contexte de la demande : {demande.Contexte.Trim()}",
                DecisionsPrises = string.IsNullOrWhiteSpace(demande?.Objectifs)
                    ? "Objectifs à clarifier avec le demandeur."
                    : $"Objectifs confirmés avec le demandeur : {demande.Objectifs.Trim()}",
                HypothesesProjet = string.IsNullOrWhiteSpace(projet.CharteProjet?.ContraintesInitiales)
                    ? "Aucune contrainte particulière identifiée à ce stade."
                    : $"Contraintes identifiées : {projet.CharteProjet.ContraintesInitiales.Trim()}"
            };
        }

        public async Task<BrouillonExecutionResult?> GenererBrouillonExecutionAsync(Guid projetId, Guid userId)
        {
            var projet = await _db.Projets
                .Include(p => p.TachesPlanning)
                .Include(p => p.Anomalies)
                .Include(p => p.Charges)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
            {
                return null;
            }

            var taches = projet.TachesPlanning.Where(t => !t.EstSupprime).ToList();
            var tachesTerminees = taches.Count(t => t.Avancement >= 100);
            var tachesEnCours = taches.Where(t => t.Avancement > 0 && t.Avancement < 100).ToList();
            var tachesNonDemarrees = taches.Where(t => t.Avancement == 0).ToList();

            var commentaireAvancement = taches.Count == 0
                ? $"Avancement global du projet : {projet.PourcentageAvancementAffiche}%."
                : $"Avancement global : {projet.PourcentageAvancementAffiche}% ({tachesTerminees}/{taches.Count} tâche(s) terminée(s)).";

            var actionsRealisees = tachesTerminees > 0
                ? string.Join(", ", taches.Where(t => t.Avancement >= 100).Take(5).Select(t => t.Libelle))
                : "Aucune tâche terminée pour le moment.";

            var actionsAVenir = tachesEnCours.Count + tachesNonDemarrees.Count > 0
                ? string.Join(", ", tachesEnCours.Concat(tachesNonDemarrees).Take(5).Select(t => t.Libelle))
                : "Aucune tâche restante identifiée.";

            var anomaliesOuvertes = projet.Anomalies.Count(a => !a.EstSupprime &&
                a.Statut != StatutAnomalie.Corrigee && a.Statut != StatutAnomalie.Fermee && a.Statut != StatutAnomalie.Rejetee);

            var problemes = anomaliesOuvertes > 0
                ? $"{anomaliesOuvertes} anomalie(s) encore ouverte(s) à traiter."
                : "Aucun blocage majeur identifié à ce jour.";

            return new BrouillonExecutionResult
            {
                CommentaireAvancementExecution = commentaireAvancement,
                ActionsRealiseesExecution = $"Tâches terminées : {actionsRealisees}",
                ActionsAVenirExecution = $"Tâches restantes : {actionsAVenir}",
                ProblemesBlocagesExecution = problemes
            };
        }
    }
}
