# Script pour corriger tous les tests et atteindre 100% de réussite

Write-Host "Correction des tests pour atteindre 100% de réussite..." -ForegroundColor Cyan

# Fonction pour ajouter les propriétés manquantes aux LivrableProjet
function Fix-LivrableProjet {
    param($file)
    
    $content = Get-Content $file -Raw
    
    # Pattern pour trouver les créations de LivrableProjet sans Commentaire et Version
    $pattern = '(new LivrableProjet\s*\{[^}]*?DeposeParId = [^}]*?)(}\s*;)'
    
    # Remplacer en ajoutant les propriétés manquantes
    $replacement = '$1,
                Commentaire = string.Empty,
                Version = "1.0"
            $2'
    
    $newContent = $content -replace $pattern, $replacement
    
    if ($newContent -ne $content) {
        Set-Content $file $newContent -NoNewline
        Write-Host "  ✓ Corrigé: $file" -ForegroundColor Green
        return $true
    }
    return $false
}

# Fonction pour corriger les HistoriquePhaseProjet
function Fix-HistoriquePhaseProjet {
    param($file)
    
    $content = Get-Content $file -Raw
    
    # Pattern pour trouver les créations de HistoriquePhaseProjet sans Commentaire
    $pattern = '(new HistoriquePhaseProjet\s*\{[^}]*?ModifieParId = [^}]*?)(}\s*;)'
    
    # Remplacer en ajoutant la propriété manquante
    $replacement = '$1,
                Commentaire = string.Empty
            $2'
    
    $newContent = $content -replace $pattern, $replacement
    
    if ($newContent -ne $content) {
        Set-Content $file $newContent -NoNewline
        Write-Host "  ✓ Corrigé: $file" -ForegroundColor Green
        return $true
    }
    return $false
}

# Fonction pour corriger SaveChanges en SaveChangesAsync
function Fix-SaveChanges {
    param($file)
    
    $content = Get-Content $file -Raw
    
    # Remplacer _context.SaveChanges() par await _context.SaveChangesAsync()
    $pattern = '(\s+)_context\.SaveChanges\(\);'
    $replacement = '$1await _context.SaveChangesAsync();'
    
    $newContent = $content -replace $pattern, $replacement
    
    if ($newContent -ne $content) {
        Set-Content $file $newContent -NoNewline
        Write-Host "  ✓ Corrigé SaveChanges: $file" -ForegroundColor Green
        return $true
    }
    return $false
}

# Liste des fichiers à corriger
$testFiles = @(
    "Tests/Unit/Projet/PlanificationTests.cs",
    "Tests/Unit/Projet/ExecutionTests.cs",
    "Tests/Unit/Projet/UATTests.cs",
    "Tests/Unit/Projet/ClotureTests.cs",
    "Tests/Unit/Projet/CharteProjetTests.cs"
)

$totalFixed = 0

foreach ($file in $testFiles) {
    if (Test-Path $file) {
        Write-Host "`nTraitement de $file..." -ForegroundColor Yellow
        
        $fixed = $false
        $fixed = Fix-LivrableProjet $file -or $fixed
        $fixed = Fix-HistoriquePhaseProjet $file -or $fixed
        $fixed = Fix-SaveChanges $file -or $fixed
        
        if ($fixed) {
            $totalFixed++
        }
    } else {
        Write-Host "  ✗ Fichier non trouvé: $file" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Résumé:" -ForegroundColor Cyan
Write-Host "  Fichiers corrigés: $totalFixed/$($testFiles.Count)" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

# Exécuter les tests
Write-Host "Exécution des tests..." -ForegroundColor Cyan
dotnet test Tests/GestionProjects.Tests.csproj --verbosity minimal

Write-Host "`n✓ Corrections terminées!" -ForegroundColor Green
