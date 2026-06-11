# Guide d'Exécution des Tests Fonctionnels

## Introduction

Ce guide explique comment exécuter les tests fonctionnels automatisés pour l'application de gestion de projets. Les tests sont basés sur le document de test fourni et couvrent tous les modules critiques de l'application.

## Prérequis

- .NET 9.0 SDK installé
- Visual Studio 2022, VS Code, ou ligne de commande
- Accès au code source du projet

## Structure des Tests

```
GestionProjects/
├── Tests/
│   ├── Helpers/                    # Utilitaires de test
│   ├── Unit/                       # Tests unitaires
│   │   ├── Authentication/         # Tests d'authentification
│   │   ├── Security/               # Tests de sécurité
│   │   ├── DemandeProjet/          # Tests de demande de projet
│   │   ├── Validation/             # Tests de validation
│   │   └── Projet/                 # Tests de gestion de projet
│   ├── Integration/                # Tests d'intégration
│   ├── GestionProjects.Tests.csproj
│   ├── README.md
│   ├── RESULTATS_TESTS.md
│   ├── run-tests.ps1               # Script PowerShell
│   └── run-tests.sh                # Script Bash
└── GUIDE_TESTS.md                  # Ce fichier
```

## Installation

### 1. Restaurer les dépendances

```bash
# Depuis la racine du projet
dotnet restore Tests/GestionProjects.Tests.csproj
```

### 2. Vérifier la compilation

```bash
dotnet build Tests/GestionProjects.Tests.csproj
```

## Exécution des Tests

### Méthode 1: Scripts Automatisés (Recommandé)

#### Windows (PowerShell)
```powershell
cd Tests
.\run-tests.ps1
```

#### Linux/Mac (Bash)
```bash
cd Tests
chmod +x run-tests.sh
./run-tests.sh
```

### Méthode 2: Ligne de Commande

#### Exécuter tous les tests
```bash
dotnet test Tests/GestionProjects.Tests.csproj
```

#### Exécuter avec détails
```bash
dotnet test Tests/GestionProjects.Tests.csproj --verbosity detailed
```

#### Exécuter avec couverture de code
```bash
dotnet test Tests/GestionProjects.Tests.csproj --collect:"XPlat Code Coverage"
```

#### Exécuter un module spécifique
```bash
# Tests d'authentification
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~AuthenticationTests"

# Tests de sécurité
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~SecurityTests"

# Tests de demande de projet
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~DemandeProjetTests"

# Tests de validation
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~ValidationTests"

# Tests de projet
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~ProjetTests"
```

#### Exécuter un test spécifique
```bash
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~AUTH01_ConnexionAvecCompteValide_DoitReussir"
```

### Méthode 3: Visual Studio

1. Ouvrir la solution `GestionProjects.sln`
2. Ouvrir Test Explorer: `Test > Test Explorer` ou `Ctrl+E, T`
3. Cliquer sur "Run All" pour exécuter tous les tests
4. Ou cliquer avec le bouton droit sur un test spécifique et sélectionner "Run"

### Méthode 4: VS Code

1. Installer l'extension ".NET Core Test Explorer"
2. Ouvrir le projet dans VS Code
3. Cliquer sur l'icône de test dans la barre latérale
4. Exécuter les tests individuellement ou tous ensemble

## Rapport de Couverture de Code

### Générer le rapport

```bash
# 1. Installer ReportGenerator (une seule fois)
dotnet tool install -g dotnet-reportgenerator-globaltool

# 2. Exécuter les tests avec couverture
dotnet test Tests/GestionProjects.Tests.csproj --collect:"XPlat Code Coverage"

# 3. Générer le rapport HTML
reportgenerator -reports:Tests/TestResults/**/coverage.cobertura.xml -targetdir:Tests/TestResults/CoverageReport -reporttypes:Html

# 4. Ouvrir le rapport
# Windows
start Tests/TestResults/CoverageReport/index.html

# Linux
xdg-open Tests/TestResults/CoverageReport/index.html

# Mac
open Tests/TestResults/CoverageReport/index.html
```

## Modules de Tests Disponibles

### 1. Authentification (AUTH-01 à AUTH-04)
- Connexion avec compte valide
- Utilisateur non référencé
- Récupération automatique des informations
- Détermination de la direction métier

**Exécution**:
```bash
dotnet test --filter "FullyQualifiedName~AuthenticationTests"
```

### 2. Sécurité (AUTH-05 à AUTH-10)
- Contrôle d'accès par rôle
- Isolation des données
- Visibilité des projets
- Droits d'administration

