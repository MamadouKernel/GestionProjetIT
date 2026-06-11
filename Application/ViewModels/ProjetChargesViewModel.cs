using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.ViewModels
{
    public class ProjetChargesViewModel
    {
        public Guid ProjetId { get; set; }
        public string CodeProjet { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Sponsor { get; set; } = string.Empty;
        public string ChefProjet { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Statut { get; set; } = string.Empty;
        public int Avancement { get; set; }
        public string Etat { get; set; } = string.Empty;
        public string ProchainJalon { get; set; } = string.Empty;

        public decimal BudgetPrevisionnel { get; set; }
        public decimal BudgetConsomme { get; set; }
        public decimal BudgetEcart { get; set; }

        public decimal ChargePrevisionnelleTotale { get; set; }
        public decimal ChargeReelleTotale { get; set; }
        public decimal ChargeRestanteEstimee { get; set; }
        public decimal ChargeEcartTotale { get; set; }
        public decimal CapaciteTotale { get; set; }
        public double TauxCapaciteUtilise { get; set; }
        public double TauxConsommation { get; set; }
        public int NombreRessources { get; set; }
        public int RessourcesSurchargees { get; set; }
        public int ChargesEnAttenteValidation { get; set; }

        public bool CanEditForecast { get; set; }
        public bool CanEditActual { get; set; }
        public bool CanValidateCharges { get; set; }
        public bool CanExport { get; set; }

        public List<ProjetChargesWeekViewModel> Weeks { get; set; } = new();
        public List<ProjetChargesResourceRowViewModel> Resources { get; set; } = new();
        public List<ProjetChargesWeeklySummaryViewModel> WeeklySummaries { get; set; } = new();
        public List<ProjetChargeAlertViewModel> Alerts { get; set; } = new();
        public List<ProjetChargeActivityViewModel> Activities { get; set; } = new();
    }

    public class ProjetChargesWeekViewModel
    {
        public DateTime StartDate { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
        public bool IsPast { get; set; }
        public bool IsFuture { get; set; }
    }

    public class ProjetChargesResourceRowViewModel
    {
        public Guid ResourceId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public decimal WeeklyCapacity { get; set; }
        public decimal CapacityTotal { get; set; }
        public decimal PlannedTotal { get; set; }
        public decimal ActualTotal { get; set; }
        public decimal VarianceTotal { get; set; }
        public decimal RemainingCapacity { get; set; }
        public double UtilizationRate { get; set; }
        public string CapacityStatus { get; set; } = string.Empty;
        public string CapacityStatusClass { get; set; } = string.Empty;
        public List<ProjetChargeCellViewModel> Cells { get; set; } = new();
    }

    public class ProjetChargeCellViewModel
    {
        public Guid ResourceId { get; set; }
        public DateTime WeekStart { get; set; }
        public decimal PlannedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public decimal VarianceHours { get; set; }
        public decimal AllocationPercentage { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string TypeActivite { get; set; } = string.Empty;
        public string Activite { get; set; } = string.Empty;
        public string ValidationComment { get; set; } = string.Empty;
        public StatutValidationCharge ValidationStatus { get; set; }
        public string ValidationStatusLabel { get; set; } = string.Empty;
        public string ValidationStatusClass { get; set; } = string.Empty;
        public double UtilizationRate { get; set; }
        public string CapacityStatus { get; set; } = string.Empty;
        public string CapacityStatusClass { get; set; } = string.Empty;
        public bool IsMissingActual { get; set; }
        public bool CanEditForecast { get; set; }
        public bool CanEditActual { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanReview { get; set; }
    }

    public class ProjetChargesWeeklySummaryViewModel
    {
        public DateTime WeekStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal PlannedTotal { get; set; }
        public decimal ActualTotal { get; set; }
        public decimal CapacityTotal { get; set; }
        public int MissingEntries { get; set; }
        public int PendingValidations { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
    }

    public class ProjetChargeAlertViewModel
    {
        public string Level { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi bi-info-circle";
        public string Message { get; set; } = string.Empty;
    }

    public class ProjetChargeActivityViewModel
    {
        public string DateLabel { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string TypeActivite { get; set; } = string.Empty;
        public string Activite { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        public string Comment { get; set; } = string.Empty;
    }
}
