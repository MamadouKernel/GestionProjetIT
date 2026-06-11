using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Validation des livrables obligatoires avant changement de phase.
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
            var livrablesObligatoires = GetLivrablesObligatoires(projet.PhaseActuelle, phaseCible);

            if (livrablesObligatoires.Count == 0)
            {
                result.EstValide = true;
                return result;
            }

            var livrablesExistants = await _context.LivrablesProjets
                .Where(l => l.ProjetId == projet.Id && !l.EstSupprime)
                .Select(l => l.TypeLivrable)
                .ToListAsync();

            var livrablesManquants = livrablesObligatoires
                .Where(l => !livrablesExistants.Contains(l))
                .ToList();

            result.LivrablesManquants = livrablesManquants;
            result.EstValide = livrablesManquants.Count == 0;

            if (!result.EstValide)
            {
                var nomsLivrables = livrablesManquants.Select(GetNomLivrable).ToList();
                result.MessageErreur =
                    $"Blocage automatique : impossible de passer en phase {GetNomPhase(phaseCible)}. " +
                    $"Livrables obligatoires manquants ({livrablesManquants.Count}) : " +
                    $"<strong>{string.Join(", ", nomsLivrables)}</strong>.";
            }

            return result;
        }

        public List<TypeLivrable> GetLivrablesObligatoires(PhaseProjet phaseActuelle, PhaseProjet phaseCible)
        {
            if (phaseActuelle == PhaseProjet.AnalyseClarification && phaseCible == PhaseProjet.PlanificationValidation)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.CharteProjet,
                    TypeLivrable.CharteProjetSignee
                };
            }

            if (phaseActuelle == PhaseProjet.PlanificationValidation && phaseCible == PhaseProjet.ExecutionSuivi)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.Wbs,
                    TypeLivrable.PlanningDetaille,
                    TypeLivrable.MatriceRaci,
                    TypeLivrable.SchemaCommunication,
                    TypeLivrable.BudgetPrevisionnel,
                    TypeLivrable.PvKickOff
                };
            }

            if (phaseActuelle == PhaseProjet.ExecutionSuivi && phaseCible == PhaseProjet.UatMep)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.CompteRenduReunion
                };
            }

            if (phaseActuelle == PhaseProjet.UatMep && phaseCible == PhaseProjet.ClotureLeconsApprises)
            {
                return new List<TypeLivrable>
                {
                    TypeLivrable.CahierTests,
                    TypeLivrable.FeuilleAnomalies,
                    TypeLivrable.PvRecette,
                    TypeLivrable.DossierMep,
                    TypeLivrable.PvMep,
                    TypeLivrable.RapportHypercare
                };
            }

            return new List<TypeLivrable>();
        }

        private static string GetNomLivrable(TypeLivrable type)
        {
            return type switch
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
                TypeLivrable.CahierTests => "Cahier de tests",
                TypeLivrable.FeuilleAnomalies => "Feuille d'anomalies",
                TypeLivrable.PvRecette => "PV de recette",
                TypeLivrable.RapportHypercare => "Rapport hypercare",
                TypeLivrable.DossierMep => "Dossier MEP",
                TypeLivrable.PvMep => "PV MEP",
                TypeLivrable.RapportCloture => "Rapport de clôture",
                TypeLivrable.PvCloture => "PV de clôture",
                TypeLivrable.DossierExploitation => "Dossier d'exploitation",
                TypeLivrable.CompteRenduReunion => "Compte-rendu de réunion",
                TypeLivrable.Devis => "Devis",
                TypeLivrable.BonCommande => "Bon de commande",
                TypeLivrable.Facture => "Facture",
                TypeLivrable.MemoInterne => "Mémo interne",
                _ => type.ToString()
            };
        }

        private static string GetNomPhase(PhaseProjet phase)
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
