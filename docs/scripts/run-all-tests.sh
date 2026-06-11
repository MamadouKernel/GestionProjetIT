#!/bin/bash
# Script pour exécuter tous les tests du projet Gestion Projets IT

echo "========================================"
echo "Tests Fonctionnels - Gestion Projets IT"
echo "========================================"
echo ""

# Vérifier que .NET SDK est installé
echo "Vérification de .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK n'est pas installé!"
    echo "Téléchargez-le depuis: https://dotnet.microsoft.com/download"
    exit 1
fi
dotnetVersion=$(dotnet --version)
echo "✅ .NET SDK version: $dotnetVersion"
echo ""

# Compiler le projet
echo "Compilation du projet de test..."
dotnet build Tests/GestionProjects.Tests.csproj --configuration Release --verbosity quiet
if [ $? -ne 0 ]; then
    echo "❌ Échec de la compilation!"
    exit 1
fi
echo "✅ Compilation réussie"
echo ""

# Exécuter les tests
echo "Exécution des tests..."
echo ""
dotnet test Tests/GestionProjects.Tests.csproj --configuration Release --no-build --verbosity normal

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo "✅ TOUS LES TESTS ONT RÉUSSI! 🎉"
    echo "========================================"
else
    echo ""
    echo "========================================"
    echo "❌ CERTAINS TESTS ONT ÉCHOUÉ"
    echo "========================================"
    exit 1
fi
