# Configuration Azure AD pour l'authentification

## Prérequis

1. Accès au portail Azure (https://portal.azure.com)
2. Droits pour créer une application dans Azure AD
3. Accès administrateur à l'application GestionProjects

## Étapes de configuration dans Azure AD

### 1. Créer une application Azure AD

1. Connectez-vous au portail Azure
2. Allez dans "Azure Active Directory" > "App registrations" > "New registration"
3. Remplissez les informations :
   - **Name**: GestionProjects (ou le nom de votre choix)
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: 
     - Type: Web
     - URL: `https://votre-domaine.com/signin-oidc` (production)
     - URL: `https://localhost:7000/signin-oidc` (développement)

### 2. Récupérer les informations de configuration

Après la création de l'application, notez les informations suivantes :

- **Application (client) ID**: Visible sur la page "Overview"
- **Directory (tenant) ID**: Visible sur la page "Overview"

### 3. Créer un Client Secret

1. Allez dans "Certificates & secrets"
2. Cliquez sur "New client secret"
3. Donnez un nom et choisissez une durée de validité
4. **IMPORTANT**: Copiez immédiatement la valeur du secret (elle ne sera plus visible après)

### 4. Configurer les permissions API

1. Allez dans "API permissions"
2. Vérifiez que les permissions suivantes sont présentes :
   - Microsoft Graph > User.Read (Delegated)
   - Microsoft Graph > email (Delegated)
   - Microsoft Graph > openid (Delegated)
   - Microsoft Graph > profile (Delegated)

### 5. Configurer les Redirect URIs

1. Allez dans "Authentication"
2. Ajoutez les URIs de redirection :
   - Production: `https://votre-domaine.com/signin-oidc`
   - Développement: `https://localhost:7000/signin-oidc`
3. Dans "Logout URL", ajoutez :
   - Production: `https://votre-domaine.com/signout-callback-oidc`
   - Développement: `https://localhost:7000/signout-callback-oidc`
4. Cochez "ID tokens" dans la section "Implicit grant and hybrid flows"

## Configuration de l'application GestionProjects

### 1. Mettre à jour appsettings.json

Ouvrez le fichier `appsettings.json` et remplacez les valeurs suivantes :

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "VOTRE_TENANT_ID",
    "ClientId": "VOTRE_CLIENT_ID",
    "ClientSecret": "VOTRE_CLIENT_SECRET",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  }
}
```

### 2. Configuration pour la production

Pour la production, il est recommandé de stocker le Client Secret de manière sécurisée :

#### Option 1: Variables d'environnement
```bash
set AzureAd__ClientSecret=VOTRE_CLIENT_SECRET
```

#### Option 2: Azure Key Vault (recommandé)
Configurez Azure Key Vault et référencez le secret dans votre configuration.

#### Option 3: User Secrets (développement uniquement)
```bash
dotnet user-secrets set "AzureAd:ClientSecret" "VOTRE_CLIENT_SECRET"
```

## Fonctionnement de l'authentification

### Flux d'authentification

1. L'utilisateur clique sur "Se connecter avec Azure AD" sur la page de login
2. L'utilisateur est redirigé vers Azure AD pour s'authentifier
3. Après authentification, Azure AD redirige vers `/signin-oidc` (callback)
4. L'application récupère les informations de l'utilisateur (email, nom, prénom, matricule)
5. Deux cas possibles :
   - **Utilisateur référencé** : Connexion automatique avec les cookies locaux
   - **Utilisateur non référencé** : Affichage de la page "Demander Accès"

### Utilisateurs non référencés

Si un utilisateur s'authentifie avec Azure AD mais n'existe pas dans la base de données :

1. Il est redirigé vers la page "Demander Accès"
2. Il peut soumettre une demande avec une justification
3. Une notification est envoyée à tous les AdminIT
4. Les AdminIT peuvent créer l'utilisateur dans le système

## Gestion des utilisateurs

### Créer un utilisateur pour Azure AD

Lorsqu'un AdminIT reçoit une demande d'accès :

1. Allez dans "Administration" > "Utilisateurs"
2. Créez un nouvel utilisateur avec :
   - **Email** : L'email Azure AD de l'utilisateur (IMPORTANT)
   - **Matricule** : Le matricule de l'utilisateur
   - **Nom et Prénom** : Les informations de l'utilisateur
   - **Rôles** : Attribuez les rôles appropriés
3. L'utilisateur pourra se connecter avec Azure AD lors de sa prochaine tentative

### Synchronisation des informations

- L'email est utilisé comme clé de correspondance entre Azure AD et la base de données
- Les informations (nom, prénom) sont récupérées depuis Azure AD mais peuvent être différentes dans la base
- Les rôles et permissions sont gérés uniquement dans l'application (pas dans Azure AD)

## Dépannage

### Erreur "Redirect URI mismatch"

Vérifiez que l'URI de redirection dans Azure AD correspond exactement à l'URL de votre application.

### Erreur "Invalid client secret"

Le client secret a peut-être expiré. Créez-en un nouveau dans Azure AD et mettez à jour la configuration.

### L'utilisateur ne peut pas se connecter

1. Vérifiez que l'email de l'utilisateur dans la base correspond à son email Azure AD
2. Vérifiez que l'utilisateur n'est pas marqué comme supprimé (`EstSupprime = false`)
3. Vérifiez les logs de l'application pour plus de détails

### Erreur "Unable to retrieve user information"

Vérifiez que les permissions API sont correctement configurées dans Azure AD.

## Sécurité

### Bonnes pratiques

1. **Ne jamais commiter le Client Secret** dans le code source
2. Utiliser des variables d'environnement ou Azure Key Vault en production
3. Renouveler régulièrement le Client Secret
4. Limiter les permissions API au strict nécessaire
5. Activer l'authentification multi-facteurs (MFA) dans Azure AD
6. Surveiller les logs d'authentification

### Logs

Les événements d'authentification sont enregistrés dans les logs de l'application :
- Connexions réussies
- Échecs d'authentification
- Demandes d'accès
- Erreurs Azure AD

Consultez les fichiers de logs dans le dossier `logs/`.

## Support

Pour toute question ou problème :
1. Consultez les logs de l'application
2. Vérifiez la configuration Azure AD
3. Contactez l'équipe DSI
