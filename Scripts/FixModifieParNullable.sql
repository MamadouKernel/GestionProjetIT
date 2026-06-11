-- Script pour rendre ModifiePar nullable dans toutes les tables
-- À exécuter si la migration n'a pas été appliquée

USE [GestProjetDb];
GO

-- Directions
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Directions]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Directions] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans Directions';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans Directions';
END
GO

-- Utilisateurs
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Utilisateurs]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Utilisateurs] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans Utilisateurs';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans Utilisateurs';
END
GO

-- DemandesProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DemandesProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[DemandesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans DemandesProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans DemandesProjets';
END
GO

-- Projets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Projets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Projets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans Projets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans Projets';
END
GO

-- MembresProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MembresProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[MembresProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans MembresProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans MembresProjets';
END
GO

-- RisquesProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[RisquesProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[RisquesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans RisquesProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans RisquesProjets';
END
GO

-- LivrablesProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[LivrablesProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[LivrablesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans LivrablesProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans LivrablesProjets';
END
GO

-- HistoriquePhasesProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[HistoriquePhasesProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[HistoriquePhasesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans HistoriquePhasesProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans HistoriquePhasesProjets';
END
GO

-- AnomaliesProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomaliesProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[AnomaliesProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans AnomaliesProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans AnomaliesProjets';
END
GO

-- DemandesClotureProjets
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DemandesClotureProjets]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[DemandesClotureProjets] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans DemandesClotureProjets';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans DemandesClotureProjets';
END
GO

-- DelegationsValidationDSI
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DelegationsValidationDSI]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[DelegationsValidationDSI] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans DelegationsValidationDSI';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans DelegationsValidationDSI';
END
GO

-- ParametresSysteme
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ParametresSysteme]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[ParametresSysteme] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans ParametresSysteme';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans ParametresSysteme';
END
GO

-- DocumentsJointsDemandes
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DocumentsJointsDemandes]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[DocumentsJointsDemandes] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans DocumentsJointsDemandes';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans DocumentsJointsDemandes';
END
GO

-- AuditLogs
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND name = 'ModifiePar' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[AuditLogs] ALTER COLUMN [ModifiePar] NVARCHAR(MAX) NULL;
    PRINT 'Colonne ModifiePar rendue nullable dans AuditLogs';
END
ELSE
BEGIN
    PRINT 'Colonne ModifiePar déjà nullable dans AuditLogs';
END
GO

PRINT 'Script terminé avec succès !';
GO


