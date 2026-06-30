using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Évaluation d'un membre de l'équipe projet par le Chef de Projet ou la DSI,
    /// renseignée typiquement à la clôture. Une seule évaluation par membre par projet.
    /// </summary>
    public class EvaluationMembreProjet : EntiteAudit
    {
        public Guid Id { get; set; }

        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; } = null!;

        public Guid MembreProjetId { get; set; }
        public MembreProjet MembreProjet { get; set; } = null!;

        public Guid EvaluateurId { get; set; }
        public Utilisateur Evaluateur { get; set; } = null!;

        public DateTime DateEvaluation { get; set; }

        public int NoteQualite { get; set; }        // 1-5
        public int NoteRespectDelais { get; set; }   // 1-5
        public int NoteCollaboration { get; set; }   // 1-5

        public string? Commentaire { get; set; }
    }
}
