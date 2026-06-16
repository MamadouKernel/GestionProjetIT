using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Security
{
    public sealed class PermissionDefinition
    {
        public string Controleur { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;
        public string NomAffichage { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Categorie { get; init; } = string.Empty;
        public string? Icone { get; init; }
        public int Ordre { get; init; }
        public bool AfficherDansMenu { get; init; }
    }

    public static class PermissionCatalog
    {
        private static readonly IReadOnlyList<PermissionDefinition> Definitions = new List<PermissionDefinition>
        {
            // General
            new() { Controleur = "Home", Action = "Index", NomAffichage = "Tableau de bord", Categorie = "General", Icone = "bi-speedometer2", Ordre = 1, AfficherDansMenu = true },
            new() { Controleur = "Dashboard", Action = "Index", NomAffichage = "Analytics", Categorie = "General", Icone = "bi-bar-chart-fill", Ordre = 2, AfficherDansMenu = true },
            new() { Controleur = "Aide", Action = "Index", NomAffichage = "Aide", Categorie = "General", Icone = "bi-question-circle", Ordre = 3, AfficherDansMenu = true },
            new() { Controleur = "Notification", Action = "Index", NomAffichage = "Notifications", Categorie = "General", Icone = "bi-bell", Ordre = 4, AfficherDansMenu = false },
            new() { Controleur = "Account", Action = "Profil", NomAffichage = "Mon Profil", Categorie = "General", Icone = "bi-person-circle", Ordre = 5, AfficherDansMenu = false },
            new() { Controleur = "Account", Action = "Preferences", NomAffichage = "Preferences", Categorie = "General", Icone = "bi-sliders", Ordre = 6, AfficherDansMenu = false },

            // Requests
            new() { Controleur = "DemandeProjet", Action = "Index", NomAffichage = "Mes Demandes", Categorie = "Demandes", Icone = "bi-list-ul", Ordre = 1, AfficherDansMenu = true },
            new() { Controleur = "DemandeProjet", Action = "Create", NomAffichage = "Nouvelle Demande", Categorie = "Demandes", Icone = "bi-plus-circle", Ordre = 2, AfficherDansMenu = true },
            new() { Controleur = "DemandeProjet", Action = "Details", NomAffichage = "Details Demande", Categorie = "Demandes", Icone = "bi-eye", Ordre = 3, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "Edit", NomAffichage = "Modifier Demande", Categorie = "Demandes", Icone = "bi-pencil", Ordre = 4, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "Dashboard", NomAffichage = "Dashboard Demandes", Categorie = "Demandes", Icone = "bi-speedometer2", Ordre = 5, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "Soumettre", NomAffichage = "Soumettre Demande", Categorie = "Demandes", Icone = "bi-send", Ordre = 6, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "ConfirmerSoumission", NomAffichage = "Confirmer Soumission Demande", Categorie = "Demandes", Icone = "bi-send-check", Ordre = 7, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "AjouterDocumentsComplementaires", NomAffichage = "Ajouter Documents Complementaires", Categorie = "Demandes", Icone = "bi-paperclip", Ordre = 8, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "DupliquerDemande", NomAffichage = "Dupliquer Demande", Categorie = "Demandes", Icone = "bi-files", Ordre = 9, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "ValiderDM", NomAffichage = "Validation Demande DM", Categorie = "Validations", Icone = "bi-check-circle", Ordre = 15, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "RejeterDM", NomAffichage = "Rejet Demande DM", Categorie = "Validations", Icone = "bi-x-circle", Ordre = 16, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "DemanderCorrectionDM", NomAffichage = "Correction Demande DM", Categorie = "Validations", Icone = "bi-pencil-square", Ordre = 17, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "ValiderDSI", NomAffichage = "Validation Demande DSI", Categorie = "Validations", Icone = "bi-shield-check", Ordre = 18, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "RejeterDSI", NomAffichage = "Rejet Demande DSI", Categorie = "Validations", Icone = "bi-x-octagon", Ordre = 19, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "RenvoyerAuDemandeurDSI", NomAffichage = "Renvoi Demandeur DSI", Categorie = "Validations", Icone = "bi-arrow-counterclockwise", Ordre = 20, AfficherDansMenu = false },
            new() { Controleur = "DemandeProjet", Action = "RenvoyerAuDMDSI", NomAffichage = "Renvoi DM DSI", Categorie = "Validations", Icone = "bi-arrow-counterclockwise", Ordre = 21, AfficherDansMenu = false },

            // Validations
            new() { Controleur = "DemandeProjet", Action = "ListeValidationDM", NomAffichage = "Validations DM", Categorie = "Validations", Icone = "bi-check-circle", Ordre = 1, AfficherDansMenu = true },
            new() { Controleur = "DemandeProjet", Action = "ListeValidationDSI", NomAffichage = "Validations DSI", Categorie = "Validations", Icone = "bi-shield-check", Ordre = 2, AfficherDansMenu = true },
            new() { Controleur = "DemandeProjet", Action = "HistoriqueValidationsDSI", NomAffichage = "Historique Validations DSI", Categorie = "Validations", Icone = "bi-clock-history", Ordre = 3, AfficherDansMenu = true },
            new() { Controleur = "DemandeProjet", Action = "HistoriqueActionsDM", NomAffichage = "Historique Actions DM", Categorie = "Validations", Icone = "bi-journal-text", Ordre = 4, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "ValidationsProjet", NomAffichage = "Validations Projet", Categorie = "Validations", Icone = "bi-file-check", Ordre = 5, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "ListeValidationClotureDSI", NomAffichage = "Validations Cloture DSI", Categorie = "Validations", Icone = "bi-flag-checkered", Ordre = 6, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "ListeValidationClotureDM", NomAffichage = "Validations Cloture DM", Categorie = "Validations", Icone = "bi-flag-checkered", Ordre = 7, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "ListeValidationClotureDemandeur", NomAffichage = "Mes validations cloture", Categorie = "Validations", Icone = "bi-flag-checkered", Ordre = 8, AfficherDansMenu = true },

            // Projects
            new() { Controleur = "Projet", Action = "Portefeuille", NomAffichage = "Portefeuille DSI", Categorie = "Projets", Icone = "bi-briefcase", Ordre = 1, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "Index", NomAffichage = "Liste des Projets", Categorie = "Projets", Icone = "bi-grid", Ordre = 2, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "Details", NomAffichage = "Details Projet", Categorie = "Projets", Icone = "bi-eye", Ordre = 3, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "HistoriqueDM", NomAffichage = "Historique & Tracabilite", Categorie = "Projets", Icone = "bi-clock-history", Ordre = 4, AfficherDansMenu = true },
            new() { Controleur = "Projet", Action = "CharteProjet", NomAffichage = "Charte Projet", Categorie = "Projets", Icone = "bi-file-text", Ordre = 5, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "FicheProjet", NomAffichage = "Fiche Projet", Categorie = "Projets", Icone = "bi-file-earmark", Ordre = 6, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "Planning", NomAffichage = "Planning", Categorie = "Projets", Icone = "bi-calendar3", Ordre = 7, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "Charges", NomAffichage = "Charges", Categorie = "Projets", Icone = "bi-graph-up", Ordre = 8, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "Recette", NomAffichage = "Recette", Categorie = "Projets", Icone = "bi-clipboard-check", Ordre = 9, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "Cloture", NomAffichage = "Cloture", Categorie = "Projets", Icone = "bi-flag", Ordre = 10, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditAnalyse", NomAffichage = "Edition Analyse", Categorie = "Projets", Icone = "bi-pencil", Ordre = 11, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditCharte", NomAffichage = "Edition Charte", Categorie = "Projets", Icone = "bi-pencil-square", Ordre = 12, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditPlanification", NomAffichage = "Edition Planification", Categorie = "Projets", Icone = "bi-pencil-square", Ordre = 13, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditExecution", NomAffichage = "Edition Execution", Categorie = "Projets", Icone = "bi-pencil-square", Ordre = 14, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditUat", NomAffichage = "Edition UAT", Categorie = "Projets", Icone = "bi-pencil-square", Ordre = 15, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditCloture", NomAffichage = "Edition Cloture", Categorie = "Projets", Icone = "bi-pencil-square", Ordre = 16, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditCollaboration", NomAffichage = "Edition Collaboration", Categorie = "Projets", Icone = "bi-pencil-square", Ordre = 17, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "UpdateProgress", NomAffichage = "Mise a jour avancement", Categorie = "Projets", Icone = "bi-speedometer2", Ordre = 18, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "AddRisk", NomAffichage = "Ajout Risque", Categorie = "Projets", Icone = "bi-exclamation-triangle", Ordre = 19, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ChangerPhase", NomAffichage = "Changer Phase", Categorie = "Projets", Icone = "bi-arrow-repeat", Ordre = 20, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ForcerStatut", NomAffichage = "Forcer Statut", Categorie = "Projets", Icone = "bi-exclamation-triangle", Ordre = 21, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ManageDossierSignature", NomAffichage = "Gestion Dossier Signature", Categorie = "Projets", Icone = "bi-pen", Ordre = 22, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "EditCommentaireTechnique", NomAffichage = "Edition Commentaire Technique", Categorie = "Projets", Icone = "bi-chat-dots", Ordre = 23, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ValiderAnalyse", NomAffichage = "Validation Phase Analyse", Categorie = "Validations", Icone = "bi-check-circle", Ordre = 9, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ValiderCharteDM", NomAffichage = "Validation Charte DM", Categorie = "Validations", Icone = "bi-check-circle", Ordre = 10, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ValiderCharteDSI", NomAffichage = "Validation Charte DSI", Categorie = "Validations", Icone = "bi-shield-check", Ordre = 11, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ValiderPlanificationDM", NomAffichage = "Validation Planification DM", Categorie = "Validations", Icone = "bi-check-circle", Ordre = 12, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ValiderPlanificationDSI", NomAffichage = "Validation Planification DSI", Categorie = "Validations", Icone = "bi-shield-check", Ordre = 13, AfficherDansMenu = false },
            new() { Controleur = "Projet", Action = "ValiderRecette", NomAffichage = "Validation Recette Metier", Categorie = "Validations", Icone = "bi-check-circle", Ordre = 14, AfficherDansMenu = false },

            // Administration
            new() { Controleur = "Admin", Action = "Users", NomAffichage = "Utilisateurs", Categorie = "Administration", Icone = "bi-people", Ordre = 1, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "ListeRoles", NomAffichage = "Assignation des Roles", Categorie = "Administration", Icone = "bi-person-lines-fill", Ordre = 2, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "GererRoles", NomAffichage = "Gerer Roles", Categorie = "Administration", Icone = "bi-shield-check", Ordre = 3, AfficherDansMenu = false },
            new() { Controleur = "Admin", Action = "Directions", NomAffichage = "Directions", Categorie = "Administration", Icone = "bi-building", Ordre = 4, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "Services", NomAffichage = "Services", Categorie = "Administration", Icone = "bi-boxes", Ordre = 5, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "Parametres", NomAffichage = "Parametres", Categorie = "Administration", Icone = "bi-gear", Ordre = 6, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "Delegations", NomAffichage = "Delegations", Categorie = "Administration", Icone = "bi-person-badge", Ordre = 7, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "DemandesCreationCompte", NomAffichage = "Demandes de compte", Categorie = "Administration", Icone = "bi-person-badge", Ordre = 8, AfficherDansMenu = true },
            new() { Controleur = "Admin", Action = "ValiderDemandeCreationCompteDM", NomAffichage = "Validation Compte DM", Categorie = "Administration", Icone = "bi-check-circle", Ordre = 11, AfficherDansMenu = false },
            new() { Controleur = "Admin", Action = "RefuserDemandeCreationCompteDM", NomAffichage = "Refus Compte DM", Categorie = "Administration", Icone = "bi-x-circle", Ordre = 12, AfficherDansMenu = false },
            new() { Controleur = "Admin", Action = "ValiderDemandeCreationCompteDSI", NomAffichage = "Validation Compte DSI", Categorie = "Administration", Icone = "bi-person-check", Ordre = 13, AfficherDansMenu = false },
            new() { Controleur = "Admin", Action = "RefuserDemandeCreationCompteDSI", NomAffichage = "Refus Compte DSI", Categorie = "Administration", Icone = "bi-person-x", Ordre = 14, AfficherDansMenu = false },
            new() { Controleur = "DemandesAcces", Action = "Index", NomAffichage = "Demandes d'acces", Categorie = "Administration", Icone = "bi-person-plus", Ordre = 9, AfficherDansMenu = true },
            new() { Controleur = "DemandesAcces", Action = "ValidationsDm", NomAffichage = "Validations d'acces (DM)", Categorie = "Validations", Icone = "bi-shield-check", Ordre = 6, AfficherDansMenu = true },
            new() { Controleur = "Autorisations", Action = "Index", NomAffichage = "Autorisations / Droits", Categorie = "Administration", Icone = "bi-shield-lock", Ordre = 10, AfficherDansMenu = true }
        };

        public static IReadOnlyList<PermissionDefinition> GetDefinitions() => Definitions;

        public static PermissionDefinition? GetDefinition(string controleur, string action) =>
            Definitions.FirstOrDefault(d =>
                string.Equals(d.Controleur, controleur, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.Action, action, StringComparison.OrdinalIgnoreCase));

        public static bool IsManagedAction(string? controleur, string? action) =>
            !string.IsNullOrWhiteSpace(controleur) &&
            !string.IsNullOrWhiteSpace(action) &&
            GetDefinition(controleur, action) != null;

        public static int GetCategoryOrder(string category) => category switch
        {
            "Demandes" => 1,
            "Validations" => 2,
            "Projets" => 3,
            "Administration" => 4,
            "General" => 5,
            _ => 999
        };

        public static string GetMenuSectionTitle(string category) => category switch
        {
            "Demandes" => "Mes Demandes",
            "Validations" => "Validations",
            "Projets" => "Projets",
            "Administration" => "Administration",
            "General" => "Général",
            _ => category
        };

        public static bool IsEnabledByDefault(RoleUtilisateur role, string controleur, string action)
        {
            if (role == RoleUtilisateur.AdminIT)
            {
                return true;
            }

            if (controleur == "Home" && action == "Index")
            {
                return true;
            }

            if (controleur == "Aide" && action == "Index")
            {
                return true;
            }

            if (controleur == "Notification" && action == "Index")
            {
                return true;
            }

            if (controleur == "Account" && (action == "Profil" || action == "Preferences"))
            {
                return true;
            }

            return role switch
            {
                RoleUtilisateur.Demandeur => (controleur, action) switch
                {
                    ("DemandeProjet", "Index") => true,
                    ("DemandeProjet", "Create") => true,
                    ("DemandeProjet", "Details") => true,
                    ("DemandeProjet", "Edit") => true,
                    ("DemandeProjet", "Soumettre") => true,
                    ("DemandeProjet", "ConfirmerSoumission") => true,
                    ("DemandeProjet", "AjouterDocumentsComplementaires") => true,
                    ("DemandeProjet", "DupliquerDemande") => true,
                    ("Projet", "Index") => true,
                    ("Projet", "Details") => true,
                    ("Projet", "ListeValidationClotureDemandeur") => true,
                    _ => false
                },
                RoleUtilisateur.DirecteurMetier => (controleur, action) switch
                {
                    ("Dashboard", "Index") => true,
                    ("DemandeProjet", "Index") => true,
                    ("DemandeProjet", "Details") => true,
                    ("DemandeProjet", "Edit") => true,
                    ("DemandeProjet", "ListeValidationDM") => true,
                    ("DemandeProjet", "HistoriqueActionsDM") => true,
                    ("DemandeProjet", "ValiderDM") => true,
                    ("DemandeProjet", "RejeterDM") => true,
                    ("DemandeProjet", "DemanderCorrectionDM") => true,
                    ("Projet", "Index") => true,
                    ("Projet", "Details") => true,
                    ("Projet", "HistoriqueDM") => true,
                    ("Projet", "ValidationsProjet") => true,
                    ("Projet", "ListeValidationClotureDM") => true,
                    ("Projet", "CharteProjet") => true,
                    ("Projet", "FicheProjet") => true,
                    ("Projet", "Planning") => true,
                    ("Projet", "Charges") => true,
                    ("Projet", "Recette") => true,
                    ("Projet", "Cloture") => true,
                    ("Projet", "ValiderCharteDM") => true,
                    ("Projet", "ValiderPlanificationDM") => true,
                    ("Projet", "ValiderRecette") => true,
                    ("Admin", "DemandesCreationCompte") => true,
                    ("Admin", "ValiderDemandeCreationCompteDM") => true,
                    ("Admin", "RefuserDemandeCreationCompteDM") => true,
                    ("DemandesAcces", "ValidationsDm") => true,
                    _ => false
                },
                RoleUtilisateur.DSI => (controleur, action) switch
                {
                    ("Dashboard", "Index") => true,
                    ("DemandeProjet", "Index") => true,
                    ("DemandeProjet", "Create") => true,
                    ("DemandeProjet", "Details") => true,
                    ("DemandeProjet", "Edit") => true,
                    ("DemandeProjet", "Soumettre") => true,
                    ("DemandeProjet", "ConfirmerSoumission") => true,
                    ("DemandeProjet", "AjouterDocumentsComplementaires") => true,
                    ("DemandeProjet", "DupliquerDemande") => true,
                    ("DemandeProjet", "ListeValidationDSI") => true,
                    ("DemandeProjet", "HistoriqueValidationsDSI") => true,
                    ("DemandeProjet", "ValiderDSI") => true,
                    ("DemandeProjet", "RejeterDSI") => true,
                    ("DemandeProjet", "RenvoyerAuDemandeurDSI") => true,
                    ("DemandeProjet", "RenvoyerAuDMDSI") => true,
                    ("Projet", "Portefeuille") => true,
                    ("Projet", "Index") => true,
                    ("Projet", "Details") => true,
                    ("Projet", "HistoriqueDM") => true,
                    ("Projet", "ValidationsProjet") => true,
                    ("Projet", "ListeValidationClotureDSI") => true,
                    ("Projet", "CharteProjet") => true,
                    ("Projet", "FicheProjet") => true,
                    ("Projet", "Planning") => true,
                    ("Projet", "Charges") => true,
                    ("Projet", "Recette") => true,
                    ("Projet", "Cloture") => true,
                    ("Projet", "EditCharte") => true,
                    ("Projet", "EditPlanification") => true,
                    ("Projet", "EditExecution") => true,
                    ("Projet", "EditUat") => true,
                    ("Projet", "EditCloture") => true,
                    ("Projet", "EditCollaboration") => true,
                    ("Projet", "ChangerPhase") => true,
                    ("Projet", "ForcerStatut") => true,
                    ("Projet", "ManageDossierSignature") => true,
                    ("Projet", "ValiderCharteDSI") => true,
                    ("Projet", "ValiderPlanificationDSI") => true,
                    ("Admin", "DemandesCreationCompte") => true,
                    ("Admin", "ValiderDemandeCreationCompteDSI") => true,
                    ("Admin", "RefuserDemandeCreationCompteDSI") => true,
                    _ => false
                },
                RoleUtilisateur.ResponsableSolutionsIT => (controleur, action) switch
                {
                    ("Dashboard", "Index") => true,
                    ("DemandeProjet", "Index") => true,
                    ("DemandeProjet", "Details") => true,
                    ("DemandeProjet", "ListeValidationDSI") => true,
                    ("DemandeProjet", "HistoriqueValidationsDSI") => true,
                    ("DemandeProjet", "ValiderDSI") => true,
                    ("DemandeProjet", "RejeterDSI") => true,
                    ("DemandeProjet", "RenvoyerAuDemandeurDSI") => true,
                    ("DemandeProjet", "RenvoyerAuDMDSI") => true,
                    ("Projet", "Portefeuille") => true,
                    ("Projet", "Index") => true,
                    ("Projet", "Details") => true,
                    ("Projet", "HistoriqueDM") => true,
                    ("Projet", "ValidationsProjet") => true,
                    ("Projet", "CharteProjet") => true,
                    ("Projet", "FicheProjet") => true,
                    ("Projet", "Planning") => true,
                    ("Projet", "Charges") => true,
                    ("Projet", "Recette") => true,
                    ("Projet", "Cloture") => true,
                    ("Projet", "EditAnalyse") => true,
                    ("Projet", "EditCharte") => true,
                    ("Projet", "EditUat") => true,
                    ("Projet", "EditCloture") => true,
                    ("Projet", "EditCollaboration") => true,
                    ("Projet", "ManageDossierSignature") => true,
                    ("Projet", "EditCommentaireTechnique") => true,
                    ("Admin", "DemandesCreationCompte") => true,
                    ("Admin", "ValiderDemandeCreationCompteDSI") => true,
                    ("Admin", "RefuserDemandeCreationCompteDSI") => true,
                    _ => false
                },
                RoleUtilisateur.ChefDeProjet => (controleur, action) switch
                {
                    ("Projet", "Index") => true,
                    ("Projet", "Details") => true,
                    ("Projet", "HistoriqueDM") => true,
                    ("Projet", "CharteProjet") => true,
                    ("Projet", "FicheProjet") => true,
                    ("Projet", "Planning") => true,
                    ("Projet", "Charges") => true,
                    ("Projet", "Recette") => true,
                    ("Projet", "Cloture") => true,
                    ("Projet", "EditAnalyse") => true,
                    ("Projet", "EditCharte") => true,
                    ("Projet", "EditPlanification") => true,
                    ("Projet", "EditExecution") => true,
                    ("Projet", "EditUat") => true,
                    ("Projet", "EditCloture") => true,
                    ("Projet", "EditCollaboration") => true,
                    ("Projet", "UpdateProgress") => true,
                    ("Projet", "AddRisk") => true,
                    ("Projet", "ChangerPhase") => true,
                    ("Projet", "ManageDossierSignature") => true,
                    ("Projet", "ValiderAnalyse") => true,
                    _ => false
                },
                _ => false
            };
        }
    }
}
