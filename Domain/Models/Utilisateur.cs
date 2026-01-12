using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class Utilisateur : EntiteAudit
    {
        public Guid Id { get; set; }
        public string Matricule { get; set; }
        public string MotDePasse { get; set; }
        public string Nom {  get; set; }
        public string Prenoms { get; set; }
        public string Email { get; set; }
        public Guid? DirectionId { get; set; }
        public Direction Direction { get; set; }

        // Rôles multiples : relation many-to-many (bonne pratique RBAC)
        public ICollection<UtilisateurRole> UtilisateurRoles { get; set; } = new List<UtilisateurRole>();

        public DateTime? DateDerniereConnexion { get; set; }
        public int NombreConnexion { get; set; }
        
        public bool PeutCreerDemandeProjet { get; set; } = true; // Par défaut, tous les utilisateurs peuvent créer des demandes

        /// <summary>
        /// Capacité hebdomadaire en heures (par défaut 40h pour un temps plein)
        /// Utilisé pour le suivi des charges et le calcul de surcharge
        /// </summary>
        public decimal CapaciteHebdomadaire { get; set; } = 40;

        // Méthode helper pour obtenir les rôles actifs
        public IEnumerable<RoleUtilisateur> GetRolesActifs()
        {
            return UtilisateurRoles?
                .Where(ur => !ur.EstSupprime &&
                    (!ur.DateDebut.HasValue || ur.DateDebut.Value <= DateTime.Now) &&
                    (!ur.DateFin.HasValue || ur.DateFin.Value >= DateTime.Now))
                .Select(ur => ur.Role)
                .ToList() ?? new List<RoleUtilisateur>();
        }

        // Propriété de compatibilité : retourne le premier rôle actif ou Demandeur par défaut
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public RoleUtilisateur Role
        {
            get => GetRolesActifs().FirstOrDefault();
        }
    }
}
