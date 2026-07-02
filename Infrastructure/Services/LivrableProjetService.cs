using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Extensions;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class LivrableProjetService : ILivrableProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public LivrableProjetService(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<Guid> DeposerAsync(
            Guid projetId,
            PhaseProjet phase,
            TypeLivrable typeLivrable,
            string nomDocument,
            string cheminRelatif,
            Guid deposeParId,
            string? commentaire,
            string? version)
        {
            var livrable = new LivrableProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Phase = phase,
                TypeLivrable = typeLivrable,
                NomDocument = nomDocument,
                CheminRelatif = cheminRelatif,
                DateDepot = DateTime.UtcNow,
                DeposeParId = deposeParId,
                Commentaire = commentaire ?? string.Empty,
                Version = version ?? string.Empty,
                DateCreation = DateTime.UtcNow,
                CreePar = _currentUserService.Matricule
            };

            _db.LivrablesProjets.Add(livrable);
            await _db.SaveChangesAsync();

            var (typeAction, details) = await _db.BuildChefProjetAuditAsync(
                "UPLOAD_LIVRABLE", projetId, deposeParId, new { livrable.NomDocument, livrable.Phase, livrable.TypeLivrable });
            await _auditService.LogActionAsync(typeAction, "LivrableProjet", livrable.Id, null, details);
            return livrable.Id;
        }

        public async Task<bool> MettreAJourAsync(
            Guid projetId,
            Guid livrableId,
            string? commentaire,
            string? version)
        {
            // DbContext étant scoped, FindAsync renvoie l'instance déjà suivie si le
            // contrôleur l'a chargée pour l'autorisation (pas de seconde requête).
            var livrable = await _db.LivrablesProjets.FindAsync(livrableId);
            if (livrable == null || livrable.ProjetId != projetId)
                return false;

            if (!string.IsNullOrWhiteSpace(commentaire))
                livrable.Commentaire = commentaire;
            if (!string.IsNullOrWhiteSpace(version))
                livrable.Version = version;

            livrable.DateModification = DateTime.UtcNow;
            livrable.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            var agissantId = await _db.Utilisateurs
                .Where(u => u.Matricule == _currentUserService.Matricule)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();
            var (typeAction, details) = await _db.BuildChefProjetAuditAsync(
                "MISE_A_JOUR_LIVRABLE", projetId, agissantId,
                new { Version = livrable.Version, Commentaire = livrable.Commentaire });
            await _auditService.LogActionAsync(typeAction, "LivrableProjet", livrable.Id,
                new { ProjetId = projetId, NomDocument = livrable.NomDocument }, details);
            return true;
        }
    }
}
