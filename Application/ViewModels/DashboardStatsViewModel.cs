namespace GestionProjects.Application.ViewModels
{
    public class DashboardStatsViewModel
    {
        // Rôles de l'utilisateur pour affichage conditionnel dans la vue
        public List<string> UserRoles { get; set; } = new();
        
        public int TotalProjets { get; set; }
        public int ProjetsEnCours { get; set; }
        public int ProjetsEnAnalyse { get; set; }
        public int ProjetsEnPlanification { get; set; }
        public int ProjetsEnExecution { get; set; }
        public int ProjetsEnUAT { get; set; }
        public int ProjetsClotures { get; set; }
        public int ProjetsSuspendus { get; set; }
        public int TotalDemandes { get; set; }
        public int DemandesEnAttente { get; set; }
        public int DemandesValidees { get; set; }
        public int DemandesRejetees { get; set; }
        public int TotalUtilisateurs { get; set; }
        public int TotalRisques { get; set; }
        public int RisquesCritiques { get; set; }
        public int TotalAnomalies { get; set; }
        public int AnomaliesOuvertes { get; set; }
        public int TotalLivrables { get; set; }
        public double TauxAvancementMoyen { get; set; }
        
        // Stats spécifiques Demandeur
        public int MesDemandes { get; set; }
        public int MesDemandesEnAttente { get; set; }
        public int MesDemandesValidees { get; set; }
        public int MesDemandesRejetees { get; set; }
        public int MesProjetsCreesParDemande { get; set; }
        
        // Stats spécifiques ChefDeProjet
        public int MesProjetsGeres { get; set; }
        public int MesProjetsGeresEnCours { get; set; }
        public int MesProjetsGeresEnAnalyse { get; set; }
        public int MesProjetsGeresEnPlanification { get; set; }
        public int MesProjetsGeresEnExecution { get; set; }
        public int MesProjetsGeresEnUAT { get; set; }
        
        // Stats spécifiques DirecteurMetier
        public int DemandesAValiderDM { get; set; }
        public int DemandesValideesDM { get; set; }
        public int DemandesRejeteesParDM { get; set; }
        public int ProjetsMaDirection { get; set; }
        
        // Graphiques DSI/AdminIT
        public Dictionary<string, int> ProjetsParStatut { get; set; } = new();
        public Dictionary<string, int> DemandesParStatut { get; set; } = new();
        public Dictionary<string, int> ProjetsParPhase { get; set; } = new();
        public Dictionary<string, int> ProjetsParEtat { get; set; } = new();
        public Dictionary<string, int> EvolutionProjets { get; set; } = new();
        public Dictionary<string, int> EvolutionDemandes { get; set; } = new();
        public Dictionary<string, int> ProjetsParDirection { get; set; } = new();
        public Dictionary<string, int> ProjetsParUrgence { get; set; } = new();
        public Dictionary<string, int> ProjetsParCriticite { get; set; } = new();
        public Dictionary<string, double> ChargeParEquipe { get; set; } = new();

        // Cockpit admin / DSI
        public int ProjetsVerts { get; set; }
        public int ProjetsOranges { get; set; }
        public int ProjetsRouges { get; set; }
        public int ValidationsDMEnAttente { get; set; }
        public int ValidationsDSIEnAttente { get; set; }
        public int CloturesEnAttente { get; set; }
        public int ChartesNonValidees { get; set; }
        public int RessourcesSurchargees { get; set; }
        public int RessourcesDisponibles { get; set; }
        public int ProjetsAvecDepassementCharge { get; set; }
        public int ProjetsDepassementBudget { get; set; }
        public decimal ChargeTotaleSemaine { get; set; }
        public decimal BudgetTotalPortefeuille { get; set; }
        public decimal BudgetConsommeTotal { get; set; }
        public double TauxUtilisationCapacite { get; set; }

        public List<string> ChargeTendanceLabels { get; set; } = new();
        public List<double> ChargeTendancePrevisionnelle { get; set; } = new();
        public List<double> ChargeTendanceReelle { get; set; } = new();

        public List<DashboardCriticalProjectItem> AdminCriticalProjects { get; set; } = new();
        public List<DashboardValidationItem> AdminPendingValidations { get; set; } = new();
        public List<DashboardMilestoneItem> AdminMilestones { get; set; } = new();
        public List<DashboardAlertItem> AdminAlerts { get; set; } = new();
        public List<DashboardPortfolioItem> AdminPortfolioRows { get; set; } = new();
        
        // Graphiques ResponsableSolutionsIT
        public Dictionary<string, int> RSIProjetsParStatut { get; set; } = new();
        public Dictionary<string, int> RSIProjetsParPhase { get; set; } = new();
        public Dictionary<string, int> RSIProjetsParDirection { get; set; } = new();
        
        // Graphiques DirecteurMetier
        public Dictionary<string, int> DMDemandesParStatut { get; set; } = new();
        public Dictionary<string, int> DMProjetsParStatut { get; set; } = new();
        
        // Graphiques ChefDeProjet
        public Dictionary<string, int> CPProjetsParPhase { get; set; } = new();
        public Dictionary<string, int> CPProjetsParStatut { get; set; } = new();
        
        // Graphiques Demandeur
        public Dictionary<string, int> DemandeurDemandesParStatut { get; set; } = new();
    }

    public class DashboardCriticalProjectItem
    {
        public Guid ProjetId { get; set; }
        public string Projet { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Etat { get; set; } = string.Empty;
        public string Retard { get; set; } = "-";
        public string Risque { get; set; } = "-";
        public string RisqueBadgeClass { get; set; } = "badge-modern-secondary";
        public string ChefProjet { get; set; } = "Non affecté";
        public string Url { get; set; } = "#";
    }

    public class DashboardValidationItem
    {
        public string Type { get; set; } = string.Empty;
        public string Element { get; set; } = string.Empty;
        public string Projet { get; set; } = string.Empty;
        public string Demandeur { get; set; } = string.Empty;
        public string DateLabel { get; set; } = string.Empty;
        public string Url { get; set; } = "#";
    }

    public class DashboardMilestoneItem
    {
        public string DateLabel { get; set; } = string.Empty;
        public string Projet { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public string Url { get; set; } = "#";
    }

    public class DashboardAlertItem
    {
        public string Niveau { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi bi-info-circle";
        public string Message { get; set; } = string.Empty;
        public string DateLabel { get; set; } = string.Empty;
        public string Url { get; set; } = "#";
    }

    public class DashboardPortfolioItem
    {
        public Guid ProjetId { get; set; }
        public string Projet { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Etat { get; set; } = string.Empty;
        public int Avancement { get; set; }
        public string Budget { get; set; } = "-";
        public string Charge { get; set; } = "-";
        public string ChefProjet { get; set; } = "Non affecté";
        public string Url { get; set; } = "#";
    }
}


