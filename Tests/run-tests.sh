#!/bin/bash
# Script Bash pour exécuter les tests et générer un rapport
# Usage: ./run-tests.sh

echo "========================================"
echo "  Tests Fonctionnels - Gestion Projets"
echo "========================================"
echo ""

# Vérifier que .NET SDK est installé
echo "Vérification de .NET SDK..."
dotnet_version=$(dotnet --version 2>&1)
if [ $? -ne 0 ]; then
    echo "❌ .NET SDK n'est pas installé!"
    exit 1
fi
echo "✅ .NET SDK version: $dotnet_version"
echo ""

# Restaurer les packages
echo "Restauration des packages NuGet..."
dotnet restore GestionProjects.Tests.csproj
if [ $? -ne 0 ]; then
    echo "❌ Échec de la restauration des packages!"
    exit 1
fi
echo "✅ Packages restaurés avec succès"
echo ""

# Compiler le projet de test
echo "Compilation du projet de test..."
dotnet build GestionProjects.Tests.csproj --configuration Release
if [ $? -ne 0 ]; then
    echo "❌ Échec de la compilation!"
    exit 1
fi
echo "✅ Compilation réussie"
echo ""

# Exécuter les tests avec couverture de code
echo "Exécution des tests..."
echo ""
dotnet test GestionProjects.Tests.csproj \
    --configuration Release \
    --no-build \
    --verbosity normal \
    --collect:"XPlat Code Coverage" \
    --logger "console;verbosity=detailed"

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo "  ✅ TOUS LES TESTS ONT RÉUSSI!"
    echo "========================================"
else
    echo ""
    echo "========================================"
    echo "  ❌ CERTAINS TESTS ONT ÉCHOUÉ!"
    echo "========================================"
    exit 1
fi

echo ""
echo "Rapport de couverture généré dans: TestResults/"
echo ""
