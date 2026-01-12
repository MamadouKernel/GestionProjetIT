using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models
{
    /// <summary>
    /// Charte de projet complète selon le modèle CIT
    /// </summary>
    public class CharteProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet Projet { get; set; }

        // Identification du projet
        public string NomProjet { get; set; } = string.Empty;
        public string? NumeroProjet { get; set; }
        public string ObjectifProjet { get; set; } = string.Empty; // Peut contenir plusieurs objectifs séparés par des retours à la ligne
        public string AssuranceQualite { get; set; } = string.Empty;
        public string Perimetre { get; set; } = string.Empty;
        public string ContraintesInitiales { get; set; } = string.Empty;
        public string RisquesInitiaux { get; set; } = string.Empty;

        // Acteurs du projet
        public Guid DemandeurId { get; set; }
        public Utilisateur Demandeur { get; set; }
        public string Sponsors { get; set; } = string.Empty; // Peut contenir plusieurs sponsors
        public Guid ChefProjetId { get; set; }
        public Utilisateur ChefProjet { get; set; }
        public string? EmailChefProjet { get; set; }

        // Informations du document
        public string CodeDocument { get; set; } = string.Empty; // Ex: CIT-CIV-DSI-CP-0001-Rév.01
        public string TypeDocument { get; set; } = "Charte de projet";
        public string Departement { get; set; } = "SYSTEME D'INFORMATION";
        public int NumeroRevision { get; set; } = 1;
        public DateTime? DateRevision { get; set; }
        public string? DescriptionRevision { get; set; }
        public string? RedigePar { get; set; }
        public string? VerifiePar { get; set; }
        public string? ApprouvePar { get; set; }

        // Autorisation officielle
        public bool SignatureSponsor { get; set; } = false;
        public DateTime? DateSignatureSponsor { get; set; }
        public Guid? SignatureSponsorId { get; set; }
        public Utilisateur? SignatureSponsorUtilisateur { get; set; }
        public bool SignatureChefProjet { get; set; } = false;
        public DateTime? DateSignatureChefProjet { get; set; }
        public Guid? SignatureChefProjetId { get; set; }
        public Utilisateur? SignatureChefProjetUtilisateur { get; set; }

        // Collections
        public ICollection<JalonCharte> Jalons { get; set; } = new List<JalonCharte>();
        public ICollection<PartiePrenanteCharte> PartiesPrenantes { get; set; } = new List<PartiePrenanteCharte>();
    }
}

