# Guide de Sécurité - Configuration des Secrets

## Configuration des Secrets pour le Développement

### Utilisation de User Secrets (Recommandé)

Pour éviter de stocker des secrets dans `appsettings.json`, utilisez User Secrets :

```bash
# Initialiser User Secrets (une seule fois)
dotnet user-secrets init

# Ajouter la chaîne de connexion
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=GestProjetDb;Trusted_Connection=True;Encrypt=True;"

# Vérifier les secrets configurés
dotnet user-secrets list
```

### Configuration pour la Production

En production, utilisez :
- **Azure Key Vault** (recommandé pour Azure)
- **Variables d'environnement**
- **Configuration sécurisée du serveur**

Exemple avec variables d'environnement :
```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;Encrypt=True;"
```

## Mot de Passe Admin

⚠️ **IMPORTANT** : Le mot de passe admin est maintenant généré aléatoirement au premier démarrage.

### En Développement
- Le mot de passe est sauvegardé dans `admin-password.txt` à la racine du projet
- **SUPPRIMEZ ce fichier après utilisation !**
- Changez le mot de passe au premier login

### En Production
- Configurez le mot de passe via User Secrets ou variables d'environnement
- Ou utilisez l'interface d'administration pour créer le compte admin

## Sécurité des Cookies

Les cookies sont maintenant configurés avec :
- `SecurePolicy = Always` en production (HTTPS uniquement)
- `SameSite = Strict` en production (protection CSRF renforcée)
- Timeout réduit à 30 minutes

## Validation des Fichiers

Les fichiers uploadés sont maintenant validés avec :
- Limite de taille : 10 MB par défaut (configurable)
- Validation de l'extension
- Protection contre path traversal
- Validation du type MIME

## Middleware de Gestion d'Erreurs

Un middleware global capture toutes les exceptions et :
- Log les erreurs détaillées avec Serilog
- Retourne des messages génériques aux utilisateurs (pas d'exposition de détails)
- Gère différents types d'exceptions avec des codes HTTP appropriés

## TestController

Le `TestController` est maintenant :
- Protégé avec `[Authorize(Roles = "AdminIT")]`
- Accessible uniquement en environnement de développement
- Retourne 404 en production

