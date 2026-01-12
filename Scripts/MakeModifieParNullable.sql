-- Script SQL pour rendre ModifiePar nullable dans toutes les tables
-- Exécuter ce script directement sur la base de données

USE [GestProjetDb];
GO

-- Rendre ModifiePar nullable pour toutes les tables
ALTER TABLE [dbo].[AnomaliesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[AuditLogs] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[DelegationsValidationDSI] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[DemandesClotureProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[DemandesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[Directions] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[DocumentsJointsDemandes] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[HistoriquePhasesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[LivrablesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[MembresProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[ParametresSysteme] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[Projets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[RisquesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
ALTER TABLE [dbo].[Utilisateurs] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
GO

PRINT 'Toutes les colonnes ModifiePar ont été rendues nullable.';
GO

