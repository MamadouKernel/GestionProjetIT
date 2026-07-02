using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GestionProjects.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.CommandTimeout(30).MigrationsAssembly("GestionProjects")));

        return services;
    }

    /// <summary>
    /// Applique les migrations EF Core puis les patches SQL de compatibilité.
    /// Les patches sont idempotents (IF NOT EXISTS) et nécessaires pour les
    /// environnements dont la migration initiale a partiellement échoué.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!db.Database.IsRelational())
        {
            Log.Information("Provider EF non relationnel detecte : migrations et patches SQL ignores.");
            return;
        }

        db.Database.Migrate();

        ApplyCompatibilityPatches(db);
        await EnsureAdminUserExistsAsync(db, scope.ServiceProvider.GetRequiredService<IConfiguration>());
    }

    // ── Patches SQL de compatibilité ──────────────────────────────────────────
    // Nécessaires quand la migration EF Core a échoué sur certains environnements.
    // Tous les blocs sont idempotents (IF NOT EXISTS).
    private static void ApplyCompatibilityPatches(ApplicationDbContext db)
    {
        ExecutePatch(db, "AvenantsProjets", @"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('AvenantsProjets') AND type='U')
            BEGIN
                CREATE TABLE [AvenantsProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [Numero] int NOT NULL,
                    [Type] int NOT NULL,
                    [Titre] nvarchar(4000) NOT NULL,
                    [Justification] nvarchar(max) NOT NULL,
                    [DescriptionPerimetre] nvarchar(max) NULL,
                    [AncienBudget] decimal(18,2) NULL,
                    [NouveauBudget] decimal(18,2) NULL,
                    [AncienneDateFinPrevue] datetime2 NULL,
                    [NouvelleDateFinPrevue] datetime2 NULL,
                    [Statut] int NOT NULL,
                    [DemandeParId] uniqueidentifier NOT NULL,
                    [DateDemande] datetime2 NOT NULL,
                    [ValideParDMId] uniqueidentifier NULL,
                    [DateValidationDM] datetime2 NULL,
                    [ValideParDSIId] uniqueidentifier NULL,
                    [DateValidationDSI] datetime2 NULL,
                    [CommentaireRejet] nvarchar(max) NOT NULL,
                    [DateApplication] datetime2 NULL,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(4000) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(4000) NULL,
                    [EstSupprime] bit NOT NULL,
                    CONSTRAINT [PK_AvenantsProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AvenantsProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_AvenantsProjets_Utilisateurs_DemandePar] FOREIGN KEY ([DemandeParId]) REFERENCES [Utilisateurs]([Id]),
                    CONSTRAINT [FK_AvenantsProjets_Utilisateurs_ValideParDM] FOREIGN KEY ([ValideParDMId]) REFERENCES [Utilisateurs]([Id]),
                    CONSTRAINT [FK_AvenantsProjets_Utilisateurs_ValideParDSI] FOREIGN KEY ([ValideParDSIId]) REFERENCES [Utilisateurs]([Id])
                );
                CREATE INDEX [IX_AvenantsProjets_ProjetId] ON [AvenantsProjets]([ProjetId]);
                CREATE INDEX [IX_AvenantsProjets_DemandeParId] ON [AvenantsProjets]([DemandeParId]);
                CREATE INDEX [IX_AvenantsProjets_ValideParDMId] ON [AvenantsProjets]([ValideParDMId]);
                CREATE INDEX [IX_AvenantsProjets_ValideParDSIId] ON [AvenantsProjets]([ValideParDSIId]);
            END
        ");

        ExecutePatch(db, "SuspensionProjet", @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='MotifSuspension')
                ALTER TABLE [Projets] ADD [MotifSuspension] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='DateSuspension')
                ALTER TABLE [Projets] ADD [DateSuspension] datetime2 NULL;
        ");

        ExecutePatch(db, "BaselineProjet", @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='DateBaseline')
                ALTER TABLE [Projets] ADD [DateBaseline] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='DateFinPrevueBaseline')
                ALTER TABLE [Projets] ADD [DateFinPrevueBaseline] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BudgetBaseline')
                ALTER TABLE [Projets] ADD [BudgetBaseline] decimal(18,2) NULL;
        ");

        ExecutePatch(db, "ValidationDmDemandeAcces", @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesAccesAzureAd') AND name='ValideeParDmId')
                ALTER TABLE [DemandesAccesAzureAd] ADD [ValideeParDmId] uniqueidentifier NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesAccesAzureAd') AND name='DateValidationDm')
                ALTER TABLE [DemandesAccesAzureAd] ADD [DateValidationDm] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesAccesAzureAd') AND name='CommentaireDm')
                ALTER TABLE [DemandesAccesAzureAd] ADD [CommentaireDm] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesAccesAzureAd') AND name='RoleConfirmeParDm')
                ALTER TABLE [DemandesAccesAzureAd] ADD [RoleConfirmeParDm] nvarchar(4000) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_DemandesAccesAzureAd_Utilisateurs_ValideeParDmId')
                ALTER TABLE [DemandesAccesAzureAd]
                    ADD CONSTRAINT [FK_DemandesAccesAzureAd_Utilisateurs_ValideeParDmId]
                    FOREIGN KEY ([ValideeParDmId]) REFERENCES [Utilisateurs]([Id]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_DemandesAccesAzureAd_ValideeParDmId' AND object_id=OBJECT_ID('DemandesAccesAzureAd'))
                CREATE INDEX [IX_DemandesAccesAzureAd_ValideeParDmId] ON [DemandesAccesAzureAd]([ValideeParDmId]);
        ");

        ExecutePatch(db, "BeneficesProjets", @"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('BeneficesProjets') AND type='U')
            BEGIN
                CREATE TABLE [BeneficesProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [Libelle] nvarchar(4000) NOT NULL,
                    [Indicateur] nvarchar(4000) NOT NULL,
                    [ValeurCible] nvarchar(4000) NOT NULL,
                    [DateCibleRealisation] datetime2 NULL,
                    [Statut] int NOT NULL,
                    [ValeurRealisee] nvarchar(4000) NULL,
                    [DateRevue] datetime2 NULL,
                    [CommentaireRevue] nvarchar(max) NULL,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(4000) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(4000) NULL,
                    [EstSupprime] bit NOT NULL,
                    CONSTRAINT [PK_BeneficesProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_BeneficesProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_BeneficesProjets_ProjetId] ON [BeneficesProjets]([ProjetId]);
            END
        ");

        ExecutePatch(db, "EvaluationsMembresProjets", @"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('EvaluationsMembresProjets') AND type='U')
            BEGIN
                CREATE TABLE [EvaluationsMembresProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [MembreProjetId] uniqueidentifier NOT NULL,
                    [EvaluateurId] uniqueidentifier NOT NULL,
                    [DateEvaluation] datetime2 NOT NULL,
                    [NoteQualite] int NOT NULL,
                    [NoteRespectDelais] int NOT NULL,
                    [NoteCollaboration] int NOT NULL,
                    [Commentaire] nvarchar(max) NULL,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(4000) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(4000) NULL,
                    [EstSupprime] bit NOT NULL,
                    CONSTRAINT [PK_EvaluationsMembresProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_EvaluationsMembresProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_EvaluationsMembresProjets_MembresProjets] FOREIGN KEY ([MembreProjetId]) REFERENCES [MembresProjets]([Id]),
                    CONSTRAINT [FK_EvaluationsMembresProjets_Utilisateurs] FOREIGN KEY ([EvaluateurId]) REFERENCES [Utilisateurs]([Id])
                );
                CREATE INDEX [IX_EvaluationsMembresProjets_ProjetId] ON [EvaluationsMembresProjets]([ProjetId]);
                CREATE UNIQUE INDEX [IX_EvaluationsMembresProjets_ProjetId_MembreProjetId] ON [EvaluationsMembresProjets]([ProjetId], [MembreProjetId]) WHERE [EstSupprime] = 0;
            END
        ");

        // Les patches SignaturesCharte, DemandeCreationCompte, ProfilRessource/Bilan,
        // ComplementPhases/ChargesWorkflow et PlanificationNative ont ete retires le
        // 02/07/2026 : leurs migrations EF respectives sont desormais marquees
        // appliquees (dev + prod), donc Migrate() les couvre nativement.
    }

    private static void ExecutePatch(ApplicationDbContext db, string name, string sql)
    {
        try { db.Database.ExecuteSqlRaw(sql); }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch SQL '{PatchName}' en echec", name);
            throw;
        }
    }

    // ── Bootstrap admin ───────────────────────────────────────────────────────
    private static async Task EnsureAdminUserExistsAsync(ApplicationDbContext db, IConfiguration config)
    {
        if (db.Utilisateurs.Any()) return;

        try
        {
            var password = GenerateSecurePassword();
            var hash     = BCrypt.Net.BCrypt.HashPassword(password);
            var adminId  = Guid.NewGuid();

            db.Utilisateurs.Add(new Utilisateur
            {
                Id = adminId, Matricule = "admin", MotDePasse = hash,
                Nom = "Administrateur", Prenoms = "DSI", Email = "admin@cit.ci",
                DateCreation = DateTime.Now, CreePar = "SYSTEM",
                ModifiePar = string.Empty, EstSupprime = false, NombreConnexion = 0
            });

            db.UtilisateurRoles.Add(new UtilisateurRole
            {
                Id = Guid.NewGuid(), UtilisateurId = adminId,
                Role = RoleUtilisateur.AdminIT,
                DateDebut = DateTime.Now, DateCreation = DateTime.Now,
                CreePar = "SYSTEM", EstSupprime = false
            });

            await db.SaveChangesAsync();
            Log.Information("Utilisateur admin créé. Matricule: admin");

            if (config.GetValue<bool>("Security:DisplayBootstrapPasswordsInConsole"))
            {
                Console.WriteLine("=================================================");
                Console.WriteLine("MOT DE PASSE ADMIN TEMPORAIRE : " + password);
                Console.WriteLine("Changez ce mot de passe dès la première connexion.");
                Console.WriteLine("=================================================");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur lors de la création de l'utilisateur admin");
            throw;
        }
    }

    private static string GenerateSecurePassword()
    {
        const string upper   = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower   = "abcdefghijklmnopqrstuvwxyz";
        const string digits  = "0123456789";
        const string special = "@#$%&*!";
        const string all     = upper + lower + digits + special;

        var pw = new System.Text.StringBuilder();
        pw.Append(upper[Rnd(upper.Length)]);
        pw.Append(lower[Rnd(lower.Length)]);
        pw.Append(digits[Rnd(digits.Length)]);
        pw.Append(special[Rnd(special.Length)]);
        for (var i = 0; i < 8; i++) pw.Append(all[Rnd(all.Length)]);

        var chars = pw.ToString().ToCharArray();
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = Rnd(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    private static int Rnd(int max) =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(max);
}
