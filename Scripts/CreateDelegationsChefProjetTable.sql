-- Script SQL pour créer la table DelegationsChefProjet
-- À exécuter manuellement si la migration n'a pas été appliquée automatiquement

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DelegationsChefProjet]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DelegationsChefProjet] (
        [Id] uniqueidentifier NOT NULL,
        [ProjetId] uniqueidentifier NOT NULL,
        [DelegantId] uniqueidentifier NOT NULL,
        [DelegueId] uniqueidentifier NOT NULL,
        [DateDebut] datetime2 NOT NULL,
        [DateFin] datetime2 NULL,
        [EstActive] bit NOT NULL,
        [DateCreation] datetime2 NOT NULL,
        [CreePar] nvarchar(max) NOT NULL,
        [DateModification] datetime2 NULL,
        [ModifiePar] nvarchar(max) NULL,
        [EstSupprime] bit NOT NULL,
        CONSTRAINT [PK_DelegationsChefProjet] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DelegationsChefProjet_Projets_ProjetId] FOREIGN KEY ([ProjetId]) REFERENCES [dbo].[Projets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DelegationsChefProjet_Utilisateurs_DelegantId] FOREIGN KEY ([DelegantId]) REFERENCES [dbo].[Utilisateurs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DelegationsChefProjet_Utilisateurs_DelegueId] FOREIGN KEY ([DelegueId]) REFERENCES [dbo].[Utilisateurs] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_DelegationsChefProjet_ProjetId] ON [dbo].[DelegationsChefProjet] ([ProjetId]);
    CREATE INDEX [IX_DelegationsChefProjet_DelegantId] ON [dbo].[DelegationsChefProjet] ([DelegantId]);
    CREATE INDEX [IX_DelegationsChefProjet_DelegueId] ON [dbo].[DelegationsChefProjet] ([DelegueId]);

    -- Ajouter la migration à l'historique EF Core
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251124000000_AddDelegationChefProjet', '9.0.11');

    PRINT 'Table DelegationsChefProjet créée avec succès.';
END
ELSE
BEGIN
    PRINT 'La table DelegationsChefProjet existe déjà.';
END

