-- Script pour ajouter la colonne Périmètre à la table DemandesProjets
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DemandesProjets]') AND name = 'Perimetre')
BEGIN
    ALTER TABLE [dbo].[DemandesProjets]
    ADD [Perimetre] nvarchar(max) NULL;
    
    PRINT 'Colonne Perimetre ajoutée avec succès à la table DemandesProjets.';
END
ELSE
BEGIN
    PRINT 'La colonne Perimetre existe déjà dans la table DemandesProjets.';
END

