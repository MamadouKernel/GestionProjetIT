using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin;

public class DirectionsListViewModel
{
    public List<Direction> Directions { get; set; } = new();
    public List<Utilisateur> DSIs { get; set; } = new();
    /// <summary>
    /// Ids des directions ayant AU MOINS UN Directeur Métier rattaché et actif.
    /// Permet a la vue d'afficher un badge d'alerte pour les directions sans DM
    /// (l'AdminIT doit y affecter un DM avant que des demandes d'acces puissent y aboutir).
    /// </summary>
    public HashSet<Guid> DirectionsAvecDm { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? Recherche { get; set; }
    public string? Statut { get; set; }
}
