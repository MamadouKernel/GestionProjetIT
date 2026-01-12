using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Common;
using GestionProjects.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GestionProjects.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ICurrentUserService? _currentUserService;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ICurrentUserService? currentUserService = null)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        // ================
        // DbSet par entité
        // ================

        public DbSet<Direction> Directions { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<UtilisateurRole> UtilisateurRoles { get; set; }

        public DbSet<DemandeProjet> DemandesProjets { get; set; }
        public DbSet<DocumentJointDemande> DocumentsJointsDemandes { get; set; }

        public DbSet<Projet> Projets { get; set; }
        public DbSet<MembreProjet> MembresProjets { get; set; }

        public DbSet<RisqueProjet> RisquesProjets { get; set; }
        public DbSet<HistoriquePhaseProjet> HistoriquePhasesProjets { get; set; }
        public DbSet<HistoriqueChefProjet> HistoriqueChefProjets { get; set; }

        public DbSet<LivrableProjet> LivrablesProjets { get; set; }
        public DbSet<AnomalieProjet> AnomaliesProjets { get; set; }

        public DbSet<DemandeClotureProjet> DemandesClotureProjets { get; set; }

        public DbSet<DelegationValidationDSI> DelegationsValidationDSI { get; set; }
        public DbSet<DelegationChefProjet> DelegationsChefProjet { get; set; }
        public DbSet<ParametreSysteme> ParametresSysteme { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PortefeuilleProjet> PortefeuillesProjets { get; set; }
        public DbSet<CharteProjet> CharteProjets { get; set; }
        public DbSet<JalonCharte> JalonsCharte { get; set; }
        public DbSet<PartiePrenanteCharte> PartiesPrenantesCharte { get; set; }
        public DbSet<FicheProjet> FicheProjets { get; set; }
        public DbSet<ChargeProjet> ChargesProjets { get; set; }

        // ======================
        // OnModelCreating minimal
        // ======================

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- Relations 1-1 DemandeProjet <-> Projet ----
            modelBuilder.Entity<Projet>()
                .HasOne(p => p.DemandeProjet)
                .WithOne(d => d.Projet)
                .HasForeignKey<Projet>(p => p.DemandeProjetId);

            // ---- Relations Direction ----
            modelBuilder.Entity<Direction>()
                .HasOne(d => d.DSI)
                .WithMany()
                .HasForeignKey(d => d.DSIId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Utilisateur>()
                .HasOne(u => u.Direction)
                .WithMany()
                .HasForeignKey(u => u.DirectionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DemandeProjet>()
                .HasOne(d => d.Direction)
                .WithMany()
                .HasForeignKey(d => d.DirectionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Projet>()
                .HasOne(p => p.Direction)
                .WithMany()
                .HasForeignKey(p => p.DirectionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Relations Service ----
            modelBuilder.Entity<Service>()
                .HasOne(s => s.Direction)
                .WithMany(d => d.Services)
                .HasForeignKey(s => s.DirectionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Relations DemandeProjet ----
            modelBuilder.Entity<DemandeProjet>()
                .HasOne(d => d.Demandeur)
                .WithMany()
                .HasForeignKey(d => d.DemandeurId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DemandeProjet>()
                .HasOne(d => d.DirecteurMetier)
                .WithMany()
                .HasForeignKey(d => d.DirecteurMetierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentJointDemande>()
                .HasOne(d => d.DemandeProjet)
                .WithMany(dp => dp.Annexes)
                .HasForeignKey(d => d.DemandeProjetId);

            // ---- Relations Projet ----
            modelBuilder.Entity<Projet>()
                .HasOne(p => p.Sponsor)
                .WithMany()
                .HasForeignKey(p => p.SponsorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Projet>()
                .HasOne(p => p.ChefProjet)
                .WithMany()
                .HasForeignKey(p => p.ChefProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Projet>()
                .HasOne(p => p.RecetteValideePar)
                .WithMany()
                .HasForeignKey(p => p.RecetteValideeParId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Projet>()
                .HasOne(p => p.CharteValideeParDMUtilisateur)
                .WithMany()
                .HasForeignKey(p => p.CharteValideeParDMId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Projet>()
                .HasOne(p => p.CharteValideeParDSIUtilisateur)
                .WithMany()
                .HasForeignKey(p => p.CharteValideeParDSIId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MembreProjet>()
                .HasOne(m => m.Projet)
                .WithMany(p => p.Membres)
                .HasForeignKey(m => m.ProjetId);

            modelBuilder.Entity<RisqueProjet>()
                .HasOne(r => r.Projet)
                .WithMany(p => p.Risques)
                .HasForeignKey(r => r.ProjetId);

            modelBuilder.Entity<HistoriquePhaseProjet>()
                .HasOne(h => h.Projet)
                .WithMany(p => p.HistoriquePhases)
                .HasForeignKey(h => h.ProjetId);

            modelBuilder.Entity<HistoriquePhaseProjet>()
                .HasOne(h => h.ModifieParUtilisateur)
                .WithMany()
                .HasForeignKey(h => h.ModifieParId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HistoriqueChefProjet>()
                .HasOne(h => h.Projet)
                .WithMany()
                .HasForeignKey(h => h.ProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HistoriqueChefProjet>()
                .HasOne(h => h.ChefProjet)
                .WithMany()
                .HasForeignKey(h => h.ChefProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Relations UtilisateurRole (Many-to-Many) ----
            modelBuilder.Entity<UtilisateurRole>()
                .HasOne(ur => ur.Utilisateur)
                .WithMany(u => u.UtilisateurRoles)
                .HasForeignKey(ur => ur.UtilisateurId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index pour améliorer les performances
            modelBuilder.Entity<UtilisateurRole>()
                .HasIndex(ur => new { ur.UtilisateurId, ur.Role })
                .IsUnique();

            // ---- Relations Notification ----
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Utilisateur)
                .WithMany()
                .HasForeignKey(n => n.UtilisateurId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index pour améliorer les performances des notifications
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UtilisateurId, n.EstLue, n.EstSupprime });

            // ---- Relations PortefeuilleProjet ----
            modelBuilder.Entity<Projet>()
                .HasOne(p => p.PortefeuilleProjet)
                .WithMany()
                .HasForeignKey(p => p.PortefeuilleProjetId)
                .OnDelete(DeleteBehavior.SetNull);

            // ---- Relations CharteProjet ----
            modelBuilder.Entity<CharteProjet>()
                .HasOne(c => c.Projet)
                .WithOne(p => p.CharteProjet)
                .HasForeignKey<CharteProjet>(c => c.ProjetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CharteProjet>()
                .HasOne(c => c.Demandeur)
                .WithMany()
                .HasForeignKey(c => c.DemandeurId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CharteProjet>()
                .HasOne(c => c.ChefProjet)
                .WithMany()
                .HasForeignKey(c => c.ChefProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CharteProjet>()
                .HasOne(c => c.SignatureSponsorUtilisateur)
                .WithMany()
                .HasForeignKey(c => c.SignatureSponsorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CharteProjet>()
                .HasOne(c => c.SignatureChefProjetUtilisateur)
                .WithMany()
                .HasForeignKey(c => c.SignatureChefProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Relations JalonCharte ----
            modelBuilder.Entity<JalonCharte>()
                .HasOne(j => j.CharteProjet)
                .WithMany(c => c.Jalons)
                .HasForeignKey(j => j.CharteProjetId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---- Relations PartiePrenanteCharte ----
            modelBuilder.Entity<PartiePrenanteCharte>()
                .HasOne(p => p.CharteProjet)
                .WithMany(c => c.PartiesPrenantes)
                .HasForeignKey(p => p.CharteProjetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PartiePrenanteCharte>()
                .HasOne(p => p.Utilisateur)
                .WithMany()
                .HasForeignKey(p => p.UtilisateurId)
                .OnDelete(DeleteBehavior.SetNull);

            // ---- Relations FicheProjet ----
            modelBuilder.Entity<FicheProjet>()
                .HasOne(f => f.Projet)
                .WithOne(p => p.FicheProjet)
                .HasForeignKey<FicheProjet>(f => f.ProjetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FicheProjet>()
                .HasOne(f => f.DerniereMiseAJourPar)
                .WithMany()
                .HasForeignKey(f => f.DerniereMiseAJourParId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LivrableProjet>()
                .HasOne(l => l.Projet)
                .WithMany(p => p.Livrables)
                .HasForeignKey(l => l.ProjetId);

            modelBuilder.Entity<LivrableProjet>()
                .HasOne(l => l.DeposePar)
                .WithMany()
                .HasForeignKey(l => l.DeposeParId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AnomalieProjet>()
                .HasOne(a => a.Projet)
                .WithMany(p => p.Anomalies)
                .HasForeignKey(a => a.ProjetId);

            modelBuilder.Entity<DemandeClotureProjet>()
                .HasOne(d => d.Projet)
                .WithMany(p => p.DemandesCloture)
                .HasForeignKey(d => d.ProjetId);

            modelBuilder.Entity<DemandeClotureProjet>()
                .HasOne(d => d.DemandePar)
                .WithMany()
                .HasForeignKey(d => d.DemandeParId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Relations ChargeProjet ----
            modelBuilder.Entity<ChargeProjet>()
                .HasOne(c => c.Projet)
                .WithMany()
                .HasForeignKey(c => c.ProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChargeProjet>()
                .HasOne(c => c.Ressource)
                .WithMany()
                .HasForeignKey(c => c.RessourceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChargeProjet>()
                .HasOne(c => c.SaisiePar)
                .WithMany()
                .HasForeignKey(c => c.SaisieParId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index pour améliorer les performances des requêtes de charges
            modelBuilder.Entity<ChargeProjet>()
                .HasIndex(c => new { c.ProjetId, c.SemaineDebut, c.RessourceId })
                .IsUnique();

            // ---- Délégations DSI ----
            modelBuilder.Entity<DelegationValidationDSI>()
                .HasOne(d => d.DSI)
                .WithMany()
                .HasForeignKey(d => d.DSIId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DelegationValidationDSI>()
                .HasOne(d => d.Delegue)
                .WithMany()
                .HasForeignKey(d => d.DelegueId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Délégations ChefProjet ----
            modelBuilder.Entity<DelegationChefProjet>()
                .HasOne(d => d.Projet)
                .WithMany()
                .HasForeignKey(d => d.ProjetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DelegationChefProjet>()
                .HasOne(d => d.Delegant)
                .WithMany()
                .HasForeignKey(d => d.DelegantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DelegationChefProjet>()
                .HasOne(d => d.Delegue)
                .WithMany()
                .HasForeignKey(d => d.DelegueId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- AuditLog ----
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Utilisateur)
                .WithMany()
                .HasForeignKey(a => a.UtilisateurId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==========================
            // Global filter EstSupprime == false
            // ==========================
            AppliquerFiltreSoftDelete(modelBuilder);

            // ==========================
            // Configuration ModifiePar nullable pour toutes les entités
            // ==========================
            ConfigurerModifieParNullable(modelBuilder);
        }

        /// <summary>
        /// Configure ModifiePar comme nullable pour toutes les entités qui héritent de EntiteAudit
        /// </summary>
        private void ConfigurerModifieParNullable(ModelBuilder modelBuilder)
        {
            var entiteAuditType = typeof(EntiteAudit);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entiteAuditType.IsAssignableFrom(entityType.ClrType))
                {
                    var entityBuilder = modelBuilder.Entity(entityType.ClrType);
                    entityBuilder.Property(nameof(EntiteAudit.ModifiePar))
                        .IsRequired(false);
                }
            }
        }

        /// <summary>
        /// Applique un filtre global "EstSupprime == false"
        /// sur toutes les entités qui héritent de EntiteAudit.
        /// </summary>
        private void AppliquerFiltreSoftDelete(ModelBuilder modelBuilder)
        {
            var entiteAuditType = typeof(EntiteAudit);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!entiteAuditType.IsAssignableFrom(entityType.ClrType))
                    continue;

                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(parameter, nameof(EntiteAudit.EstSupprime));
                var condition = Expression.Equal(prop, Expression.Constant(false));
                var lambda = Expression.Lambda(condition, parameter);

                entityType.SetQueryFilter(lambda);
            }
        }

        // ===================================
        // Audit automatique dans SaveChanges
        // ===================================

        public override int SaveChanges()
        {
            AppliquerAuditAvantSave();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AppliquerAuditAvantSave();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void AppliquerAuditAvantSave()
        {
            var userName = _currentUserService?.Matricule ?? "SYSTEM";
            var maintenant = DateTime.Now;

            foreach (var entry in ChangeTracker.Entries<EntiteAudit>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.DateCreation = maintenant;
                    entry.Entity.CreePar = string.IsNullOrWhiteSpace(entry.Entity.CreePar)
                        ? userName
                        : entry.Entity.CreePar;

                    entry.Entity.EstSupprime = false;
                    // ModifiePar reste null pour les nouvelles entités
                    if (entry.Entity.ModifiePar == null)
                    {
                        entry.Entity.ModifiePar = null;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.DateModification = maintenant;
                    entry.Entity.ModifiePar = userName;
                }
            }
        }
    }
}
