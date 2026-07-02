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
        /// L'utilisateur agit comme délégataire du Chef de Projet assigné à ce projet,
        /// via une délégation temporaire créée par le DSI (voir DelegationChefProjet).
        /// </summary>
        public bool IsDelegatedChefProjet { get; init; }
        /// <summary>
        /// L'AdminIT ne doit subir aucune restriction dans l'application : les vérifications
        /// de permission nommée sont déjà contournées pour ce rôle (PermissionService),
        /// mais certaines actions exigent en plus d'être littéralement le sponsor / chef de
        /// projet / demandeur assigné (identité, pas permission) — cet indicateur lève aussi
        /// ces dernières restrictions pour l'AdminIT.
        /// </summary>
        public bool IsAdminIT { get; init; }
        /// <summary>
        /// Le Responsable Solution IT peut agir comme Chef de Projet sur n'importe quel
        /// projet sans délégation formelle, même si un autre utilisateur y est officiellement
        /// affecté (évolution "délégation des rôles" — cas particulier du Responsable Solution IT).
        /// </summary>
        public bool IsResponsableSolutionIT { get; init; }
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
        /// <summary>
        /// Regroupe toutes les façons légitimes d'agir avec les droits du Chef de Projet
        /// affecté : l'être réellement, en être le délégataire actif, ou être Responsable
        /// Solution IT (qui n'a besoin d'aucune délégation formelle).
        /// </summary>
        public bool CanActAsChefProjet => IsAssignedChefProjet || IsDelegatedChefProjet || IsResponsableSolutionIT;

        public bool CanViewProject =>
            IsAdminIT ||
            HasDsiGovernanceAccess ||
            CanActAsChefProjet ||
            IsDemandeurProject ||
            (HasDmGovernanceAccess && (IsProjectSponsor || IsProjectInUserDirection));

        public bool CanOpenChargesTab => Has("Projet", "Charges");
        public bool CanOpenHistoryTab => Has("Projet", "HistoriqueDM");

        public bool CanEditCharte => Has("Projet", "EditCharte") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanEditAnalyse => Has("Projet", "EditAnalyse") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanStartProject => CanEditAnalyse || HasDsiGovernanceAccess;
        public bool CanValidateAnalysePhase => IsAdminIT || IsResponsableSolutionIT || (Has("Projet", "ValiderAnalyse") && (IsAssignedChefProjet || IsDelegatedChefProjet));
        public bool CanValidateCharteDm => IsAdminIT || (Has("Projet", "ValiderCharteDM") && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanValidateCharteDsi => Has("Projet", "ValiderCharteDSI");
        public bool CanManageDossierSignature => Has("Projet", "ManageDossierSignature") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanEditTechnicalComment => Has("Projet", "EditCommentaireTechnique") && HasDsiGovernanceAccess;

        public bool CanEditPlanification => Has("Projet", "EditPlanification") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanValidatePlanificationDm => IsAdminIT || (Has("Projet", "ValiderPlanificationDM") && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanValidatePlanificationDsi => Has("Projet", "ValiderPlanificationDSI");

        public bool CanEditExecution => Has("Projet", "EditExecution") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanEditUat => Has("Projet", "EditUat") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanValidateRecette => IsAdminIT || (Has("Projet", "ValiderRecette") && (IsProjectSponsor || IsDelegatedSponsor) && !IsReadOnly);
        public bool CanEditCloture => Has("Projet", "EditCloture") && (CanActAsChefProjet || HasDsiGovernanceAccess);
        public bool CanEditCollaboration => Has("Projet", "EditCollaboration") && (CanActAsChefProjet || HasDsiGovernanceAccess);

        public bool CanUpdateProgress => IsAdminIT || IsResponsableSolutionIT || (Has("Projet", "UpdateProgress") && (IsAssignedChefProjet || IsDelegatedChefProjet));
        public bool CanAddRisk => IsAdminIT || IsResponsableSolutionIT || (Has("Projet", "AddRisk") && (IsAssignedChefProjet || IsDelegatedChefProjet));
        public bool CanChangePhase => Has("Projet", "ChangerPhase") && (CanActAsChefProjet || HasDsiGovernanceAccess);
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
