# Démarrage Rapide - Tests Fonctionnels

## 🚀 Exécution en 3 Étapes

### Étape 1: Vérifier les prérequis
```bash
# Vérifier que .NET 9.0 est installé
dotnet --version
```
Si .NET n'est pas installé, téléchargez-le depuis: https://dotnet.microsoft.com/download

### Étape 2: Restaurer les dépendances
```bash
# Depuis la racine du projet GestionProjects
dotnet restore Tests/GestionProjects.Tests.csproj
```

### Étape 3: Exécuter les tests
```bash
# Option A: Script automatisé (Windows)
cd Tests
.\run-tests.ps1

# Option B: Script automatisé (Linux/Mac)
cd Tests
chmod +x run-tests.sh
./run-tests.sh

# Option C: Commande directe
dotnet test Tests/GestionProjects.Tests.csproj --verbosity normal
```

## 📊 Résultats Attendus

Si tout fonctionne correctement, vous devriez voir:
```
✅ Tous les tests ont réussi!
Passed!  - Failed:     0, Passed:    XX, Skipped:     0, Total:    XX
```

## 🔍 Tests Disponibles

| Module | Nombre de Tests | Criticité |
|--------|----------------|-----------|
| Authentification | 5+ | Bloquante |
| Sécurité | 6+ | Bloquante |
| Demande de Projet | 10+ | Bloquante/Majeure |
| Validation DM | 8+ | Bloquante |
| Validation DSI | 10+ | Bloquante |
| Gestion Projet | 15+ | Bloquante/Majeure |

## 🎯 Commandes Utiles

### Exécuter un module spécifique
```bash
# Tests d'authentification
dotnet test --filter "FullyQualifiedName~AuthenticationTests"

# Tests de sécurité
dotnet test --filter "FullyQualifiedName~SecurityTests"

# Tests de demande de projet
dotnet test --filter "FullyQualifiedName~DemandeProjetTests"
```

### Exécuter avec couverture de code
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Voir les détails des tests
```bash
dotnet test --verbosity detailed
```

## 📝 Prochaines Étapes

1. ✅ Exécuter tous les tests
2. 📊 Consulter le rapport de couverture
3. 📖 Lire `GUIDE_TESTS.md` pour plus de détails
4. 🔧 Ajouter de nouveaux tests si nécessaire

## ❓ Problèmes Courants

### Erreur: "Project file does not exist"
**Solution**: Assurez-vous d'être dans le bon répertoire
```bash
cd /chemin/vers/GestionProjects
```

### Erreur: "Unable to find package"
**Solution**: Restaurer les packages
```bash
dotnet restore Tests/GestionProjects.Tests.csproj
```

### Tests qui échouent
**Solution**: Vérifier les logs détaillés
```bash
dotnet test --verbosity detailed
```

## 📚 Documentation Complète

- `README.md` - Documentation détaillée des tests
- `GUIDE_TESTS.md` - Guide complet d'exécution
- `RESULTATS_TESTS.md` - Résultats et statistiques

## 💡 Astuce

Pour un développement rapide, utilisez le mode watch:
```bash
dotnet watch test Tests/GestionProjects.Tests.csproj
```
Les tests se relanceront automatiquement à chaque modification du code!

---

**Besoin d'aide?** Consultez `GUIDE_TESTS.md` ou contactez l'équipe de développement.
