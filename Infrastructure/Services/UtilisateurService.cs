using GestionProjects.Application.Common.Helpers;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Service métier pour la gestion des utilisateurs et de leurs rôles.
    /// Centralise la logique extraite de l'AdminController.
    /// </summary>
    public class UtilisateurService : IUtilisateurService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public UtilisateurService(ApplicationDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        /// <inheritdoc/>
        public List<RoleUtilisateur> ParseSelectedRoles(string? roles)
        {
            var roleIds = (roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value.Trim(), out var roleId) ? roleId : 0)
                .Where(roleId => roleId > 0)
                .Distinct()
                .ToList();

            var parsedRoles = roleIds
                .Select(roleId => (RoleUtilisateur)roleId)
                .Where(role => Enum.IsDefined(typeof(RoleUtilisateur), role))
                .Distinct()
                .ToList();

            if (!parsedRoles.Any())
            {
                parsedRoles.Add(RoleUtilisateur.Demandeur);
            }

            // Règle métier : AdminIT est exclusif — ne peut pas coexister avec d'autres rôles
            if (parsedRoles.Contains(RoleUtilisateur.AdminIT))
            {
                return new List<RoleUtilisateur> { RoleUtilisateur.AdminIT };
            }

            return parsedRoles;
        }

        /// <inheritdoc/>
        public async Task SynchronizeUserRolesAsync(Utilisateur user, IEnumerable<RoleUtilisateur> selectedRoles)
        {
            var desiredRoles = selectedRoles.Distinct().ToList();
            var operateur = _currentUserService.Matricule ?? "SYSTEM";

            // Charger tous les rôles (y compris soft-deleted) en ignorant le filtre global.
            // Cela évite les problèmes de tracking EF Core liés à la navigation collection
            // filtrée, et permet de réactiver un rôle précédemment supprimé au lieu
            // d'en insérer un doublon.
            var allExistingRoles = await _db.UtilisateurRoles
                .IgnoreQueryFilters()
                .Where(ur => ur.UtilisateurId == user.Id)
                .ToListAsync();

            // Activer ou créer les rôles désirés
            foreach (var role in desiredRoles)
            {
                var activeRole = allExistingRoles.FirstOrDefault(ur => ur.Role == role && !ur.EstSupprime);
                if (activeRole != null)
                    continue;

                var deletedRole = allExistingRoles.FirstOrDefault(ur => ur.Role == role && ur.EstSupprime);
                if (deletedRole != null)
                {
                    deletedRole.EstSupprime = false;
                    deletedRole.DateDebut = DateTime.Now;
                    deletedRole.DateFin = null;
                    deletedRole.DateModification = DateTime.Now;
                    deletedRole.ModifiePar = operateur;
                }
                else
                {
                    _db.UtilisateurRoles.Add(new UtilisateurRole
                    {
                        Id = Guid.NewGuid(),
                        UtilisateurId = user.Id,
                        Role = role,
                        DateDebut = DateTime.Now,
                        DateCreation = DateTime.Now,
                        CreePar = operateur,
                        EstSupprime = false
                    });
                }
            }

            // Désactiver les rôles retirés (source matérialisée pour éviter
            // toute interaction avec le change tracker durant l'itération)
            var rolesToRemove = allExistingRoles
                .Where(ur => !ur.EstSupprime && !desiredRoles.Contains(ur.Role))
                .ToList();

            foreach (var existingRole in rolesToRemove)
            {
                existingRole.EstSupprime = true;
                existingRole.DateFin = DateTime.Now;
                existingRole.DateModification = DateTime.Now;
                existingRole.ModifiePar = operateur;
            }
        }

        /// <inheritdoc/>
        public async Task<Utilisateur> CreateUserAsync(
            string matricule,
            string nom,
            string prenoms,
            string email,
            string motDePasse,
            Guid? directionId,
            IEnumerable<RoleUtilisateur> roles,
            bool peutCreerDemandeProjet = true,
            ProfilRessource? profilRessource = null,
            decimal capaciteHebdomadaire = 40)
        {
            if (!ValidationHelper.IsStrongPassword(motDePasse))
                throw new ArgumentException(ValidationHelper.StrongPasswordPolicyMessage, nameof(motDePasse));

            if (await MatriculeExisteAsync(matricule))
                throw new ArgumentException($"Le matricule '{matricule}' est déjà utilisé.", nameof(matricule));

            if (await EmailExisteAsync(email))
                throw new ArgumentException($"L'email '{email}' est déjà utilisé.", nameof(email));

            var userId = Guid.NewGuid();
            var operateur = _currentUserService.Matricule ?? "SYSTEM";

            var user = new Utilisateur
            {
                Id = userId,
                Matricule = matricule.Trim(),
                Nom = nom.Trim(),
                Prenoms = prenoms.Trim(),
                Email = email.Trim(),
                MotDePasse = BCrypt.Net.BCrypt.HashPassword(motDePasse),
                DirectionId = directionId,
                PeutCreerDemandeProjet = peutCreerDemandeProjet,
                ProfilRessource = profilRessource,
                CapaciteHebdomadaire = capaciteHebdomadaire > 0 ? capaciteHebdomadaire : 40,
                NombreConnexion = 0,
                DateCreation = DateTime.Now,
                CreePar = operateur,
                EstSupprime = false
            };

            _db.Utilisateurs.Add(user);

            var rolesValides = ParseSelectedRoles(string.Join(",", roles.Select(r => (int)r)));
            await SynchronizeUserRolesAsync(user, rolesValides);

            return user;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateUserAsync(
            Guid id,
            string matricule,
            string nom,
            string prenoms,
            string email,
            Guid? directionId,
            IEnumerable<RoleUtilisateur> roles,
            string? nouveauMotDePasse = null,
            bool peutCreerDemandeProjet = true,
            ProfilRessource? profilRessource = null,
            decimal? capaciteHebdomadaire = null)
        {
            var user = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);

            if (user == null)
                return false;

            if (!string.IsNullOrWhiteSpace(nouveauMotDePasse))
            {
                if (!ValidationHelper.IsStrongPassword(nouveauMotDePasse))
                    throw new ArgumentException(ValidationHelper.StrongPasswordPolicyMessage, nameof(nouveauMotDePasse));

                user.MotDePasse = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);
            }

            user.Matricule = matricule.Trim();
            user.Nom = nom.Trim();
            user.Prenoms = prenoms.Trim();
            user.Email = email.Trim();
            user.DirectionId = directionId;
            user.PeutCreerDemandeProjet = peutCreerDemandeProjet;
            user.ProfilRessource = profilRessource;

            if (capaciteHebdomadaire.HasValue && capaciteHebdomadaire.Value > 0)
                user.CapaciteHebdomadaire = capaciteHebdomadaire.Value;

            user.DateModification = DateTime.Now;
            user.ModifiePar = _currentUserService.Matricule;

            var rolesValides = ParseSelectedRoles(string.Join(",", roles.Select(r => (int)r)));
            await SynchronizeUserRolesAsync(user, rolesValides);

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> MatriculeExisteAsync(string matricule, Guid? excludeUserId = null)
        {
            var query = _db.Utilisateurs.Where(u => u.Matricule == matricule && !u.EstSupprime);
            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);
            return await query.AnyAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> EmailExisteAsync(string email, Guid? excludeUserId = null)
        {
            var query = _db.Utilisateurs.Where(u => u.Email == email && !u.EstSupprime);
            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);
            return await query.AnyAsync();
        }
    }
}
