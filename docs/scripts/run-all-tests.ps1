#!/usr/bin/env pwsh
# Script pour exécuter tous les tests du projet Gestion Projets IT

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Tests Fonctionnels - Gestion Projets IT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que .NET SDK est installé
Write-Host "Vérification de .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET SDK n'est pas installé!" -ForegroundColor Red
    Write-Host "Téléchargez-le depuis: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ .NET SDK version: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# Compiler le projet
Write-Host "Compilation du projet de test..." -ForegroundColor Yellow
dotnet build Tests/GestionProjects.Tests.csproj --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Échec de la compilation!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Compilation réussie" -ForegroundColor Green
Write-Host ""

# Exécuter les tests
Write-Host "Exécution des tests..." -ForegroundColor Yellow
Write-Host ""
dotnet test Tests/GestionProjects.Tests.csproj --configuration Release --no-build --verbosity normal

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✅ TOUS LES TESTS ONT RÉUSSI! 🎉" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "❌ CERTAINS TESTS ONT ÉCHOUÉ" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}
