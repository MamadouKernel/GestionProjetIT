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
                sql => sql.CommandTimeout(30)));

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

        ExecutePatch(db, "SignaturesCharte", @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='SignatureImageCP')
                ALTER TABLE [CharteProjets] ADD [SignatureImageCP] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='SignatureImageDSI')
                ALTER TABLE [CharteProjets] ADD [SignatureImageDSI] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='SignatureImageSponsor')
                ALTER TABLE [CharteProjets] ADD [SignatureImageSponsor] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='DateSignatureImageCP')
                ALTER TABLE [CharteProjets] ADD [DateSignatureImageCP] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='DateSignatureImageDSI')
                ALTER TABLE [CharteProjets] ADD [DateSignatureImageDSI] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='DateSignatureImageSponsor')
                ALTER TABLE [CharteProjets] ADD [DateSignatureImageSponsor] datetime2 NULL;

            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260514065233_AddSignaturesElectroniquesCharte')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260514065233_AddSignaturesElectroniquesCharte','9.0.11');
        ");

        ExecutePatch(db, "DemandeCreationCompte", @"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('DemandesCreationCompte') AND type='U')
            BEGIN
                CREATE TABLE [DemandesCreationCompte] (
                    [Id] uniqueidentifier NOT NULL,
                    [Nom] nvarchar(max) NOT NULL,
                    [Prenoms] nvarchar(max) NOT NULL,
                    [Email] nvarchar(max) NOT NULL,
                    [Service] nvarchar(max) NOT NULL,
                    [DirectionId] uniqueidentifier NULL,
                    [DirecteurMetierId] uniqueidentifier NULL,
                    [Statut] int NOT NULL,
                    [CommentaireDM] nvarchar(max) NULL,
                    [CommentaireDSI] nvarchar(max) NULL,
                    [DateSoumission] datetime2 NOT NULL,
                    [UtilisateurCreePar] uniqueidentifier NULL,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL,
                    CONSTRAINT [PK_DemandesCreationCompte] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_DemandesCreationCompte_Directions] FOREIGN KEY ([DirectionId]) REFERENCES [Directions]([Id]),
                    CONSTRAINT [FK_DemandesCreationCompte_Utilisateurs] FOREIGN KEY ([DirecteurMetierId]) REFERENCES [Utilisateurs]([Id])
                );
                CREATE INDEX [IX_DemandesCreationCompte_DirecteurMetierId] ON [DemandesCreationCompte]([DirecteurMetierId]);
                CREATE INDEX [IX_DemandesCreationCompte_DirectionId] ON [DemandesCreationCompte]([DirectionId]);
            END

            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260514084311_AddDemandeCreationCompte')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260514084311_AddDemandeCreationCompte','9.0.0');
        ");

        ExecutePatch(db, "ProfilRessource/Bilan", @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Utilisateurs') AND name='ProfilRessource')
                ALTER TABLE [Utilisateurs] ADD [ProfilRessource] int NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanPerimetre')
                ALTER TABLE [Projets] ADD [BilanPerimetre] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanPlanning')
                ALTER TABLE [Projets] ADD [BilanPlanning] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanBudget')
                ALTER TABLE [Projets] ADD [BilanBudget] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanDifficultes')
                ALTER TABLE [Projets] ADD [BilanDifficultes] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanReussites')
                ALTER TABLE [Projets] ADD [BilanReussites] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='LeconsReussites')
                ALTER TABLE [Projets] ADD [LeconsReussites] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='LeconsEchecs')
                ALTER TABLE [Projets] ADD [LeconsEchecs] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='LeconsRecommandations')
                ALTER TABLE [Projets] ADD [LeconsRecommandations] nvarchar(max) NULL;
            ALTER TABLE [Projets] ALTER COLUMN [BilanCloture] nvarchar(max) NULL;
            ALTER TABLE [Projets] ALTER COLUMN [LeconsApprises] nvarchar(max) NULL;

            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260514092743_AddStructuredBilanAndProfilRessource')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260514092743_AddStructuredBilanAndProfilRessource','9.0.11');
        ");

        ExecutePatch(db, "ComplementPhases/ChargesWorkflow", @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ActionsAVenirExecution')
                ALTER TABLE [FicheProjets] ADD [ActionsAVenirExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ActionsRealiseesExecution')
                ALTER TABLE [FicheProjets] ADD [ActionsRealiseesExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CanalCommunication')
                ALTER TABLE [FicheProjets] ADD [CanalCommunication] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ChangeRequis')
                ALTER TABLE [FicheProjets] ADD [ChangeRequis] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireAvancementExecution')
                ALTER TABLE [FicheProjets] ADD [CommentaireAvancementExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireBudgetPlanification')
                ALTER TABLE [FicheProjets] ADD [CommentaireBudgetPlanification] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireStatutFinal')
                ALTER TABLE [FicheProjets] ADD [CommentaireStatutFinal] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireValidationPlanification')
                ALTER TABLE [FicheProjets] ADD [CommentaireValidationPlanification] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CopilPrevu')
                ALTER TABLE [FicheProjets] ADD [CopilPrevu] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateDebutRecette')
                ALTER TABLE [FicheProjets] ADD [DateDebutRecette] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateDebutReelleExecution')
                ALTER TABLE [FicheProjets] ADD [DateDebutReelleExecution] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateFinEstimeeExecution')
                ALTER TABLE [FicheProjets] ADD [DateFinEstimeeExecution] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateFinRecette')
                ALTER TABLE [FicheProjets] ADD [DateFinRecette] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateMepPrevue')
                ALTER TABLE [FicheProjets] ADD [DateMepPrevue] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DecisionsExecution')
                ALTER TABLE [FicheProjets] ADD [DecisionsExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DecoupageLotsTravail')
                ALTER TABLE [FicheProjets] ADD [DecoupageLotsTravail] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='FrequenceReunions')
                ALTER TABLE [FicheProjets] ADD [FrequenceReunions] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='HypercareTermine')
                ALTER TABLE [FicheProjets] ADD [HypercareTermine] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='IncidentsMep')
                ALTER TABLE [FicheProjets] ADD [IncidentsMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='IncidentsPostMep')
                ALTER TABLE [FicheProjets] ADD [IncidentsPostMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='JalonsPrincipaux')
                ALTER TABLE [FicheProjets] ADD [JalonsPrincipaux] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='JustificationBudgetExecution')
                ALTER TABLE [FicheProjets] ADD [JustificationBudgetExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='JustificationRetardExecution')
                ALTER TABLE [FicheProjets] ADD [JustificationRetardExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ParticipantsReunions')
                ALTER TABLE [FicheProjets] ADD [ParticipantsReunions] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PerimetreTeste')
                ALTER TABLE [FicheProjets] ADD [PerimetreTeste] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PeriodeHypercare')
                ALTER TABLE [FicheProjets] ADD [PeriodeHypercare] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PlanMep')
                ALTER TABLE [FicheProjets] ADD [PlanMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PlanRollback')
                ALTER TABLE [FicheProjets] ADD [PlanRollback] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PlanificationRessources')
                ALTER TABLE [FicheProjets] ADD [PlanificationRessources] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PrerequisMep')
                ALTER TABLE [FicheProjets] ADD [PrerequisMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ProblemesBlocagesExecution')
                ALTER TABLE [FicheProjets] ADD [ProblemesBlocagesExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='RaciParActivite')
                ALTER TABLE [FicheProjets] ADD [RaciParActivite] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ReferenceChange')
                ALTER TABLE [FicheProjets] ADD [ReferenceChange] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ResultatMep')
                ALTER TABLE [FicheProjets] ADD [ResultatMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='StatutFinalCloture')
                ALTER TABLE [FicheProjets] ADD [StatutFinalCloture] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='StatutHypercare')
                ALTER TABLE [FicheProjets] ADD [StatutHypercare] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='StatutValidationChange')
                ALTER TABLE [FicheProjets] ADD [StatutValidationChange] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='SyntheseChargesExecution')
                ALTER TABLE [FicheProjets] ADD [SyntheseChargesExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunAcces')
                ALTER TABLE [FicheProjets] ADD [TransfertRunAcces] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunDocumentation')
                ALTER TABLE [FicheProjets] ADD [TransfertRunDocumentation] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunExploitationPrete')
                ALTER TABLE [FicheProjets] ADD [TransfertRunExploitationPrete] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunSupportInforme')
                ALTER TABLE [FicheProjets] ADD [TransfertRunSupportInforme] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='UtilisateursTesteurs')
                ALTER TABLE [FicheProjets] ADD [UtilisateursTesteurs] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesProjets') AND name='AutreSponsorId')
                ALTER TABLE [DemandesProjets] ADD [AutreSponsorId] uniqueidentifier NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesClotureProjets') AND name='CommentaireInitiateur')
                ALTER TABLE [DemandesClotureProjets] ADD [CommentaireInitiateur] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='ValideeParId')
                ALTER TABLE [ChargesProjets] ADD [ValideeParId] uniqueidentifier NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='Activite')
                ALTER TABLE [ChargesProjets] ADD [Activite] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='TypeActivite')
                ALTER TABLE [ChargesProjets] ADD [TypeActivite] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='CommentaireValidation')
                ALTER TABLE [ChargesProjets] ADD [CommentaireValidation] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='DateSoumissionValidation')
                ALTER TABLE [ChargesProjets] ADD [DateSoumissionValidation] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='DateValidation')
                ALTER TABLE [ChargesProjets] ADD [DateValidation] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='StatutValidation')
                ALTER TABLE [ChargesProjets] ADD [StatutValidation] int NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('AnomaliesProjets') AND name='CasTestProjetId')
                ALTER TABLE [AnomaliesProjets] ADD [CasTestProjetId] uniqueidentifier NULL;

            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260519135253_AddComplementPhaseAndChargeWorkflowFields')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ActionsAVenirExecution')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='ValideeParId')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260519135253_AddComplementPhaseAndChargeWorkflowFields','9.0.11');
        ");

        ExecutePatch(db, "PlanificationNative", @"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('TachesPlanningProjets') AND type='U')
            BEGIN
                CREATE TABLE [TachesPlanningProjets] (
                    [Id] uniqueidentifier NOT NULL, [ProjetId] uniqueidentifier NOT NULL,
                    [CodeWbs] nvarchar(64) NOT NULL DEFAULT N'', [Libelle] nvarchar(256) NOT NULL DEFAULT N'',
                    [Responsable] nvarchar(256) NOT NULL DEFAULT N'', [Dependances] nvarchar(max) NOT NULL DEFAULT N'',
                    [Commentaire] nvarchar(max) NOT NULL DEFAULT N'',
                    [DateDebutPrevue] datetime2 NOT NULL, [DateFinPrevue] datetime2 NOT NULL,
                    [Avancement] int NOT NULL DEFAULT 0, [Ordre] int NOT NULL DEFAULT 0,
                    [EstJalon] bit NOT NULL DEFAULT CAST(0 AS bit),
                    [DateCreation] datetime2 NOT NULL, [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL, [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_TachesPlanningProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_TachesPlanningProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_TachesPlanningProjets_ProjetId_Ordre] ON [TachesPlanningProjets]([ProjetId], [Ordre]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('LignesRaciProjets') AND type='U')
            BEGIN
                CREATE TABLE [LignesRaciProjets] (
                    [Id] uniqueidentifier NOT NULL, [ProjetId] uniqueidentifier NOT NULL,
                    [CodeActivite] nvarchar(64) NOT NULL DEFAULT N'', [Activite] nvarchar(256) NOT NULL DEFAULT N'',
                    [Responsable] nvarchar(256) NOT NULL DEFAULT N'', [Approbateur] nvarchar(256) NOT NULL DEFAULT N'',
                    [Consulte] nvarchar(max) NOT NULL DEFAULT N'', [Informe] nvarchar(max) NOT NULL DEFAULT N'',
                    [Ordre] int NOT NULL DEFAULT 0,
                    [DateCreation] datetime2 NOT NULL, [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL, [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_LignesRaciProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LignesRaciProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LignesRaciProjets_ProjetId_Ordre] ON [LignesRaciProjets]([ProjetId], [Ordre]);
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LignesRaciProjets') AND name='CodeActivite')
                ALTER TABLE [LignesRaciProjets] ADD [CodeActivite] nvarchar(64) NOT NULL DEFAULT N'';

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('LignesCommunicationProjets') AND type='U')
            BEGIN
                CREATE TABLE [LignesCommunicationProjets] (
                    [Id] uniqueidentifier NOT NULL, [ProjetId] uniqueidentifier NOT NULL,
                    [Instance] nvarchar(256) NOT NULL DEFAULT N'', [Objectif] nvarchar(max) NOT NULL DEFAULT N'',
                    [Frequence] nvarchar(256) NOT NULL DEFAULT N'', [Canal] nvarchar(256) NOT NULL DEFAULT N'',
                    [Participants] nvarchar(max) NOT NULL DEFAULT N'', [Responsable] nvarchar(256) NOT NULL DEFAULT N'',
                    [EstCopil] bit NOT NULL DEFAULT CAST(0 AS bit), [Ordre] int NOT NULL DEFAULT 0,
                    [DateCreation] datetime2 NOT NULL, [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL, [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_LignesCommunicationProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LignesCommunicationProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LignesCommunicationProjets_ProjetId_Ordre] ON [LignesCommunicationProjets]([ProjetId], [Ordre]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('LignesBudgetPlanificationProjets') AND type='U')
            BEGIN
                CREATE TABLE [LignesBudgetPlanificationProjets] (
                    [Id] uniqueidentifier NOT NULL, [ProjetId] uniqueidentifier NOT NULL,
                    [Poste] nvarchar(256) NOT NULL DEFAULT N'', [Description] nvarchar(max) NOT NULL DEFAULT N'',
                    [Montant] decimal(18,2) NOT NULL DEFAULT 0, [Commentaire] nvarchar(max) NOT NULL DEFAULT N'',
                    [Ordre] int NOT NULL DEFAULT 0,
                    [DateCreation] datetime2 NOT NULL, [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL, [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_LignesBudgetPlanificationProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LignesBudgetPlanificationProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LignesBudgetPlanificationProjets_ProjetId_Ordre] ON [LignesBudgetPlanificationProjets]([ProjetId], [Ordre]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('PvKickOffProjets') AND type='U')
            BEGIN
                CREATE TABLE [PvKickOffProjets] (
                    [Id] uniqueidentifier NOT NULL, [ProjetId] uniqueidentifier NOT NULL,
                    [DateReunion] datetime2 NULL, [Heure] nvarchar(32) NOT NULL DEFAULT N'',
                    [Lieu] nvarchar(256) NOT NULL DEFAULT N'', [Animateur] nvarchar(256) NOT NULL DEFAULT N'',
                    [Objectifs] nvarchar(max) NOT NULL DEFAULT N'', [Participants] nvarchar(max) NOT NULL DEFAULT N'',
                    [OrdreDuJour] nvarchar(max) NOT NULL DEFAULT N'', [Decisions] nvarchar(max) NOT NULL DEFAULT N'',
                    [Actions] nvarchar(max) NOT NULL DEFAULT N'', [Commentaires] nvarchar(max) NOT NULL DEFAULT N'',
                    [DateCreation] datetime2 NOT NULL, [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL, [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_PvKickOffProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PvKickOffProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_PvKickOffProjets_ProjetId] ON [PvKickOffProjets]([ProjetId]);
            END
        ");
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
