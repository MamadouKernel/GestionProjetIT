-- Script de migration pour ajouter les fonctionnalités RAG et Charges
-- À exécuter après avoir créé la migration Entity Framework

-- 1. Ajouter les champs RAG dans la table Projets
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Projets]') AND name = 'IndicateurRAG')
BEGIN
    ALTER TABLE [dbo].[Projets]
    ADD [IndicateurRAG] int NOT NULL DEFAULT 1,
        [DateDernierCalculRAG] datetime2 NULL;
    
    PRINT 'Champs RAG ajoutés à la table Projets';
END
GO

-- 2. Créer la table ChargesProjets
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChargesProjets')
BEGIN
    CREATE TABLE [dbo].[ChargesProjets] (
        [Id] uniqueidentifier NOT NULL,
        [ProjetId] uniqueidentifier NOT NULL,
        [RessourceId] uniqueidentifier NOT NULL,
        [SemaineDebut] datetime2 NOT NULL,
        [ChargePrevisionnelle] decimal(18,2) NOT NULL DEFAULT 0,
        [ChargeReelle] decimal(18,2) NULL,
        [DateSaisieChargeReelle] datetime2 NULL,
        [SaisieParId] uniqueidentifier NULL,
        [Commentaire] nvarchar(max) NULL,
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ChargesProjets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChargesProjets_Projets_ProjetId] FOREIGN KEY ([ProjetId]) REFERENCES [dbo].[Projets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChargesProjets_Utilisateurs_RessourceId] FOREIGN KEY ([RessourceId]) REFERENCES [dbo].[Utilisateurs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChargesProjets_Utilisateurs_SaisieParId] FOREIGN KEY ([SaisieParId]) REFERENCES [dbo].[Utilisateurs] ([Id]) ON DELETE SET NULL
    );
    
    CREATE UNIQUE INDEX [IX_ChargesProjets_ProjetId_SemaineDebut_RessourceId] 
    ON [dbo].[ChargesProjets] ([ProjetId], [SemaineDebut], [RessourceId]) 
    WHERE [EstSupprime] = 0;
    
    PRINT 'Table ChargesProjets créée';
END
GO

-- 3. Mettre à jour les projets existants avec RAG par défaut (Vert)
UPDATE [dbo].[Projets]
SET [IndicateurRAG] = 1, [DateDernierCalculRAG] = GETDATE()
WHERE [IndicateurRAG] IS NULL OR [DateDernierCalculRAG] IS NULL;

PRINT 'Migration RAG et Charges terminée avec succès';
GO

