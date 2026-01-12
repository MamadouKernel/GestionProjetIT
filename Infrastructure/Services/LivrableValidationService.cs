using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Service de validation des livrables obligatoires selon le PRD
    /// Bloque automatiquement le changement de phase si livrables manquants
    /// </summary>
    public class LivrableValidationService : ILivrableValidationService
    {
        private readonly ApplicationDbContext _context;

        public LivrableValidationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<LivrableValidationResult> ValiderLivrablesObligatoiresAsync(Projet projet, PhaseProjet phaseCible)
        {
            var result = new LivrableValidationResult();

            // Récupérer les livrables obligatoires pour cette transition
            var livrablesObligatoires = GetLivrablesObligatoires(projet.PhaseActuelle, phaseCible);

            if (livrablesObligatoires.Count == 0)
            {
                // Aucun livrable obligatoire pour cette transition
                result.EstValide = true;
                return result;
            }

            // Charger les livrables existants du projet
            var livrablesExistants = await _context.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .Select(l => l.TypeLivrable)
                .ToListAsync();

            // Vérifier quels livrables obligatoires sont manquants
            var livrablesManquants = livrablesObligatoires
                .Where(l => !livrablesExistants.Contains(l))
                .ToList();

            result.LivrablesManquants = livrablesManquants;
            result.EstValide = livrablesManquants.Count == 0;

            if (!result.EstValide)
            {
                var nomsLivrables = livrablesManquants.Select(GetNomLivrable).ToList();
                var listeLivrables = string.Join(", ", nomsLivrables);
                result.MessageErreur = $"⛔ Blocage automatique : Impossible de passer en phase {GetNomPhase(phaseCible)}. " +
                    $"Livrables obligatoires manquants ({livrablesManquants.Count}) : <strong>{listeLivrables}</strong>. " +
                    $"Veuillez déposer ces documents dans l'onglet \"Livrables\" du projet avant de pouvoir changer de phase.";
            }

            return result;
        }

        public List<TypeLivrable> GetLivrablesObligatoires(PhaseProjet phaseActuelle, PhaseProjet phaseCible)
        {
            // Définition des livrables obligatoires selon le PRD et les bonnes pratiques DSI

            // Transition : Analyse & Clarification → Planification
            if (phaseActuelle == PhaseProjet.AnalyseClarification && phaseCible == PhaseProjet.PlanificationValidation)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.CharteProjet // Déjà vérifié séparément, mais on l'inclut pour cohérence
                };
            }

            // Transition : Planification → Exécution
            if (phaseActuelle == PhaseProjet.PlanificationValidation && phaseCible == PhaseProjet.ExecutionSuivi)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.Wbs,                    // WBS (Work Breakdown Structure)
                    TypeLivrable.PlanningDetaille,       // Planning détaillé
                    TypeLivrable.MatriceRaci,             // Matrice RACI
                    TypeLivrable.BudgetPrevisionnel       // Budget prévisionnel
                };
            }

            // Transition : Exécution → UAT & MEP
            if (phaseActuelle == PhaseProjet.ExecutionSuivi && phaseCible == PhaseProjet.UatMep)
            {
                // Au moins un compte-rendu de réunion est requis (suivi opérationnel)
                // On ne bloque pas si aucun CR, mais on recommande fortement
                // Pour l'instant, on ne bloque pas cette transition sur les CR
                // Mais on pourrait ajouter une validation optionnelle
                return new List<TypeLivrable>();
            }

            // Transition : UAT & MEP → Clôture
            if (phaseActuelle == PhaseProjet.UatMep && phaseCible == PhaseProjet.ClotureLeconsApprises)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.CahierTests,    // Cahier de tests
                    TypeLivrable.PvRecette,      // PV de recette
                    TypeLivrable.PvMep           // PV de mise en production
                };
            }

            // Autres transitions : pas de livrables obligatoires spécifiques
            return new List<TypeLivrable>();
        }

        private string GetNomLivrable(TypeLivrable type)
        {
            return type switch
            {
                TypeLivrable.CahierCharges => "Cahier des charges",
                TypeLivrable.CahierAnalyseTechnique => "Cahier d'analyse technique",
                TypeLivrable.CharteProjet => "Charte de projet",
                TypeLivrable.NoteCadrage => "Note de cadrage",
                TypeLivrable.Wbs => "WBS (Work Breakdown Structure)",
                TypeLivrable.PlanningDetaille => "Planning détaillé",
                TypeLivrable.MatriceRaci => "Matrice RACI",
                TypeLivrable.SchemaCommunication => "Schéma de communication",
                TypeLivrable.BudgetPrevisionnel => "Budget prévisionnel",
                TypeLivrable.PvKickOff => "PV de Kick-off",
                TypeLivrable.CahierTests => "Cahier de tests",
                TypeLivrable.FeuilleAnomalies => "Feuille d'anomalies",
                TypeLivrable.PvRecette => "PV de recette",
                TypeLivrable.RapportHypercare => "Rapport d'hypercare",
                TypeLivrable.DossierMep => "Dossier MEP",
                TypeLivrable.PvMep => "PV de mise en production",
                TypeLivrable.RapportCloture => "Rapport de clôture",
                TypeLivrable.PvCloture => "PV de clôture",
                TypeLivrable.DossierExploitation => "Dossier d'exploitation",
                TypeLivrable.CompteRenduReunion => "Compte-rendu de réunion",
                TypeLivrable.Autre => "Autre",
                _ => type.ToString()
            };
        }

        private string GetNomPhase(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.Demande => "Demande",
                PhaseProjet.AnalyseClarification => "Analyse & Clarification",
                PhaseProjet.PlanificationValidation => "Planification & Validation",
                PhaseProjet.ExecutionSuivi => "Exécution & Suivi",
                PhaseProjet.UatMep => "UAT & MEP",
                PhaseProjet.ClotureLeconsApprises => "Clôture & Leçons apprises",
                _ => phase.ToString()
            };
        }
    }
}

