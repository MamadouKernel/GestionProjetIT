namespace GestionProjects.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task EnvoyerAsync(string destinataire, string sujet, string corpsHtml);
        Task EnvoyerAsync(IEnumerable<string> destinataires, string sujet, string corpsHtml);
        Task EnvoyerNouvelleDemandeAuDMAsync(string emailDM, string nomDM, string titreDemande, string nomDemandeur, string direction);
        Task EnvoyerValidationDMVersdsIAsync(string emailDSI, string titreDemande, string nomDM, string? commentaire);
        Task EnvoyerRejetOuRenvoiAuDemandeurAsync(string emailDemandeur, string nomDemandeur, string titreDemande, string acteur, string action, string? commentaire);
        Task EnvoyerRejetDSIAsync(string emailDemandeur, string? emailDM, string titreDemande, string nomDSI, string? commentaire);
        Task EnvoyerValidationDSIProjetCreeAsync(string emailDemandeur, string? emailDM, string titreDemande, string codeProjet);
        Task EnvoyerPdfCharteAsync(string emailDM, string emailDSI, string titreDemande, string nomChefProjet, byte[] pdfBytes, string nomFichier);
        Task EnvoyerDemandeAccesAsync(string emailAdmin, string nomDemandeur, string emailDemandeur, string rolesSouhaites);
        Task EnvoyerDemandeCreationCompteAuDMAsync(string emailDM, string nomDM, string nomComplet, string direction, string service, string emailDemandeur);
        Task EnvoyerDemandeCreationCompteAuDSIAsync(string emailDSI, string nomComplet, string nomDM, string direction, string service);
        Task EnvoyerCredentielsAsync(string email, string nomComplet, string username, string motDePasse);
        Task EnvoyerConfirmationCreationCompteAuDMAsync(string emailDM, string nomDM, string nomNouvelUtilisateur);
        Task EnvoyerRefusCreationCompteAsync(string emailDemandeur, string nomComplet, string acteur, string? commentaire);
        Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null);
    }
}
