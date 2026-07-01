namespace GestionProjects.Application.ViewModels
{
    public sealed class ProjetUiPermissions
    {
        public Guid UserId { get; init; }
        public Guid? CurrentUserDirectionId { get; init; }
        public bool IsReadOnly { get; init; }
        public bool IsDemandeurProject { get; init; }
        public bool IsAssignedChefProjet { get; init; }
        public bool IsProjectSponsor { get; init; }
        public bool IsProjectInUserDirection { get; init; }
        /// <summary>
        /// L'utilisateur agit comme délégataire du Directeur Métier (sponsor) de ce projet,
        /// via une délégation temporaire de rôle active (voir DelegationValidationDM).
        /// </summary>
        public bool IsDelegatedSponsor { get; init; }
        /// <summary>
        /// L'AdminIT ne doit subir aucune restriction dans l'application : les vérifications
        /// de permission nommée sont déjà contournées pour ce rôle (PermissionService),
        /// mais certaines actions exigent en plus d'être littéralement le sponsor / chef de
        /// projet / demandeur assigné (identité, pas permission) — cet indicateur lève aussi
        /// ces dernières restrictions pour l'AdminIT.
        /// </summary>
        public bool IsAdminIT { get; init; }
        public HashSet<string> ActivePermissionKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Has(string controleur, string action)
            => ActivePermissionKeys.Contains($"{controleur}::{action}");

        public bool HasPortfolioAccess => Has("Projet", "Portefeuille");
        public bool HasProjectValidationAccess => Has("Projet", "ValidationsProjet");
        public bool HasClotureDsiAccess => Has("Projet", "ListeValidationClotureDSI");
        public bool HasClotureDmAccess => Has("Projet", "ListeValidationClotureDM");
        public bool HasClotureDemandeurAccess => Has("Projet", "ListeValidationClotureDemandeur");
        public bool HasDsiGovernanceAccess =>
            HasPortfolioAccess ||
            Has("Projet", "ValiderCharteDSI") ||
            Has("Projet", "ValiderPlanificationDSI") ||
            HasClotureDsiAccess;
        public bool HasDmGovernanceAccess =>
            HasProjectValidationAccess ||
            Has("Projet", "ValiderCharteDM") ||
            Has("Projet", "ValiderPlanificationDM") ||
            Has("Projet", "ValiderRecette") ||
            HasClotureDmAccess;
        public bool HasChefProjetGovernanceAccess =>
            Has("Projet", "UpdateProgress") ||
            Has("Projet", "AddRisk") ||
            Has("Projet", "ValiderAnalyse");
        public bool CanViewProject =>
            IsAdminIT ||
            HasDsiGovernanceAccess ||
            IsAssignedChefProjet ||
            IsDemandeurProject ||
            (HasDmGovernanceAccess && (IsProjectSponsor || IsProjectInUserDirection));

        public bool CanOpenChargesTab => Has("Projet", "Charges");
        public bool CanOpenHistoryTab => Has("Projet", "HistoriqueDM");

        public bool CanEditCharte => Has("Projet", "EditCharte") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanEditAnalyse => Has("Projet", "EditAnalyse") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanStartProject => CanEditAnalyse || HasDsiGovernanceAccess;
        public bool CanValidateAnalysePhase => IsAdminIT || (Has("Projet", "ValiderAnalyse") && IsAssignedChefProjet);
        public bool CanValidateCharteDm => IsAdminIT || (Has("Projet", "ValiderCharteDM") && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanValidateCharteDsi => Has("Projet", "ValiderCharteDSI");
        public bool CanManageDossierSignature => Has("Projet", "ManageDossierSignature") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanEditTechnicalComment => Has("Projet", "EditCommentaireTechnique") && HasDsiGovernanceAccess;

        public bool CanEditPlanification => Has("Projet", "EditPlanification") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanValidatePlanificationDm => IsAdminIT || (Has("Projet", "ValiderPlanificationDM") && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanValidatePlanificationDsi => Has("Projet", "ValiderPlanificationDSI");

        public bool CanEditExecution => Has("Projet", "EditExecution") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanEditUat => Has("Projet", "EditUat") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanValidateRecette => IsAdminIT || (Has("Projet", "ValiderRecette") && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanEditCloture => Has("Projet", "EditCloture") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanEditCollaboration => Has("Projet", "EditCollaboration") && (IsAssignedChefProjet || HasDsiGovernanceAccess);

        public bool CanUpdateProgress => IsAdminIT || (Has("Projet", "UpdateProgress") && IsAssignedChefProjet);
        public bool CanAddRisk => IsAdminIT || (Has("Projet", "AddRisk") && IsAssignedChefProjet);
        public bool CanChangePhase => Has("Projet", "ChangerPhase") && (IsAssignedChefProjet || HasDsiGovernanceAccess);
        public bool CanForceStatus => Has("Projet", "ForcerStatut");
        public bool CanReassignChefProjet => HasPortfolioAccess || HasDsiGovernanceAccess;

        public bool CanValidateClotureDm => IsAdminIT || (HasClotureDmAccess && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanValidateClotureDsi => HasClotureDsiAccess;
        public bool CanValidateClotureDemandeur => IsAdminIT || (HasClotureDemandeurAccess && IsDemandeurProject);

        public bool CanUseProjectModals =>
            CanEditAnalyse ||
            CanStartProject ||
            CanEditCharte ||
            CanEditPlanification ||
            CanEditExecution ||
            CanEditUat ||
            CanEditCloture ||
            CanEditCollaboration ||
            CanUpdateProgress ||
            CanAddRisk;
    }
}
