using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class UatValidationService : IUatValidationService
    {
        private readonly ApplicationDbContext _context;

        public UatValidationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UatValidationResult> ValiderRecetteAsync(Guid projetId)
        {
            var result = await EvaluerBaseAsync(projetId);

            if (result.TotalCasObligatoires == 0)
            {
                result.Erreurs.Add("Aucun cas de test obligatoire n'est défini pour cette UAT.");
            }

            if (result.CasSansExecution > 0)
            {
                result.Erreurs.Add($"{result.CasSansExecution} cas de test obligatoire(s) n'ont pas encore été exécuté(s).");
            }

            if (result.CasEnEchecOuBloques > 0)
            {
                result.Erreurs.Add($"{result.CasEnEchecOuBloques} cas de test obligatoire(s) sont encore en échec ou bloqués.");
            }

            if (result.AnomaliesBloquantes > 0)
            {
                result.Erreurs.Add($"{result.AnomaliesBloquantes} anomalie(s) critique(s) ou haute(s) restent ouvertes pour l'UAT.");
            }

            result.EstValide = result.Erreurs.Count == 0;
            return result;
        }

        public async Task<UatValidationResult> ValiderFinUatAsync(Guid projetId)
        {
            var result = await ValiderRecetteAsync(projetId);

            if (result.CampagnesOuvertes > 0)
            {
                result.Erreurs.Add($"{result.CampagnesOuvertes} campagne(s) de test doivent être clôturée(s) avant la fin de l'UAT.");
            }

            result.EstValide = result.Erreurs.Count == 0;
            return result;
        }

        public async Task<string> GenererReferenceCasTestAsync(Projet projet)
        {
            var nombreActuel = await _context.CasTestsProjets
                .Where(c => c.ProjetId == projet.Id && !c.EstSupprime)
                .CountAsync();

            return $"TC-{projet.CodeProjet}-{(nombreActuel + 1):000}";
        }

        public async Task<CampagneTestProjet> AssurerCampagneParDefautAsync(Projet projet, string? creePar)
        {
            var campagne = await _context.CampagnesTestsProjets
                .FirstOrDefaultAsync(c => c.ProjetId == projet.Id &&
                                          !c.EstSupprime &&
                                          c.Statut != StatutCampagneTest.Cloturee);

            if (campagne != null)
            {
                return campagne;
            }

            campagne = new CampagneTestProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Nom = $"Campagne UAT {projet.CodeProjet}",
                Description = "Campagne initiale créée automatiquement à l'entrée en UAT.",
                Environnement = Environnement.Recette,
                Statut = StatutCampagneTest.EnCours,
                DateLancement = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = creePar ?? "SYSTEM"
            };

            _context.CampagnesTestsProjets.Add(campagne);
            await _context.SaveChangesAsync();
            return campagne;
        }

        private async Task<UatValidationResult> EvaluerBaseAsync(Guid projetId)
        {
            var result = new UatValidationResult();

            var casObligatoires = await _context.CasTestsProjets
                .Where(c => c.ProjetId == projetId && !c.EstSupprime && c.EstObligatoire)
                .Select(c => new { c.Id })
                .ToListAsync();

            result.TotalCasObligatoires = casObligatoires.Count;

            if (casObligatoires.Count > 0)
            {
                var caseIds = casObligatoires.Select(c => c.Id).ToList();

                var dernieresExecutions = await _context.ExecutionsTestsProjets
                    .Where(e => e.ProjetId == projetId &&
                                !e.EstSupprime &&
                                caseIds.Contains(e.CasTestProjetId))
                    .GroupBy(e => e.CasTestProjetId)
                    .Select(g => g.OrderByDescending(e => e.DateExecution).First())
                    .ToListAsync();

                result.CasSansExecution = caseIds.Count(id => dernieresExecutions.All(e => e.CasTestProjetId != id));
                result.CasValides = dernieresExecutions.Count(e => e.Statut == StatutExecutionTest.Reussie || e.Statut == StatutExecutionTest.NonApplicable);
                result.CasEnEchecOuBloques = dernieresExecutions.Count(e => e.Statut == StatutExecutionTest.EnEchec || e.Statut == StatutExecutionTest.Bloquee || e.Statut == StatutExecutionTest.AExecuter);
            }

            result.AnomaliesBloquantes = await _context.AnomaliesProjets
                .Where(a => a.ProjetId == projetId &&
                            !a.EstSupprime &&
                            (a.Priorite == PrioriteAnomalie.Critique || a.Priorite == PrioriteAnomalie.Haute) &&
                            a.Statut != StatutAnomalie.Fermee &&
                            a.Statut != StatutAnomalie.Rejetee)
                .CountAsync();

            result.CampagnesOuvertes = await _context.CampagnesTestsProjets
                .Where(c => c.ProjetId == projetId &&
                            !c.EstSupprime &&
                            c.Statut != StatutCampagneTest.Cloturee)
                .CountAsync();

            return result;
        }
    }
}
