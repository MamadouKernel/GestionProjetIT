-- Script pour ajouter la colonne DateSouhaiteeCloture à la table DemandesClotureProjets
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DemandesClotureProjets]') AND name = 'DateSouhaiteeCloture')
BEGIN
    ALTER TABLE [dbo].[DemandesClotureProjets]
    ADD [DateSouhaiteeCloture] datetime2 NULL;
    
    PRINT 'Colonne DateSouhaiteeCloture ajoutée avec succès à la table DemandesClotureProjets.';
END
ELSE
BEGIN
    PRINT 'La colonne DateSouhaiteeCloture existe déjà dans la table DemandesClotureProjets.';
END

