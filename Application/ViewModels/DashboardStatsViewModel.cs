namespace GestionProjects.Application.ViewModels
{
    public class DashboardStatsViewModel
    {
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
        
        // Données pour graphiques
        public Dictionary<string, int> ProjetsParStatut { get; set; } = new();
        public Dictionary<string, int> DemandesParStatut { get; set; } = new();
        public Dictionary<string, int> ProjetsParPhase { get; set; } = new();
        public Dictionary<string, int> EvolutionProjets { get; set; } = new();
        public Dictionary<string, int> EvolutionDemandes { get; set; } = new();
        public Dictionary<string, int> ProjetsParDirection { get; set; } = new();
        public Dictionary<string, int> ProjetsParUrgence { get; set; } = new();
        public Dictionary<string, int> ProjetsParCriticite { get; set; } = new();
        public Dictionary<string, int> EvolutionAvancement { get; set; } = new();
    }
}


