using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    public class DocumentJointDemande : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid DemandeProjetId { get; set; }
        public DemandeProjet DemandeProjet { get; set; } = null!;

        public string NomFichier { get; set; } = string.Empty;
        public string CheminRelatif { get; set; } = string.Empty;
        public DateTime DateDepot { get; set; }

        public Guid? DeposeParId { get; set; }
        public Utilisateur? DeposePar { get; set; }
    }
}
