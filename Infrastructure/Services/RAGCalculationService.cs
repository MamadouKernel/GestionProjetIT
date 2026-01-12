using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Service de calcul automatique de l'indicateur RAG selon le PRD
    /// Logique : Budget, Planning, Risques, Livrables
    /// </summary>
    public class RAGCalculationService : IRAGCalculationService
    {
        private readonly ApplicationDbContext _context;

        public RAGCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IndicateurRAG> CalculerRAGAsync(Projet projet)
        {
            // Charger les données nécessaires
            var projetComplet = await _context.Projets
                .Include(p => p.FicheProjet)
                .Include(p => p.Risques)
                .Include(p => p.Livrables)
                .Include(p => p.Anomalies)
                .FirstOrDefaultAsync(p => p.Id == projet.Id);

            if (projetComplet == null)
                return IndicateurRAG.Vert;

            int scoreRouge = 0;
            int scoreAmber = 0;

            // 1. Vérification Budget (écarts > 10% = Amber, > 20% = Rouge)
            if (projetComplet.FicheProjet != null)
            {
                var budgetPrevisionnel = projetComplet.FicheProjet.BudgetPrevisionnel ?? 0;
                var budgetConsomme = projetComplet.FicheProjet.BudgetConsomme ?? 0;
                
                if (budgetPrevisionnel > 0)
                {
                    var ecartPourcentage = Math.Abs((budgetConsomme - budgetPrevisionnel) / budgetPrevisionnel * 100);
                    
                    if (ecartPourcentage > 20)
                        scoreRouge += 2; // Écart critique
                    else if (ecartPourcentage > 10)
                        scoreAmber += 1; // Écart significatif
                }
            }

            // 2. Vérification Planning (retard > 20% = Rouge, > 10% = Amber)
            if (projetComplet.DateDebut.HasValue && projetComplet.DateFinPrevue.HasValue)
            {
                var dureeTotale = (projetComplet.DateFinPrevue.Value - projetComplet.DateDebut.Value).TotalDays;
                var joursEcoules = (DateTime.Now - projetComplet.DateDebut.Value).TotalDays;
                
                if (dureeTotale > 0 && joursEcoules > 0)
                {
                    var pourcentageTempsEcoule = (joursEcoules / dureeTotale) * 100;
                    var pourcentageAvancement = projetComplet.PourcentageAvancement;
                    
                    // Si on a consommé plus de temps que prévu par rapport à l'avancement
                    var retard = pourcentageTempsEcoule - pourcentageAvancement;
                    
                    if (retard > 20)
                        scoreRouge += 2; // Retard critique
                    else if (retard > 10)
                        scoreAmber += 1; // Retard significatif
                }
            }

            // 3. Vérification Risques (risques critiques = Rouge, risques élevés = Amber)
            var risquesCritiques = projetComplet.Risques
                .Where(r => !r.EstSupprime && 
                           r.Impact == ImpactRisque.Critique && 
                           (r.Probabilite == ProbabiliteRisque.Elevee || r.Probabilite == ProbabiliteRisque.Moyenne))
                .Count();
            
            var risquesEleves = projetComplet.Risques
                .Where(r => !r.EstSupprime && 
                           ((r.Impact == ImpactRisque.Eleve && r.Probabilite == ProbabiliteRisque.Elevee) ||
                            (r.Impact == ImpactRisque.Critique && r.Probabilite == ProbabiliteRisque.Faible)))
                .Count();

            if (risquesCritiques > 0)
                scoreRouge += risquesCritiques; // Chaque risque critique = Rouge
            if (risquesEleves > 0)
                scoreAmber += risquesEleves; // Chaque risque élevé = Amber

            // 4. Vérification Anomalies (anomalies critiques ouvertes = Rouge)
            var anomaliesCritiquesOuvertes = projetComplet.Anomalies
                .Where(a => !a.EstSupprime && 
                           a.Statut == StatutAnomalie.Ouverte && 
                           a.Priorite == PrioriteAnomalie.Critique)
                .Count();

            if (anomaliesCritiquesOuvertes > 0)
                scoreRouge += anomaliesCritiquesOuvertes;

            // 5. Vérification Statut projet (Suspendu = Rouge, Clôture en cours = Amber)
            if (projetComplet.StatutProjet == StatutProjet.Suspendu)
                scoreRouge += 2;
            else if (projetComplet.StatutProjet == StatutProjet.ClotureEnCours)
                scoreAmber += 1;

            // 6. Vérification Livrables manquants (selon phase)
            // Cette logique est déjà gérée par le service de validation, mais on peut ajouter un point Amber
            // si des livrables obligatoires sont manquants pour la phase actuelle
            var livrablesObligatoires = await GetLivrablesObligatoiresPourPhaseAsync(projetComplet.PhaseActuelle);
            var livrablesExistants = projetComplet.Livrables
                .Where(l => !l.EstSupprime && livrablesObligatoires.Contains(l.TypeLivrable))
                .Select(l => l.TypeLivrable)
                .Distinct()
                .ToList();

            var livrablesManquants = livrablesObligatoires.Except(livrablesExistants).Count();
            if (livrablesManquants > 0)
                scoreAmber += 1; // Livrables manquants = Amber

            // Calcul final : Rouge si scoreRouge > 0, Amber si scoreAmber > 1 ou (scoreRouge = 0 et scoreAmber > 0), sinon Vert
            if (scoreRouge > 0)
                return IndicateurRAG.Rouge;
            else if (scoreAmber > 1 || (scoreRouge == 0 && scoreAmber > 0))
                return IndicateurRAG.Amber;
            else
                return IndicateurRAG.Vert;
        }

        public async Task MettreAJourRAGTousProjetsAsync()
        {
            var projetsActifs = await _context.Projets
                .Where(p => !p.EstSupprime && 
                           p.StatutProjet != StatutProjet.Cloture && 
                           p.StatutProjet != StatutProjet.Annule)
                .ToListAsync();

            foreach (var projet in projetsActifs)
            {
                var rag = await CalculerRAGAsync(projet);
                projet.IndicateurRAG = rag;
                projet.DateDernierCalculRAG = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<List<TypeLivrable>> GetLivrablesObligatoiresPourPhaseAsync(PhaseProjet phase)
        {
            // Logique similaire à LivrableValidationService
            return phase switch
            {
                PhaseProjet.AnalyseClarification => new List<TypeLivrable> { TypeLivrable.CharteProjet },
                PhaseProjet.PlanificationValidation => new List<TypeLivrable> 
                { 
                    TypeLivrable.Wbs, 
                    TypeLivrable.PlanningDetaille, 
                    TypeLivrable.MatriceRaci, 
                    TypeLivrable.BudgetPrevisionnel 
                },
                PhaseProjet.UatMep => new List<TypeLivrable> 
                { 
                    TypeLivrable.CahierTests, 
                    TypeLivrable.PvRecette, 
                    TypeLivrable.PvMep 
                },
                _ => new List<TypeLivrable>()
            };
        }
    }
}

