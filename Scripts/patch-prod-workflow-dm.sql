/* ============================================================================
   Patch schéma PROD — Workflow DM premier rang (validation d'accès)
   ----------------------------------------------------------------------------
   À exécuter sur la base PROD `zeinab`, APRÈS un backup.
   Idempotent (IF NOT EXISTS) : rejouable sans risque.

   Ajoute les colonnes nécessaires au workflow où le Directeur Métier valide
   en premier rang la demande d'accès (avant création par AdminIT/DSI/RSIT).

   Colonnes :
   - ValideeParDmId     : FK utilisateur qui a tranché (verrou)
   - DateValidationDm   : horodatage de la décision DM
   - CommentaireDm      : commentaire ou motif de refus
   - RoleConfirmeParDm  : rôle confirmé (peut différer du rôle demandé)
   ============================================================================ */

USE [zeinab];
GO

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
GO

/* Vérification — attendus : 4 colonnes, 1 FK, 1 index. */
SELECT 'Colonnes' AS Verif, COUNT(*) AS Nb
FROM sys.columns
WHERE object_id=OBJECT_ID('DemandesAccesAzureAd')
  AND name IN ('ValideeParDmId','DateValidationDm','CommentaireDm','RoleConfirmeParDm');

SELECT 'FK' AS Verif, COUNT(*) AS Nb
FROM sys.foreign_keys WHERE name='FK_DemandesAccesAzureAd_Utilisateurs_ValideeParDmId';

SELECT 'Index' AS Verif, COUNT(*) AS Nb
FROM sys.indexes WHERE name='IX_DemandesAccesAzureAd_ValideeParDmId';
GO
