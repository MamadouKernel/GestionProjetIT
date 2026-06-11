namespace GestionProjects.Application.Common.Interfaces
{
    public interface ITeamsNotificationService
    {
        Task EnvoyerNouvelleDemandeAsync(string titreDemande, string demandeur, string direction, string directeurMetier, Guid demandeId);
        Task EnvoyerValidationDMAsync(string titreDemande, string directeurMetier, bool approuve, string? commentaire, Guid demandeId);
        Task EnvoyerValidationDSIAsync(string titreDemande, string dsi, bool approuve, string? commentaire, Guid demandeId);
        Task EnvoyerRejetOuRenvoiAsync(string titreDemande, string acteur, string action, string? commentaire, Guid demandeId);
        Task EnvoyerChangementPhaseAsync(string titreProjet, string anciennePhase, string nouvellePhase, string modifiePar, Guid projetId);
        Task EnvoyerDemandeClotureAsync(string titreProjet, string demandePar, Guid projetId);
        Task EnvoyerNotificationGeneraleAsync(string titre, string message, string couleur = "0076D7");
    }
}
