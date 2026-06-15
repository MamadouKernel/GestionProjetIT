/* ============================================================================
   Patch schéma PROD — nouvelles features (avenants, bénéfices, suspension, baseline)
   ----------------------------------------------------------------------------
   À exécuter sur la base PROD `zeinab`, APRÈS un backup.
   Idempotent (IF NOT EXISTS) : rejouable sans risque.
   Reproduit exactement ce que fait ApplyCompatibilityPatches au boot — nécessaire
   car en prod Database:ApplyMigrationsOnStartup = false (les patches ne tournent pas).
   ============================================================================ */

USE [zeinab];
GO

/* 1. Colonnes de suspension (Projets) */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='MotifSuspension')
    ALTER TABLE [Projets] ADD [MotifSuspension] nvarchar(max) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='DateSuspension')
    ALTER TABLE [Projets] ADD [DateSuspension] datetime2 NULL;
GO

/* 2. Colonnes de baseline (Projets) */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='DateBaseline')
    ALTER TABLE [Projets] ADD [DateBaseline] datetime2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='DateFinPrevueBaseline')
    ALTER TABLE [Projets] ADD [DateFinPrevueBaseline] datetime2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BudgetBaseline')
    ALTER TABLE [Projets] ADD [BudgetBaseline] decimal(18,2) NULL;
GO

/* 3. Table des avenants (gestion du changement) */
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
GO

/* 3b. Table des jetons d'initialisation de mot de passe (utilisée par DemandesAcces/Approuver) */
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('JetonsInitialisationMotDePasse') AND type='U')
BEGIN
    CREATE TABLE [JetonsInitialisationMotDePasse] (
        [Id] uniqueidentifier NOT NULL,
        [UtilisateurId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(450) NOT NULL,
        [DateExpiration] datetime2 NOT NULL,
        [DateUtilisation] datetime2 NULL,
        [UtiliseDepuisIp] nvarchar(max) NULL,
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL,
        CONSTRAINT [PK_JetonsInitialisationMotDePasse] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JetonsInitialisationMotDePasse_Utilisateurs_UtilisateurId]
            FOREIGN KEY ([UtilisateurId]) REFERENCES [Utilisateurs]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_JetonsInitialisationMotDePasse_TokenHash]
        ON [JetonsInitialisationMotDePasse]([TokenHash]);
    CREATE INDEX [IX_JetonsInitialisationMotDePasse_UtilisateurId_DateUtilisation_EstSupprime]
        ON [JetonsInitialisationMotDePasse]([UtilisateurId], [DateUtilisation], [EstSupprime]);
END
GO

/* 4. Table des bénéfices (réalisation de la valeur) */
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
GO

/* 5. Vérification */
SELECT 'Projets - colonnes ajoutees' AS Verif,
       COUNT(*) AS NbColonnes  /* attendu : 5 */
FROM sys.columns
WHERE object_id=OBJECT_ID('Projets')
  AND name IN ('MotifSuspension','DateSuspension','DateBaseline','DateFinPrevueBaseline','BudgetBaseline');

SELECT 'Tables creees' AS Verif,
       (SELECT COUNT(*) FROM sys.objects WHERE object_id=OBJECT_ID('AvenantsProjets') AND type='U') AS Avenants,  /* attendu : 1 */
       (SELECT COUNT(*) FROM sys.objects WHERE object_id=OBJECT_ID('BeneficesProjets') AND type='U') AS Benefices; /* attendu : 1 */
GO
