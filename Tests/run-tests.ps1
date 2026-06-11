# Script PowerShell pour exécuter les tests et générer un rapport
# Usage: .\run-tests.ps1

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Tests Fonctionnels - Gestion Projets" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que .NET SDK est installé
Write-Host "Vérification de .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET SDK n'est pas installé!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ .NET SDK version: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# Restaurer les packages
Write-Host "Restauration des packages NuGet..." -ForegroundColor Yellow
dotnet restore GestionProjects.Tests.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Échec de la restauration des packages!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Packages restaurés avec succès" -ForegroundColor Green
Write-Host ""

# Compiler le projet de test
Write-Host "Compilation du projet de test..." -ForegroundColor Yellow
dotnet build GestionProjects.Tests.csproj --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Échec de la compilation!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Compilation réussie" -ForegroundColor Green
Write-Host ""

# Exécuter les tests avec couverture de code
Write-Host "Exécution des tests..." -ForegroundColor Yellow
Write-Host ""
$testResult = dotnet test GestionProjects.Tests.csproj `
    --configuration Release `
    --no-build `
    --verbosity normal `
    --collect:"XPlat Code Coverage" `
    --logger "console;verbosity=detailed"

Write-Host ""
if ($LASTEXITCODE -eq 0) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  ✅ TOUS LES TESTS ONT RÉUSSI!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  ❌ CERTAINS TESTS ONT ÉCHOUÉ!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Rapport de couverture généré dans: TestResults/" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pour voir le rapport de couverture détaillé:" -ForegroundColor Yellow
Write-Host "  1. Installer ReportGenerator: dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor Gray
Write-Host "  2. Générer le rapport HTML: reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:TestResults/CoverageReport" -ForegroundColor Gray
Write-Host "  3. Ouvrir: TestResults/CoverageReport/index.html" -ForegroundColor Gray
Write-Host ""
