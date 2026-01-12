using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Partie prenante dans la charte de projet
    /// </summary>
    public class PartiePrenanteCharte : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid CharteProjetId { get; set; }
        public CharteProjet CharteProjet { get; set; }

        public string Nom { get; set; } = string.Empty; // Nom complet de la partie prenante
        public string Role { get; set; } = string.Empty; // Ex: "Responsable Système d'Information", "Chef de Section Solution IT"
        public Guid? UtilisateurId { get; set; } // Lien optionnel vers un utilisateur du système
        public Utilisateur? Utilisateur { get; set; }
    }
}

