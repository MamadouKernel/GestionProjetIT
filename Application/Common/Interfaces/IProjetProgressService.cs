using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Calcule et persiste le pourcentage d'avancement automatique d'un projet
    /// à partir de sa phase, de ses livrables, tâches et validations.
    /// </summary>
    public interface IProjetProgressService
    {
        /// <summary>Charge les collections/navigations nécessaires au calcul si elles ne le sont pas déjà.</summary>
        Task EnsureDataLoadedAsync(Projet projet);

        /// <summary>Recalcule <see cref="Projet.PourcentageAvancement"/> ; ne persiste que si demandé.</summary>
        Task RecalculateAsync(Projet projet, bool persistChanges = false);
    }
}
