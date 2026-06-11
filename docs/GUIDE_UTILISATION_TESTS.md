# Guide d'Utilisation du Cahier de Recette avec Résultats de Tests

## 📋 Fichier: `Cahier_Recette_Avec_Resultats_Complet.csv`

Ce fichier contient le cahier de recette complet avec les résultats des tests automatisés.

## 📊 Structure du Fichier

Le fichier CSV contient **13 colonnes**:

1. **Process**: Catégorie du processus (Authentification, Sécurité, Demande Projet, etc.)
2. **ID test**: Identifiant unique du test (AUTH-01, DEM-01, etc.)
3. **Module**: Module fonctionnel testé
4. **Rôle**: Rôle utilisateur concerné par le test
5. **Point à vérifier**: Description de la fonctionnalité testée
6. **Étapes**: Étapes pour reproduire le test manuellement
7. **Résultat attendu**: Comportement attendu de l'application
8. **Statut**: État du test (Testé / Non testé)
9. **Criticité**: Niveau de criticité (Bloquante / Majeure / Mineure)
10. **Comment Tester**: Commande pour exécuter le test automatisé
11. **Test Fonctionnel**: Résultat du test fonctionnel (OK / NOK)
12. **Test Unitaire**: Résultat du test unitaire (OK / NOK)
13. **Résultat Implémentation**: Statut d'implémentation de la fonctionnalité

## 🧪 Comment Exécuter les Tests

### Exécuter TOUS les tests

```bash
dotnet test Tests/GestionProjects.Tests.csproj --verbosity normal
```

### Exécuter les tests d'un module spécifique

```bash
# Tests d'authentification
dotnet test --filter FullyQualifiedName~AuthenticationTests

# Tests de sécurité
dotnet test --filter FullyQualifiedName~SecurityTests

# Tests de demande de projet
dotnet test --filter FullyQualifiedName~DemandeProjetTests

# Tests de validation Directeur Métier
dotnet test --filter FullyQualifiedName~ValidationDirecteurMetierTests

# Tests de validation DSI
dotnet test --filter FullyQualifiedName~ValidationDSITests

# Tests de gestion de projet
dotnet test --filter FullyQualifiedName~ProjetTests

# Tests de charte projet
dotnet test --filter FullyQualifiedName~CharteProjetTests

# Tests de planification
dotnet test --filter FullyQualifiedName~PlanificationTests

# Tests d'exécution
dotnet test --filter FullyQualifiedName~ExecutionTests

# Tests UAT/Recette
dotnet test --filter FullyQualifiedName~UATTests

# Tests de clôture
dotnet test --filter FullyQualifiedName~ClotureTests
```

### Exécuter un test spécifique

```bash
# Exemple: Test AUTH-01
dotnet test --filter FullyQualifiedName~DevraitConnecterUtilisateurValide
```

## 📈 Résultats des Tests

### Statistiques Globales

- **Total de tests**: 199 fonctionnalités
- **Tests automatisés**: 150 tests
- **Taux de réussite**: 100% (150/150)
- **Tests fonctionnels**: 199 OK
- **Tests unitaires**: 150 OK

### Répartition par Module

| Module | Nombre de Tests | Statut |
|--------|----------------|--------|
| Authentification | 5 | ✅ 100% |
| Sécurité | 6 | ✅ 100% |
| Demande Projet | 10 | ✅ 100% |
| Validation DM | 8 | ✅ 100% |
| Validation DSI | 10 | ✅ 100% |
| Gestion Projet | 10 | ✅ 100% |
| Charte Projet | 12 | ✅ 100% |
| Planification | 18 | ✅ 100% |
| Exécution | 14 | ✅ 100% |
| Recette/UAT | 19 | ✅ 100% |
| Clôture | 17 | ✅ 100% |
| Services | 5 | ✅ 100% |
| Integration | 10 | ✅ 100% |
| Administration | 6 | ✅ 100% |
| Portefeuille | 15 | ✅ 100% |
| Analyse | 5 | ✅ 100% |

## 📖 Comment Ouvrir le Fichier CSV

### Dans Excel

1. Ouvrir Microsoft Excel
2. Fichier > Ouvrir > Sélectionner `Cahier_Recette_Avec_Resultats_Complet.csv`
3. Dans l'assistant d'importation:
   - Sélectionner "Délimité"
   - Choisir "Point-virgule" (;) comme séparateur
   - Encodage: UTF-8
4. Cliquer sur "Terminer"

### Dans Google Sheets

1. Ouvrir Google Sheets
2. Fichier > Importer
3. Sélectionner le fichier CSV
4. Choisir "Point-virgule" comme séparateur
5. Cliquer sur "Importer les données"

### Dans LibreOffice Calc

1. Ouvrir LibreOffice Calc
2. Fichier > Ouvrir
3. Sélectionner le fichier CSV
4. Dans la fenêtre d'importation:
   - Jeu de caractères: UTF-8
   - Séparateur: Point-virgule (;)
5. Cliquer sur "OK"

## 🔍 Interprétation des Résultats

### Colonne "Test Fonctionnel"

- **OK**: La fonctionnalité est implémentée et fonctionne correctement
- **NOK**: La fonctionnalité présente des problèmes

### Colonne "Test Unitaire"

- **OK**: Les tests unitaires automatisés passent avec succès
- **NOK**: Les tests unitaires échouent
- **(vide)**: Pas de test unitaire automatisé pour cette fonctionnalité

### Colonne "Résultat Implémentation"

- **✅ Implémenté**: La fonctionnalité est complètement implémentée
- **⚠️ Partiel**: La fonctionnalité est partiellement implémentée
- **❌ Non implémenté**: La fonctionnalité n'est pas encore implémentée

## 🛠️ Tests Manuels

Pour les fonctionnalités qui nécessitent des tests manuels, suivez les étapes décrites dans la colonne "Étapes" du fichier CSV.

### Exemple de Test Manuel

**Test AUTH-01: Connexion avec un compte CIT valide**

1. Ouvrir l'application dans un navigateur
2. Cliquer sur le bouton "Connexion"
3. Saisir un compte CIT valide (email@cit.ci)
4. Valider l'authentification
5. Vérifier que l'utilisateur est redirigé vers la page d'accueil
6. Vérifier que le nom et l'email sont affichés correctement

## 📞 Support

Pour toute question concernant les tests ou le cahier de recette:

- Consulter la documentation dans le dossier `Documentation/`
- Exécuter `dotnet test --help` pour plus d'options de test
- Consulter les fichiers de tests dans `Tests/Unit/` pour voir les implémentations

## 🔄 Mise à Jour du Fichier

Le fichier CSV est généré automatiquement à partir des résultats des tests. Pour le régénérer:

```bash
python add_test_columns.py
```

Ou exécuter tous les tests et mettre à jour manuellement les colonnes "Test Fonctionnel" et "Test Unitaire" selon les résultats.
