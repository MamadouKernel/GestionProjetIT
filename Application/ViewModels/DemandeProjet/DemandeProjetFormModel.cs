using System.ComponentModel.DataAnnotations;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Helpers;
using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.DemandeProjet;

public class DemandeProjetFormModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Le titre du projet est requis.")]
    public string? Titre { get; set; }

    [Required(ErrorMessage = "La description est requise.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Le contexte est requis.")]
    public string? Contexte { get; set; }

    [Required(ErrorMessage = "Les objectifs sont requis.")]
    public string? Objectifs { get; set; }

    public string? AvantagesAttendus { get; set; }
    public string? Perimetre { get; set; }
    public UrgenceProjet Urgence { get; set; }
    public CriticiteProjet Criticite { get; set; }
    public DateTime? DateMiseEnOeuvreSouhaitee { get; set; }

    [Required(ErrorMessage = "La direction est requise.")]
    public Guid? DirectionId { get; set; }

    public Guid DirecteurMetierId { get; set; }
    public Guid? AutreSponsorId { get; set; }

    public StatutDemande StatutDemande { get; set; }
    public DateTime DateSoumission { get; set; }
    public string? CommentaireDirecteurMetier { get; set; }
    public string? CommentaireDSI { get; set; }
    public string? CahierChargesPath { get; set; }
    public ICollection<DocumentJointDemande> Annexes { get; set; } = new List<DocumentJointDemande>();

    public DocumentJointDemande? CahierChargesDocument => Annexes
        .FirstOrDefault(a => !a.EstSupprime &&
                             !string.IsNullOrWhiteSpace(CahierChargesPath) &&
                             string.Equals(a.CheminRelatif, CahierChargesPath, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<DocumentJointDemande> DocumentsAnnexes => Annexes
        .Where(a => !a.EstSupprime &&
                    (string.IsNullOrWhiteSpace(CahierChargesPath) ||
                     !string.Equals(a.CheminRelatif, CahierChargesPath, StringComparison.OrdinalIgnoreCase)));

    public int PrioriteScore => PrioriteDemandeHelper.CalculateScore(Urgence, Criticite);
    public string PrioriteCode => PrioriteDemandeHelper.GetPrioriteCode(Urgence, Criticite);
    public string PrioriteLibelle => PrioriteDemandeHelper.GetPrioriteLibelle(Urgence, Criticite);
    public string PrioriteBadgeClass => PrioriteDemandeHelper.GetPrioriteBadgeClass(Urgence, Criticite);

    public static DemandeProjetFormModel FromEntity(Domain.Models.DemandeProjet demande) => new()
    {
        Id = demande.Id,
        Titre = demande.Titre,
        Description = demande.Description,
        Contexte = demande.Contexte,
        Objectifs = demande.Objectifs,
        AvantagesAttendus = demande.AvantagesAttendus,
        Perimetre = demande.Perimetre,
        Urgence = demande.Urgence,
        Criticite = demande.Criticite,
        DateMiseEnOeuvreSouhaitee = demande.DateMiseEnOeuvreSouhaitee,
        DirectionId = demande.DirectionId,
        DirecteurMetierId = demande.DirecteurMetierId,
        AutreSponsorId = demande.AutreSponsorId,
        StatutDemande = demande.StatutDemande,
        DateSoumission = demande.DateSoumission,
        CommentaireDirecteurMetier = demande.CommentaireDirecteurMetier,
        CommentaireDSI = demande.CommentaireDSI,
        CahierChargesPath = demande.CahierChargesPath,
        Annexes = demande.Annexes
    };
}
