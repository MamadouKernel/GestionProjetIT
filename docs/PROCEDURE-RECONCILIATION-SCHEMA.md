# Procédure — Réconciliation du schéma de base de données

> **À exécuter par un humain disposant d'un backup, PAS automatiquement.**
> Cette procédure supprime la dette « migrations EF + patches SQL » décrite
> dans la revue de code. Elle est **irréversible sans backup**.

## Incident réel du 02/07/2026 — RÉSOLU

Le 01/07 à 16:12 (`a55d363`) et 15:13 (`c56a342`), `ApplyMigrationsOnStartup=true`
et `MigrationsAssembly("GestionProjects")` ont été activés en prod sans dérouler
cette procédure. Publié à 16:33 (MSDeploy). La prod (`zeinab`, `10.88.179.103`)
est tombée en crash-loop (`.NET Runtime` Id=1000 / `IIS AspNetCore Module V2`
Id=1018 dans l'Observateur d'événements) au boot suivant.

**Cause réelle (différente de l'exemple ci-dessous) :** l'historique
`__EFMigrationsHistory` en prod était figé à `FixUniqueIndexUtilisateurRoleFiltered`
(12/06). `Migrate()` a donc rejoué 4 migrations d'un coup ; la 1ʳᵉ,
`AddJetonsInitialisationMotDePasse`, a planté sur `SqlException 2714` — la table
existait déjà (créée manuellement via `Scripts/patch-prod-nouvelles-features.sql`).

**Résolution appliquée** (vérifiée colonne par colonne avant toute écriture) :
1. `BACKUP DATABASE zeinab` sur le serveur.
2. `ApplyMigrationsOnStartup` repassé à `false` le temps du diagnostic, republié
   pour stabiliser le service.
3. Comparaison schéma réel vs migration pour chacune des 4 migrations en attente
   (`sys.columns`/`sys.indexes`/`sys.objects`) : une seule était un vrai fantôme
   (`AddJetonsInitialisationMotDePasse`, schéma identique), les 3 autres
   (`BornerLongueursChaines`, `AddIndexUtilisateurMatriculeEmail`,
   `AddDelegationValidationDM`) n'existaient pas encore en base.
4. Insertion manuelle de la seule ligne fantôme dans `__EFMigrationsHistory`.
5. `ApplyMigrationsOnStartup` repassé à `true`, republié : `Migrate()` a appliqué
   proprement les 3 migrations restantes. Confirmé sans erreur dans l'Observateur
   d'événements + `/health` 200.
6. Les 5 blocs de `ApplyCompatibilityPatches` devenus redondants (leur migration
   EF est désormais marquée appliquée partout : dev **et** prod) ont été retirés
   de `DatabaseExtensions.cs` : `SignaturesCharte`, `DemandeCreationCompte`,
   `ProfilRessource/Bilan`, `ComplementPhases/ChargesWorkflow`,
   `PlanificationNative`.

**Restent des patches actifs** (`AvenantsProjets`, `SuspensionProjet`,
`BaselineProjet`, `ValidationDmDemandeAcces`, `BeneficesProjets`,
`EvaluationsMembresProjets`) — **aucune migration EF ne les couvre**, ils
n'ont donc jamais pu devenir des « fantômes » vis-à-vis de `Migrate()`. Tant
que ces 6 entités n'ont pas de vraie migration générée (`dotnet ef migrations
add`), ces patches restent nécessaires pour provisionner un environnement
neuf. Prochaine étape de nettoyage : générer ces migrations manquantes puis
retirer les 6 patches restants.

**Leçon retenue** : ne jamais activer `ApplyMigrationsOnStartup` ou poser
`MigrationsAssembly` sans avoir d'abord constaté l'état réel de
`__EFMigrationsHistory` en prod (étape 1 ci-dessous) — l'écart peut être
plus large et différent de ce que documente cette procédure.

---

---

## 1. Le problème (preuve concrète)

Au démarrage de l'application, ce log apparaît **à chaque boot** :

```
[INF] Applying migration '20260610153708_AddNativePlanningArtifacts'.
[ERR] Failed executing DbCommand ... CREATE TABLE [LignesBudgetPlanificationProjets] ...
[ERR] Erreur migration EF Core — tentative patches SQL manuels
SqlException (2714): There is already an object named 'LignesBudgetPlanificationProjets'.
```

**Cause :** les tables de planification native ont été créées par un **patch SQL**
(`DatabaseExtensions.ApplyCompatibilityPatches`, bloc `PlanificationNative`)
**avant** que la migration `AddNativePlanningArtifacts` ait pu s'appliquer. Comme
ce patch — contrairement aux autres — **n'insère pas** la ligne correspondante
dans `__EFMigrationsHistory`, EF Core retente la migration à chaque démarrage,
échoue, et se rabat sur les patches.

**Pire — divergence de schéma réelle.** La migration et le patch ne produisent
**pas le même schéma** :

| Élément | Migration EF (= snapshot/modèle) | Patch SQL (= base actuelle) |
|---------|----------------------------------|------------------------------|
| `Poste`, `Libelle`, `CodeWbs`… | `nvarchar(max)` | `nvarchar(256)` / `nvarchar(64)` |
| Colonnes texte | `NOT NULL` sans défaut | `NOT NULL DEFAULT N''` |
| Index budget | `IX_..._ProjetId` (ProjetId) | `IX_..._ProjetId_Ordre` (ProjetId, Ordre) |
| Contraintes FK | `FK_..._Projets_ProjetId` | `FK_..._Projets` |

Conséquence : `dotnet ef migrations add` génèrera toujours un diff « fantôme »
tant que base et snapshot ne sont pas réconciliés. Et la base actuelle est
**plus restrictive** que le modèle (`nvarchar(256)` alors que le code autorise
des chaînes plus longues) → bug latent de troncature/erreur d'insertion.

---

## 2. Pré-requis (obligatoires)

1. **Backup complet** de la base prod :
   ```sql
   BACKUP DATABASE [GestProjetDb]
   TO DISK = 'C:\backups\GestProjetDb_avant_reconciliation.bak' WITH INIT, COMPRESSION;
   ```
2. Fenêtre de maintenance (application arrêtée).
3. Accès `dotnet ef` sur une machine qui compile le projet.

---

## 3. Décision de référence

**La source de vérité = le modèle EF (entités C# + `ApplicationDbContextModelSnapshot`).**
On aligne la base réelle sur le modèle, pas l'inverse. Raison : c'est le modèle
que le code utilise au runtime ; les tailles `nvarchar(256)` des patches étaient
des choix arbitraires non reflétés dans les entités.

> Si une entité **devrait** réellement être limitée (ex. un code sur 50 car.),
> corrige l'**entité** (`[MaxLength(50)]`) AVANT de générer la migration de
> réconciliation, pour que la limite vienne du modèle.

---

## 4. Procédure pas-à-pas

### Étape 1 — Constater l'état réel

```sql
-- Migrations déjà marquées appliquées
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;

-- Les tables de planification existent-elles ?
SELECT name FROM sys.objects WHERE type='U'
  AND name IN ('TachesPlanningProjets','LignesRaciProjets',
               'LignesCommunicationProjets','LignesBudgetPlanificationProjets','PvKickOffProjets');
```

### Étape 2 — Marquer les migrations « fantômes » comme appliquées

Pour **chaque** migration dont les objets existent déjà (créés par patch) mais
qui n'est pas dans l'historique — au minimum `AddNativePlanningArtifacts` :

```sql
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId='20260610153708_AddNativePlanningArtifacts')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260610153708_AddNativePlanningArtifacts', '9.0.11');
```

À ce stade, `Migrate()` ne tentera plus de recréer ces tables.

### Étape 3 — Générer la migration de réconciliation

Désactive **temporairement** l'appel aux patches (commente
`ApplyCompatibilityPatches(db)` dans `DatabaseExtensions.ApplyMigrationsAsync`),
puis :

```bash
dotnet ef migrations add ReconcileSchema
```

Ouvre la migration générée. **Deux cas :**

- **Elle est vide** (`Up`/`Down` sans contenu) → base et modèle sont déjà
  alignés. Supprime-la (`dotnet ef migrations remove`) et passe à l'étape 5.
- **Elle contient des `AlterColumn` / `RenameIndex` / `DropForeignKey`…**
  (probable, vu les divergences ci-dessus) → c'est le diff réel à appliquer.
  **Relis chaque ligne** : elle doit faire passer la base de l'état « patch »
  à l'état « modèle » (ex. `nvarchar(256)` → `nvarchar(max)`, renommage des
  index/FK). Rien d'autre ne doit apparaître. Si une perte de données est
  possible (réduction de taille), STOP et arbitre.

### Étape 4 — Appliquer la réconciliation

```bash
dotnet ef database update
```

Vérifie qu'aucune erreur n'apparaît et que `__EFMigrationsHistory` contient
bien `ReconcileSchema`.

### Étape 5 — Supprimer définitivement les patches

Une fois la base alignée, supprime de `DatabaseExtensions.cs` :
- la méthode `ApplyCompatibilityPatches` et `ExecutePatch` ;
- leur appel dans `ApplyMigrationsAsync`.

`ApplyMigrationsAsync` ne doit plus contenir que `db.Database.Migrate()` +
le bootstrap admin.

### Étape 6 — Vérification finale

```bash
dotnet ef migrations add VerifNoDiff   # doit générer une migration VIDE
dotnet ef migrations remove            # on la jette : c'était juste le test
```

Une migration vide = base, snapshot et entités parfaitement alignés. **Objectif atteint.**

---

## 5. Rollback

À n'importe quelle étape, en cas de doute :

```sql
RESTORE DATABASE [GestProjetDb]
FROM DISK = 'C:\backups\GestProjetDb_avant_reconciliation.bak'
WITH REPLACE;
```

Et `git checkout -- Infrastructure/Extensions/DatabaseExtensions.cs` pour
restaurer les patches.

---

## 6. Prévention (pour ne jamais y revenir)

1. **Toute** évolution de schéma passe par `dotnet ef migrations add` — **jamais**
   de `ExecuteSqlRaw` d'`ALTER`/`CREATE TABLE` dans le code applicatif.
2. Ne jamais écrire à la main dans `__EFMigrationsHistory`.
3. Garde de CI : échouer le build si `dotnet ef migrations add __ci_check`
   produit une migration non vide (= un dev a modifié une entité sans migration).
4. En cas d'échec de `Migrate()` en prod, **investiguer**, ne pas patcher autour.
