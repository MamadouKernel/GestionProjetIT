/* ============================================================================
   PURGE des données de démonstration — base PROD `zeinab`
   ----------------------------------------------------------------------------
   ⚠️ DESTRUCTIF. À exécuter par un humain, APRÈS un backup complet :
       BACKUP DATABASE [zeinab] TO DISK='C:\backups\zeinab_avant_purge.bak' WITH INIT, COMPRESSION;

   CONSERVE : le schéma, le compte 2414 (+ ses rôles), Directions, Services,
              ParametresSysteme, RolePermissions.
   SUPPRIME : tous les projets + demandes + tout leur contenu, portefeuilles,
              notifications, audit, et tous les AUTRES utilisateurs (démo).

   Le script s'exécute dans une TRANSACTION OUVERTE : il supprime puis affiche
   des compteurs. RIEN n'est définitif tant que tu n'as pas tapé COMMIT.
   ============================================================================ */

USE [zeinab];
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @keep uniqueidentifier = (SELECT Id FROM Utilisateurs WHERE Matricule = '2414');
IF @keep IS NULL
BEGIN
    RAISERROR('Compte 2414 introuvable — on arrete pour ne pas se verrouiller dehors.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

/* 1. UAT */
DELETE FROM ExecutionsTestsProjets;
DELETE FROM CasTestsProjets;
DELETE FROM CampagnesTestsProjets;

/* 2. Signatures électroniques */
DELETE FROM SignatairesDossiersSignatureProjets;
DELETE FROM DossiersSignatureProjets;

/* 3. Collaboration */
DELETE FROM TachesCollaborationProjets;
DELETE FROM CollaborationsProjets;

/* 4. Charges / planning / artefacts */
DELETE FROM ChargesProjets;
DELETE FROM TachesPlanningProjets;
DELETE FROM LignesRaciProjets;
DELETE FROM LignesCommunicationProjets;
DELETE FROM LignesBudgetPlanificationProjets;
DELETE FROM PvKickOffProjets;

/* 5. Charte / fiche */
DELETE FROM JalonsCharte;
DELETE FROM PartiesPrenantesCharte;
DELETE FROM CharteProjets;
DELETE FROM FicheProjets;

/* 6. Livrables / risques / anomalies / nouvelles features */
DELETE FROM LivrablesProjets;
DELETE FROM AnomaliesProjets;
DELETE FROM RisquesProjets;
DELETE FROM AvenantsProjets;
DELETE FROM BeneficesProjets;

/* 7. Clôture / historiques / délégations / membres */
DELETE FROM DemandesClotureProjets;
DELETE FROM HistoriquePhasesProjets;
DELETE FROM HistoriqueChefProjets;
DELETE FROM DelegationsChefProjet;
DELETE FROM DelegationsValidationDSI;
DELETE FROM MembresProjets;

/* 8. Projets */
DELETE FROM Projets;

/* 9. Demandes (+ documents) */
DELETE FROM DocumentsJointsDemandes;
DELETE FROM DemandesProjets;

/* 10. Portefeuilles, notifications, audit, demandes de compte/accès */
DELETE FROM PortefeuillesProjets;
DELETE FROM Notifications;
DELETE FROM AuditLogs;
DELETE FROM DemandesCreationCompte;
DELETE FROM DemandesAccesAzureAd;

/* 11. Détacher les DSI démo des directions conservées (sinon FK bloque la suppression) */
UPDATE Directions SET DSIId = NULL WHERE DSIId IS NOT NULL AND DSIId <> @keep;

/* 12. Utilisateurs : tout SAUF le compte 2414 */
IF OBJECT_ID('JetonsInitialisationMotDePasse','U') IS NOT NULL
    DELETE FROM JetonsInitialisationMotDePasse WHERE UtilisateurId <> @keep;
DELETE FROM UtilisateurRoles            WHERE UtilisateurId <> @keep;
DELETE FROM Utilisateurs                 WHERE Id           <> @keep;

/* 13. Vérification — RIEN n'est encore validé */
SELECT
    (SELECT COUNT(*) FROM Projets)          AS Projets,        -- attendu 0
    (SELECT COUNT(*) FROM DemandesProjets)  AS Demandes,       -- attendu 0
    (SELECT COUNT(*) FROM Utilisateurs)     AS Utilisateurs,   -- attendu 1 (le compte 2414)
    (SELECT COUNT(*) FROM Directions)       AS Directions,     -- conservées
    (SELECT COUNT(*) FROM Services)         AS Services;       -- conservés

/* ----------------------------------------------------------------------------
   Vérifie les compteurs ci-dessus (Projets=0, Demandes=0, Utilisateurs=1).
   - Si tout est correct :  COMMIT TRANSACTION;
   - Si quelque chose cloche : ROLLBACK TRANSACTION;
   (tape l'une de ces deux commandes dans la MÊME fenêtre/session)
   ---------------------------------------------------------------------------- */
