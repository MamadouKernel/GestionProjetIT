using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class ProjetHistoriqueDMViewModel
{
    public IEnumerable<Domain.Models.Projet> Projets { get; set; } = Enumerable.Empty<Domain.Models.Projet>();

    /// <summary>
    /// Données enrichies par projet (historique, statistiques livrables/anomalies/risques).
    /// </summary>
    public List<ProjetHistoriqueItem> ProjetsAvecHistorique { get; set; } = new();

    // Filtres
    public IEnumerable<Direction> Directions { get; set; } = Enumerable.Empty<Direction>();
    public string? Recherche { get; set; }
    public Guid? SelectedDirectionId { get; set; }
    public PhaseProjet? SelectedPhase { get; set; }
    public StatutProjet? SelectedStatut { get; set; }

    // Pagination
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 10;
}

public class ProjetHistoriqueItem
{
    public Domain.Models.Projet Projet { get; set; } = null!;
    public List<AuditLog> AuditLogs { get; set; } = new();
    public int TotalLivrables { get; set; }
    public int TotalAnomalies { get; set; }
    public int AnomaliesOuvertes { get; set; }
    public int TotalRisques { get; set; }
    public int RisquesCritiques { get; set; }
}