**Exécution**:
```bash
dotnet test --filter "FullyQualifiedName~SecurityTests"
```

### 3. Demande de Projet (DEM-01 à DEM-30)
- Validation des champs
- Création de demande
- Gestion des documents
- Workflow de soumission

**Exécution**:
```bash
dotnet test --filter "FullyQualifiedName~DemandeProjetTests"
```

### 4. Validation Directeur Métier (VALM-01 à VALM-15)
- Affichage des demandes
- Validation/Rejet
- Demande de correction
- Modification des champs

**Exécution**:
```bash
dotnet test --filter "FullyQualifiedName~ValidationDirecteurMetierTests"
```

### 5. Validation DSI (VALD-01 à VALD-13)
- Validation finale
- Création de projet
- Délégation de validation
- Retour de demande

**Exécution**:
```bash
dotnet test --filter "FullyQualifiedName~ValidationDSITests"
```

### 6. Gestion de Projet (ANA, CHR, PLAN, EXEC, UAT, CLOT)
- Équipe projet et risques
- Charte projet
- Planification et budget
- Exécution et avancement
- Recette et MEP
- Clôture et bilan

**Exécution**:
```bash
dotnet test --filter "FullyQualifiedName~ProjetTests"
```

## Interprétation des Résultats

### Résultat Positif
```
Passed!  - Failed:     0, Passed:    45, Skipped:     0, Total:    45
```
✅ Tous les tests ont réussi

### Résultat avec Échecs
```
Failed!  - Failed:     3, Passed:    42, Skipped:     0, Total:    45
```
❌ 3 tests ont échoué - Vérifier les détails dans la sortie

### Détails d'un Échec
```
Failed AUTH01_ConnexionAvecCompteValide_DoitReussir [< 1 ms]
  Error Message:
   Expected result to be of type RedirectToActionResult, but found ViewResult.
```

## Résolution des Problèmes

### Erreur: "dotnet command not found"
**Solution**: Installer .NET 9.0 SDK depuis https://dotnet.microsoft.com/download

### Erreur: "Project file does not exist"
**Solution**: Vérifier que vous êtes dans le bon répertoire
```bash
cd /chemin/vers/GestionProjects
```

### Erreur: "Unable to find package"
**Solution**: Restaurer les packages
```bash
dotnet restore Tests/GestionProjects.Tests.csproj
```

### Tests qui échouent de manière intermittente
**Solution**: Problème potentiel de concurrence avec la base de données en mémoire
- Chaque test utilise une base de données isolée
- Vérifier que les tests sont bien indépendants

### Erreur de compilation
**Solution**: Nettoyer et recompiler
```bash
dotnet clean Tests/GestionProjects.Tests.csproj
dotnet build Tests/GestionProjects.Tests.csproj
```

## Intégration Continue (CI/CD)

### Azure DevOps
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Tests'
  inputs:
    command: 'test'
    projects: 'Tests/GestionProjects.Tests.csproj'
    arguments: '--configuration Release --collect:"XPlat Code Coverage"'
```

### GitHub Actions
```yaml
- name: Run Tests
  run: dotnet test Tests/GestionProjects.Tests.csproj --configuration Release --collect:"XPlat Code Coverage"
```

### GitLab CI
```yaml
test:
  script:
    - dotnet test Tests/GestionProjects.Tests.csproj --configuration Release --collect:"XPlat Code Coverage"
```

## Bonnes Pratiques

1. **Exécuter les tests avant chaque commit**
   ```bash
   dotnet test Tests/GestionProjects.Tests.csproj
   ```

2. **Vérifier la couverture de code régulièrement**
   - Objectif: > 80% de couverture

3. **Ajouter des tests pour chaque nouvelle fonctionnalité**
   - Suivre les conventions de nommage existantes

4. **Maintenir les tests à jour**
   - Mettre à jour les tests lors de modifications du code

5. **Documenter les tests complexes**
   - Utiliser des commentaires XML

## Support

Pour toute question ou problème:
1. Consulter la documentation dans `Tests/README.md`
2. Vérifier les logs de test détaillés
3. Consulter `Tests/RESULTATS_TESTS.md` pour les résultats attendus
4. Contacter l'équipe de développement

## Ressources Supplémentaires

- [Documentation xUnit](https://xunit.net/)
- [Documentation FluentAssertions](https://fluentassertions.com/)
- [Documentation .NET Testing](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [Guide de couverture de code](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)
