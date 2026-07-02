using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Implémentation de l'évaluation des membres. L'autorisation reste au contrôleur ;
    /// ici la logique métier (upsert, validation des notes) et l'audit.
    /// </summary>
    public class EvaluationMembreProjetService : IEvaluationMembreProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public EvaluationMembreProjetService(ApplicationDbContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public async Task<List<EvaluationMembreProjet>> ListerAsync(Guid projetId)
        {
            return await _db.EvaluationsMembresProjets
                .Include(e => e.MembreProjet)
                .Include(e => e.Evaluateur)
                .Where(e => e.ProjetId == projetId && !e.EstSupprime)
                .OrderBy(e => e.MembreProjet.Nom)
                .ToListAsync();
        }

        public async Task<WorkflowResult> EnregistrerAsync(
            Guid projetId, Guid membreProjetId, Guid evaluateurId,
            int noteQualite, int noteRespectDelais, int noteCollaboration, string? commentaire)
        {
            if (!EstNoteValide(noteQualite) || !EstNoteValide(noteRespectDelais) || !EstNoteValide(noteCollaboration))
                return WorkflowResult.Error("Chaque note doit être comprise entre 1 et 5.");

            var membre = await _db.MembresProjets.FirstOrDefaultAsync(m => m.Id == membreProjetId && m.ProjetId == projetId);
            if (membre == null)
                return WorkflowResult.NotFound();

            var evaluation = await _db.EvaluationsMembresProjets
                .FirstOrDefaultAsync(e => e.ProjetId == projetId && e.MembreProjetId == membreProjetId && !e.EstSupprime);

            var estNouvelle = evaluation == null;
            if (evaluation == null)
            {
                evaluation = new EvaluationMembreProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    MembreProjetId = membreProjetId
                };
                _db.EvaluationsMembresProjets.Add(evaluation);
            }

            evaluation.EvaluateurId = evaluateurId;
            evaluation.DateEvaluation = DateTime.UtcNow;
            evaluation.NoteQualite = noteQualite;
            evaluation.NoteRespectDelais = noteRespectDelais;
            evaluation.NoteCollaboration = noteCollaboration;
            evaluation.Commentaire = commentaire?.Trim();

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync(
                estNouvelle ? "AJOUT_EVALUATION_MEMBRE" : "MODIFICATION_EVALUATION_MEMBRE",
                "EvaluationMembreProjet", evaluation.Id,
                null, new { ProjetId = projetId, MembreProjetId = membreProjetId, noteQualite, noteRespectDelais, noteCollaboration });

            return WorkflowResult.Success("Évaluation enregistrée.");
        }

        public async Task<WorkflowResult> SupprimerAsync(Guid evaluationId, Guid userId)
        {
            var evaluation = await _db.EvaluationsMembresProjets.FirstOrDefaultAsync(e => e.Id == evaluationId);
            if (evaluation == null)
                return WorkflowResult.NotFound();

            evaluation.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_EVALUATION_MEMBRE", "EvaluationMembreProjet", evaluation.Id);

            return WorkflowResult.Success("Évaluation supprimée.");
        }

        private static bool EstNoteValide(int note) => note is >= 1 and <= 5;
    }
}
