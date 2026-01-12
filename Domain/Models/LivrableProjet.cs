using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class LivrableProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        public PhaseProjet Phase { get; set; }
        public TypeLivrable TypeLivrable { get; set; }

        public string NomDocument { get; set; }
        public string CheminRelatif { get; set; }

        public DateTime DateDepot { get; set; }

        public Guid? DeposeParId { get; set; }
        public Utilisateur DeposePar { get; set; }

        public string Commentaire { get; set; }
        public string Version { get; set; }
    }
}
