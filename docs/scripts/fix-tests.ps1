# Fix all test issues

# Fix TypeLivrable enum values
$files = @(
    "Tests/Unit/Projet/UATTests.cs",
    "Tests/Unit/Projet/PlanificationTests.cs",
    "Tests/Unit/Projet/ExecutionTests.cs",
    "Tests/Unit/Projet/ClotureTests.cs"
)

foreach ($file in $files) {
    $content = Get-Content $file -Raw
    
    # Fix TypeLivrable enum values
    $content = $content -replace 'TypeLivrable\.PlanRecette', 'TypeLivrable.CahierTests'
    $content = $content -replace 'TypeLivrable\.CahierRecette', 'TypeLivrable.CahierTests'
    $content = $content -replace 'TypeLivrable\.PVRecette', 'TypeLivrable.PvRecette'
    $content = $content -replace 'TypeLivrable\.PlanMEP', 'TypeLivrable.DossierMep'
    $content = $content -replace 'TypeLivrable\.PVMEP', 'TypeLivrable.PvMep'
    $content = $content -replace 'TypeLivrable\.DocumentationUtilisateur', 'TypeLivrable.Autre'
    $content = $content -replace 'TypeLivrable\.DocumentationTechnique', 'TypeLivrable.Autre'
    $content = $content -replace 'TypeLivrable\.PlanRetourArriere', 'TypeLivrable.Autre'
    $content = $content -replace 'TypeLivrable\.WBS', 'TypeLivrable.Wbs'
    $content = $content -replace 'TypeLivrable\.MatriceRACI', 'TypeLivrable.MatriceRaci'
    $content = $content -replace 'TypeLivrable\.PlanCommunication', 'TypeLivrable.SchemaCommunication'
    $content = $content -replace 'TypeLivrable\.PlanGestionRisques', 'TypeLivrable.Autre'
    $content = $content -replace 'TypeLivrable\.PlanGestionQualite', 'TypeLivrable.Autre'
    $content = $content -replace 'TypeLivrable\.RapportAvancement', 'TypeLivrable.CompteRenduReunion'
    $content = $content -replace 'TypeLivrable\.DecisionGoNoGo', 'TypeLivrable.Autre'
    $content = $content -replace 'TypeLivrable\.BilanProjet', 'TypeLivrable.RapportCloture'
    $content = $content -replace 'TypeLivrable\.RapportFinal', 'TypeLivrable.RapportCloture'
    
    # Fix StatutAnomalie enum values
    $content = $content -replace 'StatutAnomalie\.Resolue', 'StatutAnomalie.Fermee'
    
    # Fix StatutRisque enum values
    $content = $content -replace 'StatutRisque\.EnCours', 'StatutRisque.EnCoursTraitement'
    
    # Fix PrioriteAnomalie enum values
    $content = $content -replace 'PrioriteAnomalie\.Bloquante', 'PrioriteAnomalie.Critique'
    
    # Fix AnomalieProjet properties
    $content = $content -replace '\.Titre\s*=', '.Reference ='
    $content = $content -replace '\.DateDetection\s*=', '.DateCreationAnomalie ='
    
    # Fix JalonCharte properties
    $content = $content -replace '\.NomJalon\s*=', '.Nom ='
    $content = $content -replace '\.DatePrevue\s*=', '.DatePrevisionnelle ='
    $content = $content -replace 'j\.NomJalon', 'j.Nom'
    
    # Fix PartiePrenanteCharte properties
    $content = $content -replace '\.Responsabilites\s*=', '.Role ='
    
    # Fix HistoriquePhaseProjet properties
    $content = $content -replace '\.PhaseAvant\s*=', '.Phase ='
    $content = $content -replace '\.PhaseApres\s*=', '.Phase ='
    $content = $content -replace '\.DateChangement\s*=', '.DateDebut ='
    $content = $content -replace '\.ChangePar\s*=', '.ModifieParId ='
    
    # Fix DemandeClotureProjet properties
    $content = $content -replace '\.BilanProjet\s*=', '.CommentaireDemandeur ='
    $content = $content -replace '\.LeconsApprises\s*=', '.CommentaireDemandeur ='
    $content = $content -replace '\.StatutValidationDM', '.StatutValidationDirecteurMetier'
    $content = $content -replace '\.DateValidationDM', '.DateValidationDirecteurMetier'
    
    # Fix ChartesProjets to CharteProjets
    $content = $content -replace '\.ChartesProjets', '.CharteProjets'
    
    Set-Content $file $content -NoNewline
}

Write-Host "All fixes applied successfully!"
