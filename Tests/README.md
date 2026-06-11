# Tests Fonctionnels - Gestion de Projets

Ce dossier contient les tests fonctionnels automatisés pour l'application de gestion de projets, basés sur le document de test fourni.

## Structure des Tests

```
Tests/
├── Helpers/
│   └── TestDbContextFactory.cs          # Factory pour créer des contextes de test
├── Unit/
│   ├── Authentication/
│   │   └── AuthenticationTests.cs       # Tests AUTH-01 à AUTH-04
│   ├── Security/
│   │   └── SecurityTests.cs             # Tests AUTH-05 à AUTH-10
│   ├── DemandeProjet/
│   │   └── DemandeProjetTests.cs        # Tests DEM-01 à DEM-30
│   ├── Validation/
│   │   ├── ValidationDirecteurMetierTests.cs  # Tests VALM-01 à VALM-15
│   │   └── ValidationDSITests.cs              # Tests VALD-01 à VALD-13
│   └── Projet/
│       └── ProjetTests.cs               # Tests ANA, CHR, PLAN, EXEC, UAT, CLOT
└── GestionProjects.Tests.csproj
```

## Technologies Utilisées

- **xUnit**: Framework de test
- **FluentAssertions**: Assertions fluides et lisibles
- **Moq**: Framework de mocking
- **Entity Framework Core InMemory**: Base de données en mémoire pour les tests
- **Microsoft.AspNetCore.Mvc.Testing**: Tests d'intégration ASP.NET Core

## Prérequis

- .NET 9.0 SDK
- Visual Studio 2022 ou VS Code avec extension C#

## Installation

1. Restaurer les packages NuGet:
```bash
dotnet restore Tests/GestionProjects.Tests.csproj
```

## Exécution des Tests

### Exécuter tous les tests
```bash
dotnet test Tests/GestionProjects.Tests.csproj
```

### Exécuter les tests avec détails
```bash
dotnet test Tests/GestionProjects.Tests.csproj --verbosity detailed
```

### Exécuter les tests avec couverture de code
```bash
dotnet test Tests/GestionProjects.Tests.csproj --collect:"XPlat Code Coverage"
```

### Exécuter une classe de test spécifique
```bash
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~AuthenticationTests"
```

### Exécuter un test spécifique
```bash
dotnet test Tests/GestionProjects.Tests.csproj --filter "FullyQualifiedName~AUTH01_ConnexionAvecCompteValide_DoitReussir"
```

## Couverture des Tests

### Module Authentification (AUTH-01 à AUTH-04)
- ✅ AUTH-01: Connexion avec compte CIT valide
- ✅ AUTH-02: Première connexion utilisateur non référencé
- ✅ AUTH-03: Récupération automatique des informations
- ✅ AUTH-04: Détermination automatique de la direction métier

### Module Sécurité (AUTH-05 à AUTH-10)
- ✅ AUTH-05: Interdiction d'accès admin pour Demandeur
- ✅ AUTH-06: Isolation des données entre directions
- ✅ AUTH-07: Visibilité limitée pour Chef de projet
- ✅ AUTH-08: Accès global pour DSI
- ✅ AUTH-09: Droits complets pour Admin IT
- ✅ AUTH-10: Blocage d'accès non autorisé (logique de test)

### Module Demande de Projet (DEM-01 à DEM-30)
- ✅ DEM-01 à DEM-13: Présence des champs du formulaire
- ✅ DEM-14 à DEM-21: Validation des champs obligatoires
- ✅ DEM-22: Longueur du titre
- ✅ DEM-23: Caractères spéciaux
- ✅ DEM-27 à DEM-28: Création et statut initial

### Module Validation Directeur Métier (VALM-01 à VALM-15)
- ✅ VALM-01 et VALM-02: Affichage et filtrage des demandes
- ✅ VALM-03: Action Valider
- ✅ VALM-06 à VALM-08: Modification des champs
- ✅ VALM-10 et VALM-11: Demande de correction
- ✅ VALM-13 et VALM-14: Rejet avec commentaire

### Module Validation DSI (VALD-01 à VALD-13)
- ✅ VALD-01: Liste des demandes à valider
- ✅ VALD-02 et VALD-03: Validation et création de projet
- ✅ VALD-04: Statut initial du projet
- ✅ VALD-06 et VALD-07: Rejet avec commentaire
- ✅ VALD-09 et VALD-10: Retour vers demandeur/directeur
- ✅ VALD-11 à VALD-13: Délégation de validation

### Module Projet (ANA, CHR, PLAN, EXEC, UAT, CLOT)
- ✅ ANA-01 à ANA-04: Équipe projet
- ✅ ANA-10 à ANA-16: Gestion des risques
- ✅ CHR-01 à CHR-05: Charte projet
- ✅ PLAN-05 et PLAN-11: Budget prévisionnel
- ✅ EXEC-04 à EXEC-09: Pourcentage d'avancement
- ✅ EXEC-10: État projet (Vert/Orange/Rouge)
- ✅ UAT-07 et UAT-08: Statuts recette et MEP
- ✅ CLOT-01 à CLOT-08: Bilan et leçons apprises
- ✅ CLOT-12 à CLOT-15: Workflow de clôture

## Conventions de Nommage

Les tests suivent la convention:
```
[ID_TEST]_[Description]_[ResultatAttendu]
```

Exemples:
- `AUTH01_ConnexionAvecCompteValide_DoitReussir`
- `DEM14_21_ValidationChampsObligatoires_DoitEchouer`
- `VALM03_ValidationDemande_DoitChangerStatut`

## Données de Test

Les données de test sont créées automatiquement via `TestDbContextFactory`:
- Directions: DSI, Finance
- Utilisateurs: Admin IT, Demandeur, Directeur Métier, DSI, Chef de Projet
- Mots de passe: Format `[Role]@123` (ex: `Admin@123`)

## Résultats Attendus

Tous les tests doivent passer (statut vert) pour valider la conformité de l'application avec les spécifications.

## Rapport de Test

Après exécution, un rapport détaillé est généré avec:
- Nombre de tests exécutés
- Nombre de tests réussis/échoués
- Temps d'exécution
- Couverture de code (si activée)

## Intégration Continue

Ces tests peuvent être intégrés dans un pipeline CI/CD:
```yaml
# Exemple pour Azure DevOps
- task: DotNetCoreCLI@2
  displayName: 'Run Tests'
  inputs:
    command: 'test'
    projects: 'Tests/GestionProjects.Tests.csproj'
    arguments: '--configuration Release --collect:"XPlat Code Coverage"'
```

## Maintenance

Pour ajouter de nouveaux tests:
1. Créer une nouvelle classe de test dans le dossier approprié
2. Hériter de `IDisposable` pour le nettoyage
3. Utiliser `TestDbContextFactory` pour créer le contexte
4. Suivre les conventions de nommage
5. Documenter avec des commentaires XML

## Support

Pour toute question ou problème:
- Consulter la documentation du projet
- Vérifier les logs de test
- Contacter l'équipe de développement
