-- Script pour mettre à jour Services pour utiliser DirectionId directement
-- (Direction et Département sont identiques dans cette organisation)
-- À exécuter sur la base de données GestProjetDb

-- Si la table Services existe déjà avec DepartementId, on doit migrer les données
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'DepartementId')
BEGIN
    PRINT 'Migration des Services de DepartementId vers DirectionId...';
    
    -- 1. Ajouter la colonne DirectionId si elle n'existe pas
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'DirectionId')
    BEGIN
        ALTER TABLE [dbo].[Services]
        ADD [DirectionId] uniqueidentifier NULL;
        PRINT 'Colonne DirectionId ajoutée à la table Services';
    END
    
    -- 2. Migrer les données de DepartementId vers DirectionId via la table Departements
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Departements')
    BEGIN
        UPDATE s
        SET s.DirectionId = d.DirectionId
        FROM [dbo].[Services] s
        INNER JOIN [dbo].[Departements] d ON s.DepartementId = d.Id
        WHERE s.DirectionId IS NULL;
        
        PRINT 'Données migrées de DepartementId vers DirectionId';
    END
    
    -- 3. Rendre DirectionId NOT NULL après migration
    -- D'abord, s'assurer qu'il n'y a pas de valeurs NULL
    UPDATE [dbo].[Services]
    SET DirectionId = (SELECT TOP 1 Id FROM [dbo].[Directions] ORDER BY Id)
    WHERE DirectionId IS NULL;
    
    ALTER TABLE [dbo].[Services]
    ALTER COLUMN [DirectionId] uniqueidentifier NOT NULL;
    
    -- 4. Supprimer la contrainte de clé étrangère et l'index de DepartementId
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Services_Departements_DepartementId')
    BEGIN
        ALTER TABLE [dbo].[Services]
        DROP CONSTRAINT [FK_Services_Departements_DepartementId];
        PRINT 'Contrainte FK_Services_Departements_DepartementId supprimée';
    END
    
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Services_DepartementId' AND object_id = OBJECT_ID(N'[dbo].[Services]'))
    BEGIN
        DROP INDEX [IX_Services_DepartementId] ON [dbo].[Services];
        PRINT 'Index IX_Services_DepartementId supprimé';
    END
    
    -- 5. Ajouter la contrainte de clé étrangère vers Directions
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Services_Directions_DirectionId')
    BEGIN
        ALTER TABLE [dbo].[Services]
        ADD CONSTRAINT [FK_Services_Directions_DirectionId] 
        FOREIGN KEY ([DirectionId]) REFERENCES [dbo].[Directions] ([Id]) ON DELETE NO ACTION;
        PRINT 'Contrainte FK_Services_Directions_DirectionId ajoutée';
    END
    
    -- 6. Créer l'index sur DirectionId
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Services_DirectionId' AND object_id = OBJECT_ID(N'[dbo].[Services]'))
    BEGIN
        CREATE INDEX [IX_Services_DirectionId] ON [dbo].[Services] ([DirectionId]);
        PRINT 'Index IX_Services_DirectionId créé';
    END
    
    -- 7. Supprimer la colonne DepartementId
    ALTER TABLE [dbo].[Services]
    DROP COLUMN [DepartementId];
    PRINT 'Colonne DepartementId supprimée de la table Services';
    
    -- 8. Supprimer la table Departements si elle existe et est vide (optionnel)
    -- ATTENTION: Ne pas supprimer si elle contient encore des données
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Departements')
    BEGIN
        IF NOT EXISTS (SELECT * FROM [dbo].[Departements])
        BEGIN
            DROP TABLE [dbo].[Departements];
            PRINT 'Table Departements supprimée (elle était vide)';
        END
        ELSE
        BEGIN
            PRINT 'ATTENTION: La table Departements contient encore des données. Suppression manuelle requise.';
        END
    END
END
ELSE
BEGIN
    -- Si la table Services n'existe pas encore ou n'a pas DepartementId, créer/mettre à jour directement avec DirectionId
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Services')
    BEGIN
        CREATE TABLE [dbo].[Services] (
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
            CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_Services_Directions_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [dbo].[Directions] ([Id]) ON DELETE NO ACTION
        );
        PRINT 'Table Services créée avec DirectionId';
        
        CREATE INDEX [IX_Services_DirectionId] ON [dbo].[Services] ([DirectionId]);
        PRINT 'Index IX_Services_DirectionId créé';
    END
    ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND name = 'DirectionId')
    BEGIN
        -- La table existe mais n'a pas DirectionId, l'ajouter
        ALTER TABLE [dbo].[Services]
        ADD [DirectionId] uniqueidentifier NULL;
        
        -- Remplir avec une valeur par défaut (à ajuster selon vos besoins)
        UPDATE [dbo].[Services]
        SET DirectionId = (SELECT TOP 1 Id FROM [dbo].[Directions] ORDER BY Id)
        WHERE DirectionId IS NULL;
        
        ALTER TABLE [dbo].[Services]
        ALTER COLUMN [DirectionId] uniqueidentifier NOT NULL;
        
        ALTER TABLE [dbo].[Services]
        ADD CONSTRAINT [FK_Services_Directions_DirectionId] 
        FOREIGN KEY ([DirectionId]) REFERENCES [dbo].[Directions] ([Id]) ON DELETE NO ACTION;
        
        CREATE INDEX [IX_Services_DirectionId] ON [dbo].[Services] ([DirectionId]);
        
        PRINT 'Colonne DirectionId ajoutée à la table Services existante';
    END
END
GO

-- Ajouter la relation de collection dans Directions si ce n'est pas déjà fait
-- (Cette partie est gérée par EF Core, mais on peut ajouter un commentaire pour référence)
PRINT 'Vérification terminée. La structure Services -> Direction est maintenant en place.';
GO

