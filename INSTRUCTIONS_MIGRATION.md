# Instructions pour créer la migration

## Option 1 : Créer automatiquement avec EF Core (Recommandé)

1. Ouvrez un terminal PowerShell ou CMD dans le dossier du projet
2. Exécutez la commande suivante :

```bash
dotnet ef migrations add InitialCreate
```

Si cela fonctionne, vous devriez voir :
- Un dossier `Migrations` créé avec les fichiers de migration
- Le fichier `InitialCreate.cs` qui contient la création de toutes les tables
- Le fichier `ApplicationDbContextModelSnapshot.cs` mis à jour

3. Vérifiez ensuite que tout compile :

```bash
dotnet build
```

4. Si tout est OK, vous pouvez appliquer la migration à la base de données :

```bash
dotnet ef database update
```

⚠️ **ATTENTION** : Si votre base de données contient déjà des données, cette commande créera toutes les tables. Assurez-vous que c'est ce que vous voulez.

---

## Option 2 : Utiliser le script SQL manuel (Si EF Core ne fonctionne pas)

Si la commande `dotnet ef migrations add` ne fonctionne pas, vous pouvez utiliser le script SQL que j'ai créé :

1. Ouvrez SQL Server Management Studio (SSMS) ou votre outil SQL préféré
2. Connectez-vous à votre base de données `GestProjetDb`
3. Exécutez le script : `Script_MigrateToMultipleRoles.sql`

Ce script :
- Crée la table `UtilisateurRoles`
- Migre les rôles existants de `Utilisateurs.Role` vers `UtilisateurRoles`
- Crée les index nécessaires

⚠️ **IMPORTANT** : Après avoir exécuté le script SQL, vous devrez créer une migration EF Core pour synchroniser le snapshot :

```bash
dotnet ef migrations add SyncAfterManualSQL
```

Puis marquer cette migration comme appliquée (car vous l'avez déjà appliquée manuellement) :

```sql
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('SyncAfterManualSQL', '9.0.11');
```

---

## Vérification

Après avoir créé/appliqué la migration, vérifiez :

1. **Vérifier que la table UtilisateurRoles existe** :
```sql
SELECT * FROM UtilisateurRoles;
```

2. **Vérifier que les utilisateurs ont des rôles** :
```sql
SELECT u.Matricule, u.Nom, ur.Role 
FROM Utilisateurs u
LEFT JOIN UtilisateurRoles ur ON u.Id = ur.UtilisateurId AND ur.EstSupprime = 0;
```

3. **Tester l'application** :
- Lancer l'application
- Se connecter avec l'utilisateur admin
- Aller dans Admin > Utilisateurs
- Vérifier que vous pouvez créer/modifier des utilisateurs avec plusieurs rôles

---

## En cas de problème

Si vous rencontrez des erreurs :

1. Vérifiez que Entity Framework Tools est installé :
```bash
dotnet tool install --global dotnet-ef
```

2. Vérifiez votre chaîne de connexion dans `appsettings.json`

3. Vérifiez que la base de données existe et est accessible

4. Si vous avez des erreurs de compilation, lancez :
```bash
dotnet clean
dotnet build
```

