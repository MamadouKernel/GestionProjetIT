using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces;

/// <summary>
/// Workflow de validation de la charte projet. Le controller conserve le gating de permission ;
/// le service applique les règles métier, la persistance et l'audit.
/// </summary>
public interface ICharteProjetWorkflowService
{
    /// <summary>
    /// Charge (ou crée par défaut) la charte du projet et assemble le view-model de sa page.
    /// </summary>
    Task<CharteProjetPageViewModel> ObtenirPourAffichageAsync(Projet projet);
    Task<WorkflowResult> SauvegarderAsync(Guid projetId, CharteProjet charte, List<JalonCharte>? jalons, List<PartiePrenanteCharte>? partiesPrenantes, Guid userId);
    Task<WorkflowResult> ValiderDmAsync(Guid projetId, Guid userId);
    Task<WorkflowResult> RejeterDmAsync(Guid projetId, string commentaire);
    Task<WorkflowResult> ValiderDsiAsync(Guid projetId, Guid userId);
    Task<WorkflowResult> RejeterDsiAsync(Guid projetId, string commentaire);
}
