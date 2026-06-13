/*
================================================================================
  AUDIT DES LONGUEURS RÉELLES — à exécuter AVANT la migration "BornerLongueursChaines"
================================================================================

  Contexte
  --------
  La migration borne par défaut toutes les colonnes nvarchar(max) à nvarchar(4000)
  (sauf les champs de texte long / base64 explicitement conservés en max).
  Une colonne dont une valeur existante dépasse 4000 caractères serait TRONQUÉE
  par l'ALTER COLUMN — donc perte de données irréversible.

  Mode d'emploi
  -------------
  1. Exécuter ce script sur CHAQUE base concernée (dev PUIS production).
  2. Si le 1er résultat (« colonnes À RISQUE ») est VIDE  -> migration sûre, applique.
  3. Si NON vide -> envoie-moi la liste : j'exempterai ces colonnes en nvarchar(max)
     dans ConfigurerColonnesTexteLong() AVANT que tu n'appliques la migration.

  Ne modifie aucune donnée. Lecture seule.
================================================================================
*/

SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#Resultats') IS NOT NULL DROP TABLE #Resultats;
CREATE TABLE #Resultats (
    NomTable    sysname,
    NomColonne  sysname,
    MaxLongueur bigint
);

DECLARE @sql nvarchar(max) = N'';

-- Construit dynamiquement un SELECT MAX(LEN(col)) pour chaque colonne nvarchar(max)
SELECT @sql = STRING_AGG(CAST(
        N'INSERT INTO #Resultats SELECT '
      + QUOTENAME(t.name, '''') + N', '
      + QUOTENAME(c.name, '''') + N', '
      + N'ISNULL(MAX(LEN(' + QUOTENAME(c.name) + N')), 0) FROM '
      + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N';'
    AS nvarchar(max)), CHAR(13) + CHAR(10))
FROM sys.columns c
JOIN sys.tables  t  ON t.object_id     = c.object_id
JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
WHERE ty.name        = 'nvarchar'
  AND c.max_length   = -1            -- -1 = nvarchar(max)
  AND t.is_ms_shipped = 0
  AND t.name <> '__EFMigrationsHistory';

EXEC sp_executesql @sql;

-- 1) COLONNES À RISQUE : seraient tronquées par nvarchar(4000) -> à exempter
PRINT '--- Colonnes A RISQUE (> 4000 caracteres) : doivent rester nvarchar(max) ---';
SELECT NomTable, NomColonne, MaxLongueur
FROM #Resultats
WHERE MaxLongueur > 4000
ORDER BY MaxLongueur DESC;

-- 2) VUE COMPLÈTE : longueur réelle max de chaque colonne nvarchar(max)
PRINT '--- Vue complete (toutes les colonnes nvarchar(max)) ---';
SELECT NomTable, NomColonne, MaxLongueur
FROM #Resultats
ORDER BY MaxLongueur DESC;

DROP TABLE #Resultats;
