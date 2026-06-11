namespace GestionProjects.Application.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public int StatusCode { get; set; } = 500;
        public string Title { get; set; } = "Une erreur est survenue";
        public string Heading { get; set; } = "Incident d'application";
        public string Description { get; set; } = "Une erreur s'est produite lors du traitement de votre demande.";
        public string IconClass { get; set; } = "bi-exclamation-octagon-fill";
        public string AccentClass { get; set; } = "is-danger";
        public string BadgeLabel { get; set; } = "Erreur système";
        public string? OriginalPath { get; set; }
        public string? Detail { get; set; }
        public bool ShowTechnicalDetails { get; set; }
        public string PrimaryActionText { get; set; } = "Retour à l'accueil";
        public string PrimaryActionUrl { get; set; } = "/";
        public string SecondaryActionText { get; set; } = "Revenir en arrière";

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

