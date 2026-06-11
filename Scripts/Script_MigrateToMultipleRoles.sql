-- Script de migration pour passer à un système de rôles multiples (Many-to-Many)
-- À exécuter sur la base de données GestProjetDb
-- Ce script migre les données existantes vers la nouvelle structure

PRINT 'Début de la migration vers les rôles multiples...';
GO

-- 1. Créer la table UtilisateurRoles
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UtilisateurRoles')
BEGIN
    CREATE TABLE [dbo].[UtilisateurRoles] (
        [Id] uniqueidentifier NOT NULL,
        [UtilisateurId] uniqueidentifier NOT NULL,
        [Role] int NOT NULL,
        [DateDebut] datetime2 NULL,
        [DateFin] datetime2 NULL,
        [Commentaire] nvarchar(max) NULL,
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL DEFAULT 0,
        CONSTRAINT [PK_UtilisateurRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UtilisateurRoles_Utilisateurs_UtilisateurId] FOREIGN KEY ([UtilisateurId]) REFERENCES [dbo].[Utilisateurs] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Table UtilisateurRoles créée';
END
ELSE
BEGIN
    PRINT 'Table UtilisateurRoles existe déjà';
END
GO

-- 2. Créer l'index unique pour éviter les doublons (UtilisateurId + Role)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UtilisateurRoles_UtilisateurId_Role' AND object_id = OBJECT_ID(N'[dbo].[UtilisateurRoles]'))
BEGIN
    CREATE UNIQUE INDEX [IX_UtilisateurRoles_UtilisateurId_Role] 
    ON [dbo].[UtilisateurRoles] ([UtilisateurId], [Role])
    WHERE [EstSupprime] = 0;
    PRINT 'Index IX_UtilisateurRoles_UtilisateurId_Role créé';
END
GO

-- 3. Migrer les rôles existants de Utilisateurs.Role vers UtilisateurRoles
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Utilisateurs]') AND name = 'Role')
BEGIN
    PRINT 'Migration des rôles existants...';
    
    -- Insérer les rôles existants dans UtilisateurRoles
    INSERT INTO [dbo].[UtilisateurRoles] (
        [Id],
        [UtilisateurId],
        [Role],
        [DateDebut],
        [DateCreation],
        [CreePar],
        [EstSupprime]
    )
    SELECT 
        NEWID() AS [Id],
        [Id] AS [UtilisateurId],
        [Role] AS [Role],
        GETDATE() AS [DateDebut],
        GETDATE() AS [DateCreation],
        'SYSTEM_MIGRATION' AS [CreePar],
        0 AS [EstSupprime]
    FROM [dbo].[Utilisateurs]
    WHERE [Role] IS NOT NULL 
      AND [Role] != 0
      AND [EstSupprime] = 0
      AND NOT EXISTS (
          -- Éviter les doublons
          SELECT 1 FROM [dbo].[UtilisateurRoles] ur 
          WHERE ur.UtilisateurId = Utilisateurs.Id 
            AND ur.Role = Utilisateurs.Role
            AND ur.EstSupprime = 0
      );
    
    PRINT CONCAT('Migration terminée. ', @@ROWCOUNT, ' rôles migrés.');
    
    -- Pour les utilisateurs sans rôle (Role = 0 ou NULL), créer le rôle Demandeur par défaut
    INSERT INTO [dbo].[UtilisateurRoles] (
        [Id],
        [UtilisateurId],
        [Role],
        [DateDebut],
        [DateCreation],
        [CreePar],
        [EstSupprime]
    )
    SELECT 
        NEWID() AS [Id],
        [Id] AS [UtilisateurId],
        1 AS [Role], -- Demandeur = 1
        GETDATE() AS [DateDebut],
        GETDATE() AS [DateCreation],
        'SYSTEM_MIGRATION' AS [CreePar],
        0 AS [EstSupprime]
    FROM [dbo].[Utilisateurs]
    WHERE ([Role] IS NULL OR [Role] = 0)
      AND [EstSupprime] = 0
      AND NOT EXISTS (
          SELECT 1 FROM [dbo].[UtilisateurRoles] ur 
          WHERE ur.UtilisateurId = Utilisateurs.Id 
            AND ur.EstSupprime = 0
      );
    
    PRINT CONCAT('Rôle Demandeur par défaut créé pour ', @@ROWCOUNT, ' utilisateurs sans rôle.');
END
ELSE
BEGIN
    PRINT 'Colonne Role n''existe pas dans Utilisateurs. Migration non nécessaire.';
END
GO

-- 4. Vérifier la migration
SELECT 
    COUNT(*) AS TotalUtilisateurs,
    (SELECT COUNT(DISTINCT UtilisateurId) FROM [dbo].[UtilisateurRoles] WHERE EstSupprime = 0) AS UtilisateursAvecRoles,
    (SELECT COUNT(*) FROM [dbo].[UtilisateurRoles] WHERE EstSupprime = 0) AS TotalRolesAssignes
FROM [dbo].[Utilisateurs]
WHERE EstSupprime = 0;
GO

PRINT 'Migration vers les rôles multiples terminée avec succès !';
PRINT '';
PRINT 'IMPORTANT: La colonne Role dans Utilisateurs peut maintenant être supprimée';
PRINT 'mais il est recommandé de la garder temporairement pour compatibilité.';
GO

