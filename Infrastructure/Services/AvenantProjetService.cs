using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Implémentation du workflow d'avenant projet (gestion du changement).
    /// L'autorisation reste au contrôleur ; ici la logique métier, l'application
    /// du changement au projet et l'audit.
    /// </summary>
    public class AvenantProjetService : IAvenantProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public AvenantProjetService(ApplicationDbContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public async Task<List<AvenantProjet>> ListerAsync(Guid projetId)
        {
            return await _db.AvenantsProjets
                .Include(a => a.DemandePar)
                .Include(a => a.ValideParDMUtilisateur)
                .Include(a => a.ValideParDSIUtilisateur)
                .Where(a => a.ProjetId == projetId && !a.EstSupprime)
                .OrderByDescending(a => a.Numero)
                .ToListAsync();
        }

        public async Task<WorkflowResult> CreerAsync(
            Guid projetId,
            Guid userId,
            TypeAvenant type,
            string titre,
            string justification,
            string? descriptionPerimetre,
            decimal? nouveauBudget,
            DateTime? nouvelleDateFinPrevue)
        {
            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
                return WorkflowResult.NotFound();

            if (projet.StatutProjet is StatutProjet.Cloture or StatutProjet.Annule)
                return WorkflowResult.Error("Impossible de créer un avenant sur un projet clôturé ou annulé.");

            if (string.IsNullOrWhiteSpace(titre) || string.IsNullOrWhiteSpace(justification))
                return WorkflowResult.Error("Le titre et la justification de l'avenant sont obligatoires.");

            var toucheBudget = type is TypeAvenant.Budget or TypeAvenant.Mixte;
            var toucheDelai = type is TypeAvenant.Delai or TypeAvenant.Mixte;
            var touchePerimetre = type is TypeAvenant.Perimetre or TypeAvenant.Mixte;

            if (toucheBudget && (!nouveauBudget.HasValue || nouveauBudget.Value < 0))
                return WorkflowResult.Error("Un avenant budgétaire nécessite un nouveau budget valide.");

            if (toucheDelai && !nouvelleDateFinPrevue.HasValue)
                return WorkflowResult.Error("Un avenant de délai nécessite une nouvelle date de fin prévue.");

            if (touchePerimetre && string.IsNullOrWhiteSpace(descriptionPerimetre))
                return WorkflowResult.Error("Un avenant de périmètre nécessite une description du changement de périmètre.");

            var dernierNumero = await _db.AvenantsProjets
                .Where(a => a.ProjetId == projetId)
                .Select(a => (int?)a.Numero)
                .MaxAsync() ?? 0;

            var avenant = new AvenantProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Numero = dernierNumero + 1,
                Type = type,
                Titre = titre.Trim(),
                Justification = justification.Trim(),
                DescriptionPerimetre = touchePerimetre ? descriptionPerimetre?.Trim() : null,
                AncienBudget = toucheBudget ? projet.FicheProjet?.BudgetPrevisionnel : null,
                NouveauBudget = toucheBudget ? nouveauBudget : null,
                AncienneDateFinPrevue = toucheDelai ? projet.DateFinPrevue : null,
                NouvelleDateFinPrevue = toucheDelai ? nouvelleDateFinPrevue : null,
                Statut = StatutAvenant.EnAttenteValidationDM,
                DemandeParId = userId,
                DateDemande = DateTime.UtcNow
            };

            _db.AvenantsProjets.Add(avenant);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CREATION_AVENANT", "AvenantProjet", avenant.Id,
                null,
                new { ProjetId = projetId, avenant.Numero, Type = type.ToString(), avenant.Titre });

            return WorkflowResult.Success($"Avenant AV-{avenant.Numero:D2} soumis pour validation Métier.");
        }

        public async Task<WorkflowResult> ValiderDmAsync(Guid avenantId, Guid userId)
        {
            var avenant = await _db.AvenantsProjets.FirstOrDefaultAsync(a => a.Id == avenantId);
            if (avenant == null)
                return WorkflowResult.NotFound();

            if (avenant.Statut != StatutAvenant.EnAttenteValidationDM)
                return WorkflowResult.Error("Cet avenant n'est pas en attente de validation Métier.");

            avenant.Statut = StatutAvenant.EnAttenteValidationDSI;
            avenant.ValideParDMId = userId;
            avenant.DateValidationDM = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_AVENANT_DM", "AvenantProjet", avenant.Id);

            return WorkflowResult.Success($"Avenant AV-{avenant.Numero:D2} validé par le Métier, en attente de la DSI.");
        }

        public async Task<WorkflowResult> ValiderDsiAsync(Guid avenantId, Guid userId)
        {
            var avenant = await _db.AvenantsProjets.FirstOrDefaultAsync(a => a.Id == avenantId);
            if (avenant == null)
                return WorkflowResult.NotFound();

            if (avenant.Statut != StatutAvenant.EnAttenteValidationDSI)
                return WorkflowResult.Error("Cet avenant n'est pas en attente de validation DSI.");

            var projet = await _db.Projets
                .Include(p => p.FicheProjet)
                .FirstOrDefaultAsync(p => p.Id == avenant.ProjetId);
            if (projet == null)
                return WorkflowResult.NotFound();

            // Application du changement au projet (la validation DSI fait foi).
            if (avenant.NouvelleDateFinPrevue.HasValue)
                projet.DateFinPrevue = avenant.NouvelleDateFinPrevue;

            if (avenant.NouveauBudget.HasValue && projet.FicheProjet != null)
                projet.FicheProjet.BudgetPrevisionnel = avenant.NouveauBudget.Value;

            avenant.Statut = StatutAvenant.Applique;
            avenant.ValideParDSIId = userId;
            avenant.DateValidationDSI = DateTime.UtcNow;
            avenant.DateApplication = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_AVENANT_DSI", "AvenantProjet", avenant.Id,
                new { avenant.AncienBudget, avenant.AncienneDateFinPrevue },
                new { avenant.NouveauBudget, avenant.NouvelleDateFinPrevue });

            return WorkflowResult.Success($"Avenant AV-{avenant.Numero:D2} validé et appliqué au projet.");
        }

        public async Task<WorkflowResult> RejeterAsync(Guid avenantId, Guid userId, string commentaire)
        {
            var avenant = await _db.AvenantsProjets.FirstOrDefaultAsync(a => a.Id == avenantId);
            if (avenant == null)
                return WorkflowResult.NotFound();

            if (avenant.Statut is StatutAvenant.Applique or StatutAvenant.Rejete)
                return WorkflowResult.Error("Cet avenant est déjà finalisé.");

            if (string.IsNullOrWhiteSpace(commentaire))
                return WorkflowResult.Error("Un motif de rejet est obligatoire.");

            avenant.Statut = StatutAvenant.Rejete;
            avenant.CommentaireRejet = commentaire.Trim();
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_AVENANT", "AvenantProjet", avenant.Id,
                null,
                new { Motif = avenant.CommentaireRejet });

            return WorkflowResult.Success($"Avenant AV-{avenant.Numero:D2} rejeté.");
        }
    }
}
