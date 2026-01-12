# RAPPORT D'AUDIT DE SÉCURITÉ ET QUALITÉ
## Application Web de Gestion des Projets IT - CIT DSI

**Date de l'audit :** 30 Décembre 2025  
**Auditeur :** Expert en sécurité et architecture .NET  
**Version de l'application :** 1.0  
**Framework :** ASP.NET Core 9.0

---

## EXECUTIVE SUMMARY

### Score Global : **7.2/10** ⚠️

L'application présente une architecture solide avec une séparation des responsabilités claire (Clean Architecture). Cependant, plusieurs vulnérabilités critiques et des points d'amélioration importants ont été identifiés, notamment en matière de sécurité, de gestion des erreurs et de performance.

### Points Forts ✅
- Architecture Clean Architecture bien structurée
- Utilisation d'Entity Framework Core (protection contre SQL Injection)
- Authentification par cookies avec BCrypt pour les mots de passe
- Logging structuré avec Serilog
- Tests unitaires et d'intégration présents
- Pagination et cache implémentés

### Points Critiques ⚠️
- **CRITIQUE** : Contrôleur de test accessible publiquement avec fonctionnalités sensibles
- **CRITIQUE** : Chaîne de connexion en clair dans appsettings.json
- **CRITIQUE** : Mot de passe admin hardcodé dans le code
- **ÉLEVÉ** : Absence de validation stricte des fichiers uploadés
- **ÉLEVÉ** : Gestion d'erreurs insuffisante dans certains contrôleurs
- **MOYEN** : Risques de requêtes N+1 dans plusieurs endroits
- **MOYEN** : Configuration de sécurité des cookies à améliorer

---

## 1. SÉCURITÉ

### 1.1 Authentification et Autorisation

#### ✅ Points Positifs
- Utilisation de BCrypt pour le hachage des mots de passe (bonne pratique)
- Authentification par cookies avec `HttpOnly` activé
- Autorisation basée sur les rôles (RBAC)
- Protection CSRF avec `ValidateAntiForgeryToken` sur 73 actions POST

#### ⚠️ Vulnérabilités Identifiées

**CRITIQUE - VUL-001 : Contrôleur de Test Accessible Publiquement**
```csharp
// Controllers/TestController.cs
[AllowAnonymous]
public class TestController : Controller
{
    public IActionResult CheckAdmin() { ... }
    public IActionResult ResetAdminPassword() { ... }
}
```
**Impact :** Un attaquant peut réinitialiser le mot de passe admin sans authentification.  
**Recommandation :** 
- Supprimer ce contrôleur en production
- Ou le protéger avec `[Authorize(Roles = "AdminIT")]`
- Utiliser des variables d'environnement pour les tests

**CRITIQUE - VUL-002 : Mot de Passe Admin Hardcodé**
```csharp
// Program.cs ligne 97
var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
Log.Information("Utilisateur admin créé avec succès. Matricule: admin, Mot de passe: Admin@123");
```
**Impact :** Le mot de passe par défaut est visible dans les logs et le code source.  
**Recommandation :**
- Générer un mot de passe aléatoire au premier démarrage
- Forcer le changement du mot de passe au premier login
- Ne jamais logger les mots de passe

**ÉLEVÉ - VUL-003 : Configuration Cookie Sécurisée Insuffisante**
```csharp
// Program.cs ligne 64
options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
```
**Impact :** En HTTP, les cookies peuvent être interceptés.  
**Recommandation :**
```csharp
options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // En production
options.Cookie.SameSite = SameSiteMode.Strict; // Protection CSRF renforcée
```

**MOYEN - VUL-004 : Timeout de Session**
```csharp
options.ExpireTimeSpan = TimeSpan.FromHours(1);
```
**Impact :** 1 heure peut être trop long pour une application sensible.  
**Recommandation :** Réduire à 30 minutes et implémenter un mécanisme de "Remember Me" optionnel.

### 1.2 Gestion des Secrets

**CRITIQUE - VUL-005 : Chaîne de Connexion en Clair**
```json
// appsettings.json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=GestProjetDb;Trusted_Connection=True;Encrypt=False;"
}
```
**Impact :** La chaîne de connexion est visible dans le code source.  
**Recommandation :**
- Utiliser Azure Key Vault ou User Secrets pour le développement
- Utiliser des variables d'environnement en production
- Activer le chiffrement de la connexion SQL Server (`Encrypt=True`)

### 1.3 Validation des Entrées

#### ✅ Points Positifs
- Utilisation de `ValidateAntiForgeryToken` sur les actions POST
- Validation côté serveur avec `ModelState.IsValid`
- Entity Framework Core protège contre SQL Injection

#### ⚠️ Vulnérabilités Identifiées

