using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;

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
                DateDepot = DateTime.Now,
                DeposeParId = deposeParId,
                Commentaire = commentaire ?? string.Empty,
                Version = version ?? string.Empty,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule
            };

            _db.LivrablesProjets.Add(livrable);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPLOAD_LIVRABLE", "LivrableProjet", livrable.Id);
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

            livrable.DateModification = DateTime.Now;
            livrable.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync(
                "MISE_A_JOUR_LIVRABLE", "LivrableProjet", livrable.Id,
                new { ProjetId = projetId, NomDocument = livrable.NomDocument },
                new { Version = livrable.Version, Commentaire = livrable.Commentaire });
            return true;
        }
    }
}
