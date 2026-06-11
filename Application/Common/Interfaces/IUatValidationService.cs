using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.Common.Interfaces
{
    public interface IUatValidationService
    {
        Task<UatValidationResult> ValiderRecetteAsync(Guid projetId);
        Task<UatValidationResult> ValiderFinUatAsync(Guid projetId);
        Task<string> GenererReferenceCasTestAsync(Projet projet);
        Task<CampagneTestProjet> AssurerCampagneParDefautAsync(Projet projet, string? creePar);
    }

    public class UatValidationResult
    {
        public bool EstValide { get; set; }
        public List<string> Erreurs { get; set; } = new();
        public int TotalCasObligatoires { get; set; }
        public int CasValides { get; set; }
        public int CasSansExecution { get; set; }
        public int CasEnEchecOuBloques { get; set; }
        public int AnomaliesBloquantes { get; set; }
        public int CampagnesOuvertes { get; set; }

        public string MessageErreur => string.Join(" ", Erreurs);
    }
}