**ÉLEVÉ - VUL-006 : Validation des Fichiers Uploadés Insuffisante**
```csharp
// Infrastructure/Services/FileStorageService.cs
public async Task<string> SaveFileAsync(IFormFile file, string subfolder, string? identifier = null)
{
    if (file == null || file.Length == 0)
        throw new ArgumentException("Le fichier est vide.");
    // Pas de validation de type MIME, pas de scan antivirus, pas de limite de taille stricte
}
```
**Impact :** Risque d'upload de fichiers malveillants (malware, scripts).  
**Recommandation :**
- Valider le type MIME réel du fichier (pas seulement l'extension)
- Implémenter une limite de taille stricte (ex: 10MB)
- Scanner les fichiers avec un antivirus
- Renommer les fichiers avec un GUID pour éviter les path traversal
- Valider le contenu des fichiers (ex: vérifier les en-têtes)

**MOYEN - VUL-007 : Validation des GUIDs**
```csharp
// Plusieurs contrôleurs
var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
```
**Impact :** `Guid.Parse` peut lever une exception si la valeur est invalide.  
**Recommandation :**
```csharp
if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
    return Unauthorized();
```

### 1.4 Protection CSRF

✅ **Bien implémenté** : 73 actions POST protégées avec `[ValidateAntiForgeryToken]`

---

## 2. ARCHITECTURE ET CODE QUALITY

### 2.1 Architecture

#### ✅ Points Positifs
- Clean Architecture bien respectée (Domain/Application/Infrastructure)
- Séparation claire des responsabilités
- Utilisation d'interfaces pour l'injection de dépendances
- Pattern Repository (via DbContext)

#### ⚠️ Points d'Amélioration

**MOYEN - ARCH-001 : Duplication de Code**
```csharp
// Program.cs lignes 35-40
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();
// Cache
builder.Services.AddMemoryCache(); // Duplication
```
**Recommandation :** Supprimer la duplication.

**MOYEN - ARCH-002 : Noms d'Interfaces Incohérents**
- Certaines interfaces dans `Infrastructure.Services`
- D'autres dans `Application.Common.Interfaces`
**Recommandation :** Centraliser toutes les interfaces dans `Application.Common.Interfaces`.

### 2.2 Gestion des Erreurs

#### ⚠️ Vulnérabilités Identifiées

**ÉLEVÉ - ERR-001 : Gestion d'Erreurs Incomplète**
```csharp
// Plusieurs contrôleurs
catch (Exception ex)
{
    ModelState.AddModelError(string.Empty, $"Erreur : {ex.Message}");
    return View(model);
}
```
**Impact :** Exposition d'informations sensibles dans les messages d'erreur.  
**Recommandation :**
- Implémenter un middleware de gestion d'erreurs global
- Logger les erreurs détaillées sans les exposer à l'utilisateur
- Retourner des messages d'erreur génériques à l'utilisateur

**MOYEN - ERR-002 : Absence de Try-Catch dans Certaines Actions**
Plusieurs actions critiques n'ont pas de gestion d'erreurs.  
**Recommandation :** Ajouter un try-catch global ou un middleware.

### 2.3 Performance

#### ⚠️ Vulnérabilités Identifiées

**MOYEN - PERF-001 : Risques de Requêtes N+1**
```csharp
// Controllers/ProjetController.cs - Exemple
foreach (var projet in projets)
{
    var auditLogs = await _db.AuditLogs.Where(...).ToListAsync(); // N+1
    var totalLivrables = await _db.LivrablesProjets.CountAsync(...); // N+1
}
```
**Impact :** Performance dégradée avec un grand nombre de projets.  
**Recommandation :**
- Utiliser `Include()` et `ThenInclude()` pour charger les données en une seule requête
- Utiliser des projections avec `Select()` pour éviter les requêtes multiples
- Implémenter un cache pour les données fréquemment accédées

**MOYEN - PERF-002 : Pagination Non Appliquée Partout**
Certaines listes peuvent être très longues sans pagination.  
**Recommandation :** Appliquer la pagination à toutes les listes.

**FAIBLE - PERF-003 : Cache Non Utilisé Partout**
Le service de cache existe mais n'est pas utilisé partout où il devrait l'être.  
**Recommandation :** Utiliser le cache pour les données de référence (Directions, Services, etc.).

---

## 3. BASE DE DONNÉES

### 3.1 Configuration

#### ✅ Points Positifs
- Entity Framework Core avec migrations
- Relations bien configurées avec `OnDelete` approprié
- Index sur les colonnes fréquemment interrogées (Notifications)

#### ⚠️ Points d'Amélioration

**MOYEN - DB-001 : Pas de Chiffrement de Connexion**
```json
"Encrypt=False"
```
**Recommandation :** Activer `Encrypt=True` et configurer un certificat.

**MOYEN - DB-002 : Pas de Timeout Configuré**
**Recommandation :** Configurer un timeout de commande SQL.

**FAIBLE - DB-003 : Pas de Backup Automatique Configuré**
**Recommandation :** Documenter la stratégie de backup.

### 3.2 Requêtes

#### ✅ Points Positifs
- Utilisation d'Entity Framework Core (protection SQL Injection)
- Requêtes asynchrones (`ToListAsync`, `FirstOrDefaultAsync`)

#### ⚠️ Points d'Amélioration

**MOYEN - DB-004 : Requêtes Synchrones Résiduelles**
Quelques `ToList()`, `FirstOrDefault()` synchrones trouvés.  
**Recommandation :** Remplacer par les versions asynchrones.

---

## 4. LOGGING ET MONITORING

### 4.1 Logging

#### ✅ Points Positifs
- Serilog configuré avec rotation quotidienne
- Logging structuré avec contexte
- Logs d'erreur dans `AuditService`

#### ⚠️ Points d'Amélioration

**MOYEN - LOG-001 : Logs Sensibles**
```csharp
Log.Information("Utilisateur admin créé avec succès. Matricule: admin, Mot de passe: Admin@123");
```
**Recommandation :** Ne jamais logger les mots de passe, même hashés.

**FAIBLE - LOG-002 : Pas de Logging de Performance**
**Recommandation :** Ajouter des logs de performance pour les opérations longues.

### 4.2 Monitoring

**FAIBLE - MON-001 : Pas d'APM (Application Performance Monitoring)**
**Recommandation :** Intégrer Application Insights ou un outil similaire.

---

## 5. TESTS

### 5.1 Tests Unitaires

#### ✅ Points Positifs
- Tests unitaires présents pour les services critiques
- Utilisation de Moq et FluentAssertions
- Tests avec base de données InMemory

#### ⚠️ Points d'Amélioration

**MOYEN - TEST-001 : Couverture de Tests Insuffisante**
Seulement 3 services testés sur ~10 services.  
**Recommandation :** Augmenter la couverture à au moins 70%.

**FAIBLE - TEST-002 : Pas de Tests de Sécurité**
**Recommandation :** Ajouter des tests pour :
- Tentatives de contournement d'autorisation
- Validation des entrées
- Protection CSRF

### 5.2 Tests d'Intégration

#### ✅ Points Positifs
- Tests d'intégration pour les workflows critiques
- Utilisation de `WebApplicationFactory`

**FAIBLE - TEST-003 : Couverture Limitée**
**Recommandation :** Ajouter des tests pour tous les workflows métier.

---

## 6. CONFIGURATION ET DÉPLOIEMENT

### 6.1 Configuration

#### ⚠️ Vulnérabilités Identifiées

**CRITIQUE - CONFIG-001 : Secrets dans appsettings.json**
**Recommandation :**
- Utiliser User Secrets pour le développement
- Utiliser Azure Key Vault ou variables d'environnement en production
- Ajouter `appsettings.json` au `.gitignore` si nécessaire

**MOYEN - CONFIG-002 : Pas de Configuration par Environnement**
**Recommandation :** Créer `appsettings.Production.json` avec des configurations spécifiques.

### 6.2 Déploiement

**FAIBLE - DEPLOY-001 : Pas de Documentation de Déploiement**
**Recommandation :** Documenter :
- Les prérequis
- Les étapes de déploiement
- La configuration requise
- Les procédures de rollback

---

## 7. RECOMMANDATIONS PRIORITAIRES

### Priorité CRITIQUE (À corriger immédiatement)

1. **Supprimer ou sécuriser `TestController`**
   - Supprimer en production OU
   - Protéger avec `[Authorize(Roles = "AdminIT")]`

2. **Sécuriser les secrets**
   - Déplacer la chaîne de connexion vers User Secrets / Key Vault
   - Ne plus hardcoder le mot de passe admin

3. **Améliorer la validation des fichiers**
   - Valider le type MIME réel
   - Implémenter une limite de taille stricte
   - Scanner les fichiers

### Priorité ÉLEVÉE (À corriger sous 1 semaine)

4. **Améliorer la gestion d'erreurs**
   - Implémenter un middleware global
   - Ne pas exposer les détails d'erreur aux utilisateurs

5. **Sécuriser les cookies**
   - `SecurePolicy = Always` en production
   - `SameSite = Strict`

6. **Corriger les requêtes N+1**
   - Utiliser `Include()` et `ThenInclude()`
   - Implémenter des projections

### Priorité MOYENNE (À corriger sous 1 mois)

7. **Augmenter la couverture de tests**
   - Atteindre 70% de couverture
   - Ajouter des tests de sécurité

8. **Améliorer le logging**
   - Ne jamais logger les secrets
   - Ajouter des logs de performance

9. **Documenter le déploiement**
   - Créer un guide de déploiement
   - Documenter les configurations

---

## 8. CONCLUSION

L'application présente une base solide avec une architecture bien pensée. Cependant, plusieurs vulnérabilités critiques doivent être corrigées avant la mise en production, notamment concernant la gestion des secrets, la validation des fichiers et la sécurité des endpoints de test.

**Score par Catégorie :**
- Sécurité : **6.5/10** ⚠️
- Architecture : **8.0/10** ✅
- Performance : **7.0/10** ⚠️
- Tests : **6.0/10** ⚠️
- Documentation : **5.0/10** ⚠️

**Recommandation Globale :** Corriger les vulnérabilités critiques avant la mise en production, puis traiter les points de priorité élevée dans les semaines suivantes.

---

**Fin du Rapport d'Audit**

