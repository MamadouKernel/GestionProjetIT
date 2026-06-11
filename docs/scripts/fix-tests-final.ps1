# Script final pour corriger tous les tests

Write-Host "Correction finale des tests..." -ForegroundColor Cyan

$files = @(
    "Tests/Unit/Projet/PlanificationTests.cs",
    "Tests/Unit/Projet/ExecutionTests.cs", 
    "Tests/Unit/Projet/UATTests.cs",
    "Tests/Unit/Projet/ClotureTests.cs",
    "Tests/Unit/Projet/CharteProjetTests.cs"
)

foreach ($file in $files) {
    Write-Host "Traitement de $file..." -ForegroundColor Yellow
    $content = Get-Content $file -Raw
    
    # Ajouter Commentaire et Version après DeposeParId pour les livrables multi-lignes
    $content = $content -replace '(DeposeParId = projet\.ChefProjetId!\.Value\s*)\n(\s*)\}', '$1,
$2    Commentaire = string.Empty,
$2    Version = "1.0"
$2}'
    
    # Ajouter Commentaire et Version pour les livrables inline (une ligne)
    $content = $content -replace '(DeposeParId = projet\.ChefProjetId!\.Value\s*)\}', '$1, Commentaire = string.Empty, Version = "1.0" }'
    
    # Corriger HistoriquePhaseProjet
    $content = $content -replace '(ModifieParId = projet\.ChefProjetId!\.Value\s*)\n(\s*)\}', '$1,
$2    Commentaire = string.Empty
$2}'
    
    # Remplacer _context.SaveChanges() par await _context.SaveChangesAsync()
    $content = $content -replace '(\s+)_context\.SaveChanges\(\);', '$1await _context.SaveChangesAsync();'
    
    Set-Content $file $content -NoNewline
    Write-Host "  ✓ Corrigé" -ForegroundColor Green
}

Write-Host "`nExécution des tests..." -ForegroundColor Cyan
dotnet test Tests/GestionProjects.Tests.csproj --verbosity minimal

Write-Host "`n✓ Terminé!" -ForegroundColor Green
