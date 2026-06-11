using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class TacheCollaborationProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet? Projet { get; set; }

        public Guid CollaborationProjetId { get; set; }
        public CollaborationProjet? CollaborationProjet { get; set; }

        public PhaseProjet Phase { get; set; }
        public string Titre { get; set; } = string.Empty;
        public StatutTacheCollaborationProjet Statut { get; set; }
        public DateTime? DateEcheance { get; set; }

        public Guid? AssigneeId { get; set; }
        public Utilisateur? Assignee { get; set; }

        public string? ExternalTaskId { get; set; }
        public string? ExternalBucketId { get; set; }
        public bool EstSynchronisee { get; set; }
    }
}
