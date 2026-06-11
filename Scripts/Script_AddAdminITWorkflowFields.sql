-- Script pour ajouter les champs Admin IT Workflow manquants
-- À exécuter sur la base de données GestProjetDb

-- 1. Ajouter le champ PeutCreerDemandeProjet à la table Utilisateurs
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Utilisateurs]') AND name = 'PeutCreerDemandeProjet')
BEGIN
    ALTER TABLE [dbo].[Utilisateurs]
    ADD [PeutCreerDemandeProjet] BIT NOT NULL DEFAULT 1;
    PRINT 'Colonne PeutCreerDemandeProjet ajoutée à la table Utilisateurs';
END
GO

-- 2. Créer la table Departements
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Departements')
BEGIN
    CREATE TABLE [dbo].[Departements] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [Libelle] nvarchar(max) NOT NULL,
        [EstActive] bit NOT NULL DEFAULT 1,
        [DirectionId] uniqueidentifier NOT NULL,
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Departements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departements_Directions_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [dbo].[Directions] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Table Departements créée';
END
GO

-- 3. Créer la table Services
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Services')
BEGIN
    CREATE TABLE [dbo].[Services] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [Libelle] nvarchar(max) NOT NULL,
        [EstActive] bit NOT NULL DEFAULT 1,
        [DepartementId] uniqueidentifier NOT NULL,
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Services_Departements_DepartementId] FOREIGN KEY ([DepartementId]) REFERENCES [dbo].[Departements] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Table Services créée';
END
GO

-- 4. Créer la table HistoriqueChefProjets
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriqueChefProjets')
BEGIN
    CREATE TABLE [dbo].[HistoriqueChefProjets] (
        [Id] uniqueidentifier NOT NULL,
        [ProjetId] uniqueidentifier NOT NULL,
        [ChefProjetId] uniqueidentifier NOT NULL,
        [DateDebut] datetime2 NOT NULL,
        [DateFin] datetime2 NULL,
        [Commentaire] nvarchar(max) NOT NULL DEFAULT '',
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL DEFAULT 0,
        CONSTRAINT [PK_HistoriqueChefProjets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HistoriqueChefProjets_Projets_ProjetId] FOREIGN KEY ([ProjetId]) REFERENCES [dbo].[Projets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistoriqueChefProjets_Utilisateurs_ChefProjetId] FOREIGN KEY ([ChefProjetId]) REFERENCES [dbo].[Utilisateurs] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Table HistoriqueChefProjets créée';
END
GO

-- 5. Créer les index pour améliorer les performances
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Departements_DirectionId')
BEGIN
    CREATE INDEX [IX_Departements_DirectionId] ON [dbo].[Departements] ([DirectionId]);
    PRINT 'Index IX_Departements_DirectionId créé';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Services_DepartementId')
BEGIN
    CREATE INDEX [IX_Services_DepartementId] ON [dbo].[Services] ([DepartementId]);
    PRINT 'Index IX_Services_DepartementId créé';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoriqueChefProjets_ProjetId')
BEGIN
    CREATE INDEX [IX_HistoriqueChefProjets_ProjetId] ON [dbo].[HistoriqueChefProjets] ([ProjetId]);
    PRINT 'Index IX_HistoriqueChefProjets_ProjetId créé';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoriqueChefProjets_ChefProjetId')
BEGIN
    CREATE INDEX [IX_HistoriqueChefProjets_ChefProjetId] ON [dbo].[HistoriqueChefProjets] ([ChefProjetId]);
    PRINT 'Index IX_HistoriqueChefProjets_ChefProjetId créé';
END
GO

PRINT 'Script terminé avec succès !';

