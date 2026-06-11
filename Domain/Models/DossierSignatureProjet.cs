using GestionProjects.Domain.Common;
using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Models
{
    public class DossierSignatureProjet : EntiteAudit
    {
        public Guid Id { get; set; }
        public Guid ProjetId { get; set; }
        public Projet? Projet { get; set; }

        public TypeDocumentSignatureProjet TypeDocument { get; set; }
        public FournisseurSignatureElectronique Fournisseur { get; set; }
        public StatutDossierSignature Statut { get; set; }

        public Guid? LivrableSourceId { get; set; }
        public LivrableProjet? LivrableSource { get; set; }

        public string NomDocumentSource { get; set; } = string.Empty;
        public string? CheminDocumentSource { get; set; }
        public string? NomDocumentSigne { get; set; }
        public string? CheminDocumentSigne { get; set; }

        public string? ExternalRequestId { get; set; }
        public string? UrlSuivi { get; set; }
        public string MessageStatut { get; set; } = string.Empty;

        public DateTime? DateEnvoi { get; set; }
        public DateTime? DateFinalisation { get; set; }
        public DateTime? DateExpiration { get; set; }

        public ICollection<SignataireDossierSignatureProjet> Signataires { get; set; } = new List<SignataireDossierSignatureProjet>();
    }
}
