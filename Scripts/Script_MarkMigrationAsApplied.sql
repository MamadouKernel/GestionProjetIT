-- Script à exécuter APRÈS avoir exécuté Script_AddAdminITWorkflowFields.sql
-- Ce script marque la migration comme déjà appliquée dans la table __EFMigrationsHistory

-- Vérifier si la migration existe déjà
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250125000000_AddAdminITWorkflowFields')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250125000000_AddAdminITWorkflowFields', '9.0.11');
    PRINT 'Migration AddAdminITWorkflowFields marquée comme appliquée';
END
ELSE
BEGIN
    PRINT 'Migration AddAdminITWorkflowFields existe déjà dans l''historique';
END
GO

