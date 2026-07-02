using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Implémentation de la réalisation des bénéfices. L'autorisation reste au contrôleur ;
    /// ici la logique métier et l'audit.
    /// </summary>
    public class BeneficeProjetService : IBeneficeProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public BeneficeProjetService(ApplicationDbContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public async Task<List<BeneficeProjet>> ListerAsync(Guid projetId)
        {
            return await _db.BeneficesProjets
                .Where(b => b.ProjetId == projetId && !b.EstSupprime)
                .OrderBy(b => b.DateCreation)
                .ToListAsync();
        }

        public async Task<WorkflowResult> AjouterAsync(
            Guid projetId, Guid userId, string libelle, string indicateur,
            string valeurCible, DateTime? dateCibleRealisation)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null)
                return WorkflowResult.NotFound();

            if (string.IsNullOrWhiteSpace(libelle))
                return WorkflowResult.Error("Le libellé du bénéfice est obligatoire.");

            var benefice = new BeneficeProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Libelle = libelle.Trim(),
                Indicateur = (indicateur ?? string.Empty).Trim(),
                ValeurCible = (valeurCible ?? string.Empty).Trim(),
                DateCibleRealisation = dateCibleRealisation,
                Statut = StatutBenefice.Attendu
            };

            _db.BeneficesProjets.Add(benefice);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("AJOUT_BENEFICE", "BeneficeProjet", benefice.Id,
                null, new { ProjetId = projetId, benefice.Libelle });

            return WorkflowResult.Success("Bénéfice attendu ajouté.");
        }

        public async Task<WorkflowResult> EvaluerAsync(
            Guid beneficeId, Guid userId, StatutBenefice statut,
            string? valeurRealisee, string? commentaire)
        {
            var benefice = await _db.BeneficesProjets.FirstOrDefaultAsync(b => b.Id == beneficeId);
            if (benefice == null)
                return WorkflowResult.NotFound();

            if (statut == StatutBenefice.Attendu)
                return WorkflowResult.Error("Choisissez un état de réalisation (réalisé, partiel ou non réalisé).");

            benefice.Statut = statut;
            benefice.ValeurRealisee = valeurRealisee?.Trim();
            benefice.CommentaireRevue = commentaire?.Trim();
            benefice.DateRevue = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REVUE_BENEFICE", "BeneficeProjet", benefice.Id,
                null, new { Statut = statut.ToString(), benefice.ValeurRealisee });

            return WorkflowResult.Success("Revue du bénéfice enregistrée.");
        }

        public async Task<WorkflowResult> SupprimerAsync(Guid beneficeId, Guid userId)
        {
            var benefice = await _db.BeneficesProjets.FirstOrDefaultAsync(b => b.Id == beneficeId);
            if (benefice == null)
                return WorkflowResult.NotFound();

            benefice.EstSupprime = true;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SUPPRESSION_BENEFICE", "BeneficeProjet", benefice.Id);

            return WorkflowResult.Success("Bénéfice supprimé.");
        }
    }
}
