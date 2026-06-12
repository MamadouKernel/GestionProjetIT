using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.DemandeProjet
{
    public class DemandeProjetDetailsViewModel
    {
        public Domain.Models.DemandeProjet Demande { get; set; } = null!;
        public bool CanActAsDemandeur { get; set; }
        public bool CanEditAsDemandeur { get; set; }
        public bool CanSubmitDemande { get; set; }
        public bool CanAddDocuments { get; set; }
        public bool CanDuplicateDemande { get; set; }
        public bool CanActAsDm { get; set; }
        public bool CanActAsDsi { get; set; }
        public string BackLinkAction { get; set; } = string.Empty;
        public List<Utilisateur> ChefsProjet { get; set; } = new();
    }
}
