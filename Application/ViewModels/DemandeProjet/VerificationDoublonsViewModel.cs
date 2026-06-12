using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class VerificationDoublonsViewModel
    {
        public Domain.Models.DemandeProjet DemandeCourante { get; set; } = null!;
        public List<DemandeSimilaireDto> DemandesSimilaires { get; set; } = new();
    }

    public class DemandeSimilaireDto
    {
        public Guid DemandeId { get; set; }
        public string Titre { get; set; } = string.Empty;
        public StatutDemande StatutDemande { get; set; }
        public DateTime DateSoumission { get; set; }
        public string Demandeur { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string? CommentaireRejet { get; set; }
        public ProjetExistantDto? ProjetExistant { get; set; }
        public double Similarite { get; set; }
    }

    public class ProjetExistantDto
    {
        public Guid ProjetId { get; set; }
        public string CodeProjet { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;
        public StatutProjet StatutProjet { get; set; }
        public PhaseProjet PhaseActuelle { get; set; }
        public string ChefProjet { get; set; } = string.Empty;
    }
}
