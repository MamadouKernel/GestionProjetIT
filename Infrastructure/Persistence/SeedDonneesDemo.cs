using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GestionProjects.Infrastructure.Persistence
{
    /// <summary>
    /// Seed de données de démonstration pour la recette et les tests fonctionnels.
    /// Activé uniquement si "SeedDemo:Enabled" = true dans appsettings.
    /// Idempotent : ne recrée jamais une donnée déjà présente (basé sur le matricule).
    /// </summary>
    public static class SeedDonneesDemo
    {
        // Secret local obligatoire: ne jamais stocker le mot de passe de demo dans le depot.
        private const string DemoPasswordConfigKey = "SeedDemo:Password";

        public static async Task ExecuterAsync(ApplicationDbContext db, IConfiguration configuration)
        {
            Log.Information("🌱 Vérification du seed de démonstration...");
            var motDePasseDemo = configuration[DemoPasswordConfigKey];
            if (string.IsNullOrWhiteSpace(motDePasseDemo))
            {
                Log.Warning(
                    "Seed de demonstration ignore : configurez {ConfigKey} via user-secrets ou variable d'environnement SeedDemo__Password.",
                    DemoPasswordConfigKey);
                return;
            }

            // ── Directions ──────────────────────────────────────────────────
            var dirDSI  = await UpsertDirectionAsync(db, "DSI",  "Direction des Systèmes d'Information");
            var dirFin  = await UpsertDirectionAsync(db, "FIN",  "Direction Financière");
            var dirOps  = await UpsertDirectionAsync(db, "OPS",  "Direction des Opérations");
            var dirRH   = await UpsertDirectionAsync(db, "RH",   "Direction des Ressources Humaines");
            var dirCom  = await UpsertDirectionAsync(db, "COM",  "Direction Commerciale");
            await db.SaveChangesAsync();

            // ── Utilisateurs & rôles ─────────────────────────────────────────
            var dsi  = await UpsertUtilisateurAsync(db, "DSI001",  "Koffi",   "Paul",     "paul.koffi@cit.ci",     dirDSI.Id,  RoleUtilisateur.DSI, motDePasseDemo);
            var rsi  = await UpsertUtilisateurAsync(db, "RSI001",  "Bamba",   "Seydou",   "seydou.bamba@cit.ci",   dirDSI.Id,  RoleUtilisateur.ResponsableSolutionsIT, motDePasseDemo);
            var cp1  = await UpsertUtilisateurAsync(db, "CP001",   "Diallo",  "Fatou",    "fatou.diallo@cit.ci",   dirDSI.Id,  RoleUtilisateur.ChefDeProjet, motDePasseDemo);
            var cp2  = await UpsertUtilisateurAsync(db, "CP002",   "Touré",   "Ibrahim",  "ibrahim.toure@cit.ci",  dirDSI.Id,  RoleUtilisateur.ChefDeProjet, motDePasseDemo);
            var dm1  = await UpsertUtilisateurAsync(db, "DM001",   "Yao",     "Marie",    "marie.yao@cit.ci",      dirFin.Id,  RoleUtilisateur.DirecteurMetier, motDePasseDemo);
            var dm2  = await UpsertUtilisateurAsync(db, "DM002",   "Kouamé",  "Serge",    "serge.kouame@cit.ci",   dirOps.Id,  RoleUtilisateur.DirecteurMetier, motDePasseDemo);
            var dm3  = await UpsertUtilisateurAsync(db, "DM003",   "Assi",    "Blanche",  "blanche.assi@cit.ci",   dirRH.Id,   RoleUtilisateur.DirecteurMetier, motDePasseDemo);
            var dem1 = await UpsertUtilisateurAsync(db, "DEM001",  "Kouassi", "Jean",     "jean.kouassi@cit.ci",   dirFin.Id,  RoleUtilisateur.Demandeur, motDePasseDemo);
            var dem2 = await UpsertUtilisateurAsync(db, "DEM002",  "Traoré",  "Aminata",  "aminata.traore@cit.ci", dirOps.Id,  RoleUtilisateur.Demandeur, motDePasseDemo);
            var dem3 = await UpsertUtilisateurAsync(db, "DEM003",  "Fofana",  "Moussa",   "moussa.fofana@cit.ci",  dirRH.Id,   RoleUtilisateur.Demandeur, motDePasseDemo);
            var dem4 = await UpsertUtilisateurAsync(db, "DEM004",  "Aka",     "Cécile",   "cecile.aka@cit.ci",     dirCom.Id,  RoleUtilisateur.Demandeur, motDePasseDemo);
            await db.SaveChangesAsync();

            Log.Information("✅ Utilisateurs de démonstration créés/vérifiés ({Count} comptes)", 11);

            // ── Portefeuille ─────────────────────────────────────────────────
            var portefeuille = await UpsertPortefeuilleAsync(db);
            await db.SaveChangesAsync();

            // ── Demandes et projets ──────────────────────────────────────────
            await UpsertDemandeEtProjetAsync(db, portefeuille.Id,
                matriculeDemandeur: "DEM001", matriculeDM: "DM001", matriculeCP: "CP001",
                titre: "Système de gestion des congés",
                description: "Mise en place d'un outil RH pour la gestion des demandes de congés et absences.",
                objectifs: "Automatiser la saisie, l'approbation et le suivi des congés pour tous les employés CIT.",
                statut: StatutDemande.ValideeParDSI,
                phaseProjet: PhaseProjet.AnalyseClarification,
                statutProjet: StatutProjet.NonDemarre,
                codeProjet: "PROJ-CONGES-001");

            await UpsertDemandeEtProjetAsync(db, portefeuille.Id,
                matriculeDemandeur: "DEM002", matriculeDM: "DM002", matriculeCP: "CP001",
                titre: "Refonte du portail intranet",
                description: "Modernisation du portail intranet CIT avec une interface responsive et des fonctionnalités collaboratives.",
                objectifs: "Améliorer l'expérience employé et centraliser les outils de communication interne.",
                statut: StatutDemande.ValideeParDSI,
                phaseProjet: PhaseProjet.PlanificationValidation,
                statutProjet: StatutProjet.EnCours,
                codeProjet: "PROJ-INTRANET-002");

            await UpsertDemandeEtProjetAsync(db, portefeuille.Id,
                matriculeDemandeur: "DEM003", matriculeDM: "DM003", matriculeCP: "CP002",
                titre: "Application mobile de réservation de bus",
                description: "Développement d'une application mobile permettant aux employés de réserver leur place dans les bus navettes.",
                objectifs: "Optimiser la gestion des navettes et améliorer la mobilité des collaborateurs.",
                statut: StatutDemande.ValideeParDSI,
                phaseProjet: PhaseProjet.ExecutionSuivi,
                statutProjet: StatutProjet.EnCours,
                codeProjet: "PROJ-BUS-003");

            await UpsertDemandeEtProjetAsync(db, portefeuille.Id,
                matriculeDemandeur: "DEM004", matriculeDM: "DM001", matriculeCP: "CP002",
                titre: "Tableau de bord commercial",
                description: "Mise en place d'un tableau de bord pour le suivi en temps réel des indicateurs commerciaux.",
                objectifs: "Donner une visibilité temps réel sur les KPI commerciaux pour une meilleure prise de décision.",
                statut: StatutDemande.EnAttenteValidationDSI,
                phaseProjet: null,
                statutProjet: null,
                codeProjet: null);

            // Demandes en cours de validation (pour tester le workflow)
            await UpsertDemandeBrouillonAsync(db,
                matriculeDemandeur: "DEM001", matriculeDM: "DM001",
                titre: "Migration base de données Oracle vers SQL Server",
                description: "Migration de la base Oracle legacy vers SQL Server pour réduire les coûts de licence.",
                statut: StatutDemande.Brouillon);

            await UpsertDemandeBrouillonAsync(db,
                matriculeDemandeur: "DEM002", matriculeDM: "DM002",
                titre: "Système de ticketing IT",
                description: "Implémentation d'un système de gestion des incidents et demandes IT (ITSM).",
                statut: StatutDemande.EnAttenteValidationDirecteurMetier);

            await NormaliserProjetsNonDemarresAsync(db);
            await db.SaveChangesAsync();
            Log.Information("✅ Demandes et projets de démonstration créés/vérifiés");
            Log.Information("📋 Comptes testeurs disponibles. Mot de passe fourni via {ConfigKey}.", DemoPasswordConfigKey);
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS PRIVÉS
        // ════════════════════════════════════════════════════════════════════

        private static async Task<Direction> UpsertDirectionAsync(
            ApplicationDbContext db, string code, string libelle)
        {
            var existing = await db.Directions.FirstOrDefaultAsync(d => d.Code == code && !d.EstSupprime);
            if (existing != null) return existing;

            var dir = new Direction
            {
                Id          = Guid.NewGuid(),
                Code        = code,
                Libelle     = libelle,
                EstActive   = true,
                DateCreation = DateTime.UtcNow,
                CreePar     = "SEED_DEMO",
                EstSupprime = false
            };
            db.Directions.Add(dir);
            return dir;
        }

        private static async Task<Utilisateur> UpsertUtilisateurAsync(
            ApplicationDbContext db, string matricule, string nom, string prenoms,
            string email, Guid directionId, RoleUtilisateur role, string motDePasseDemo)
        {
            var existing = await db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Matricule == matricule && !u.EstSupprime);

            if (existing != null)
            {
                // Vérifier que le rôle est bien assigné
                if (!existing.UtilisateurRoles.Any(r => r.Role == role && !r.EstSupprime))
                {
                    db.UtilisateurRoles.Add(new UtilisateurRole
                    {
                        Id           = Guid.NewGuid(),
                        UtilisateurId = existing.Id,
                        Role         = role,
                        DateDebut    = DateTime.UtcNow,
                        DateCreation = DateTime.UtcNow,
                        CreePar      = "SEED_DEMO",
                        EstSupprime  = false
                    });
                }
                return existing;
            }

            var userId = Guid.NewGuid();
            var user = new Utilisateur
            {
                Id           = userId,
                Matricule    = matricule,
                Nom          = nom,
                Prenoms      = prenoms,
                Email        = email,
                DirectionId  = directionId,
                MotDePasse   = BCrypt.Net.BCrypt.HashPassword(motDePasseDemo),
                NombreConnexion = 0,
                DateCreation = DateTime.UtcNow,
                CreePar      = "SEED_DEMO",
                EstSupprime  = false
            };
            db.Utilisateurs.Add(user);
            db.UtilisateurRoles.Add(new UtilisateurRole
            {
                Id           = Guid.NewGuid(),
                UtilisateurId = userId,
                Role         = role,
                DateDebut    = DateTime.UtcNow,
                DateCreation = DateTime.UtcNow,
                CreePar      = "SEED_DEMO",
                EstSupprime  = false
            });
            return user;
        }

        private static async Task<PortefeuilleProjet> UpsertPortefeuilleAsync(ApplicationDbContext db)
        {
            var existing = await db.PortefeuillesProjets.FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);
            if (existing != null) return existing;

            var p = new PortefeuilleProjet
            {
                Id                        = Guid.NewGuid(),
                Nom                       = "Portefeuille Projets DSI — CIT 2024/2025",
                ObjectifStrategiqueGlobal = "Assurer l'amélioration globale de l'efficacité opérationnelle et de la satisfaction des parties prenantes au Côte d'Ivoire Terminal.",
                AvantagesAttendus         = "• Digitalisation des processus RH et opérationnels\n• Réduction des coûts d'infrastructure IT\n• Amélioration de l'expérience collaborateur\n• Visibilité temps réel sur les KPI de l'entreprise",
                RisquesEtMitigations      = "• Résistance au changement : formation et accompagnement\n• Retards de livraison : suivi rapproché et jalons hebdomadaires\n• Risques sécurité : revue de sécurité sur chaque projet",
                EstActif                  = true,
                DateCreation              = DateTime.UtcNow,
                CreePar                   = "SEED_DEMO",
                EstSupprime               = false
            };
            db.PortefeuillesProjets.Add(p);
            return p;
        }

        private static async Task UpsertDemandeEtProjetAsync(
            ApplicationDbContext db,
            Guid portefeuilleId,
            string matriculeDemandeur,
            string matriculeDM,
            string? matriculeCP,
            string titre,
            string description,
            string objectifs,
            StatutDemande statut,
            PhaseProjet? phaseProjet,
            StatutProjet? statutProjet,
            string? codeProjet)
        {
            // Idempotent : ne pas recréer si déjà présent
            if (await db.DemandesProjets.AnyAsync(d => d.Titre == titre && !d.EstSupprime))
                return;

            var demandeur = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Matricule == matriculeDemandeur);
            var dm        = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Matricule == matriculeDM);
            var cp        = matriculeCP != null
                ? await db.Utilisateurs.FirstOrDefaultAsync(u => u.Matricule == matriculeCP)
                : null;

            if (demandeur == null || dm == null) return;

            var demandeId = Guid.NewGuid();
            var demande = new DemandeProjet
            {
                Id                = demandeId,
                Titre             = titre,
                Description       = description,
                Contexte          = $"Ce projet s'inscrit dans la transformation digitale de CIT.",
                Objectifs         = objectifs,
                AvantagesAttendus = "Gains de productivité, réduction des erreurs, meilleure traçabilité.",
                Perimetre         = "Ensemble des collaborateurs CIT.",
                Urgence           = UrgenceProjet.Moyenne,
                Criticite         = CriticiteProjet.Moyenne,
                DateMiseEnOeuvreSouhaitee = DateTime.UtcNow.AddMonths(6),
                DemandeurId       = demandeur.Id,
                DirectionId       = demandeur.DirectionId,
                DirecteurMetierId = dm.Id,
                StatutDemande     = statut,
                DateSoumission    = DateTime.UtcNow.AddDays(-30),
                DateValidationDM  = statut >= StatutDemande.EnAttenteValidationDSI ? DateTime.UtcNow.AddDays(-20) : null,
                DateValidationDSI = statut == StatutDemande.ValideeParDSI ? DateTime.UtcNow.AddDays(-10) : null,
                DateCreation      = DateTime.UtcNow.AddDays(-35),
                CreePar           = "SEED_DEMO",
                EstSupprime       = false
            };
            db.DemandesProjets.Add(demande);

            // Créer le projet si la demande est validée DSI
            if (statut == StatutDemande.ValideeParDSI && phaseProjet.HasValue && statutProjet.HasValue && codeProjet != null)
            {
                var projet = new Projet
                {
                    Id                   = Guid.NewGuid(),
                    CodeProjet           = codeProjet,
                    Titre                = titre,
                    Objectif             = objectifs,
                    PortefeuilleProjetId = portefeuilleId,
                    DemandeProjetId      = demandeId,
                    DirectionId          = demandeur.DirectionId,
                    SponsorId            = dm.Id,
                    ChefProjetId         = cp?.Id,
                    StatutProjet         = statutProjet.Value,
                    PhaseActuelle        = phaseProjet.Value,
                    EtatProjet           = EtatProjet.Vert,
                    PourcentageAvancement = CalculerPourcentageDemo(phaseProjet.Value, statutProjet.Value),
                    DateDebut            = statutProjet.Value == StatutProjet.EnCours ? DateTime.UtcNow.AddDays(-15) : null,
                    BilanCloture         = string.Empty,
                    LeconsApprises       = string.Empty,
                    DateCreation         = DateTime.UtcNow.AddDays(-10),
                    CreePar              = "SEED_DEMO",
                    EstSupprime          = false
                };
                db.Projets.Add(projet);
            }
        }

        private static int CalculerPourcentageDemo(PhaseProjet phaseProjet, StatutProjet statutProjet)
        {
            if (statutProjet == StatutProjet.NonDemarre)
                return 0;

            if (statutProjet == StatutProjet.Cloture)
                return 100;

            if (statutProjet == StatutProjet.Annule)
                return 0;

            return phaseProjet switch
            {
                PhaseProjet.AnalyseClarification => 10,
                PhaseProjet.PlanificationValidation => 25,
                PhaseProjet.ExecutionSuivi => 55,
                PhaseProjet.UatMep => 80,
                PhaseProjet.ClotureLeconsApprises => 95,
                _ => 0
            };
        }

        private static async Task NormaliserProjetsNonDemarresAsync(ApplicationDbContext db)
        {
            var projets = await db.Projets
                .Where(p => !p.EstSupprime &&
                            p.StatutProjet == StatutProjet.NonDemarre &&
                            (p.PourcentageAvancement != 0 || p.DateDebut != null))
                .ToListAsync();

            foreach (var projet in projets)
            {
                projet.PourcentageAvancement = 0;
                projet.DateDebut = null;
                projet.DateModification = DateTime.UtcNow;
                projet.ModifiePar = "SEED_DEMO_NORMALISATION";
            }
        }

        private static async Task UpsertDemandeBrouillonAsync(
            ApplicationDbContext db,
            string matriculeDemandeur,
            string matriculeDM,
            string titre,
            string description,
            StatutDemande statut)
        {
            if (await db.DemandesProjets.AnyAsync(d => d.Titre == titre && !d.EstSupprime))
                return;

            var demandeur = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Matricule == matriculeDemandeur);
            var dm        = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Matricule == matriculeDM);
            if (demandeur == null || dm == null) return;

            db.DemandesProjets.Add(new DemandeProjet
            {
                Id                = Guid.NewGuid(),
                Titre             = titre,
                Description       = description,
                Contexte          = "Contexte opérationnel CIT.",
                Objectifs         = "À préciser lors de la soumission.",
                Urgence           = UrgenceProjet.Basse,
                Criticite         = CriticiteProjet.Faible,
                DemandeurId       = demandeur.Id,
                DirectionId       = demandeur.DirectionId,
                DirecteurMetierId = dm.Id,
                StatutDemande     = statut,
                DateSoumission    = statut == StatutDemande.Brouillon ? DateTime.UtcNow : DateTime.UtcNow.AddDays(-5),
                DateCreation      = DateTime.UtcNow.AddDays(-7),
                CreePar           = "SEED_DEMO",
                EstSupprime       = false
            });
        }
    }
}
