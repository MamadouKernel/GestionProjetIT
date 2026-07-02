using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;

namespace GestionProjects.Infrastructure.Services
{
    public class MembreProjetService : IMembreProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public MembreProjetService(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<bool> AjouterMembreAsync(Guid projetId, Guid utilisateurId, string roleDansProjet)
        {
            var utilisateur = await _db.Utilisateurs.FindAsync(utilisateurId);
            if (utilisateur == null)
                return false;

            var membre = new MembreProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projetId,
                Nom = utilisateur.Nom,
                Prenom = utilisateur.Prenoms,
                RoleDansProjet = roleDansProjet,
                Email = utilisateur.Email,
                DirectionLibelle = utilisateur.Direction?.Libelle ?? string.Empty,
                EstActif = true,
                DateCreation = DateTime.UtcNow,
                CreePar = _currentUserService.Matricule
            };

            _db.MembresProjets.Add(membre);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("AJOUT_MEMBRE", "MembreProjet", membre.Id);
            return true;
        }

        public async Task<bool> RetirerMembreAsync(Guid projetId, Guid membreId)
        {
            var membre = await _db.MembresProjets.FindAsync(membreId);
            if (membre == null || membre.ProjetId != projetId)
                return false;

            membre.EstActif = false;
            membre.EstSupprime = true;
            membre.DateModification = DateTime.UtcNow;
            membre.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("RETRAIT_MEMBRE_PROJET", "MembreProjet", membre.Id,
                new { ProjetId = projetId, MembreNom = $"{membre.Nom} {membre.Prenom}" },
                new { Action = "Retiré/Désactivé" });
            return true;
        }
    }
}
