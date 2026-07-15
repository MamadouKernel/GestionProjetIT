using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class ProjetDetailsViewModel
{
    public Domain.Models.Projet Projet { get; set; } = null!;

    // Onglet actif
    public string ActiveTab { get; set; } = "synthese";

    // Flags d'accès
    public bool IsReadOnly { get; set; }
    public bool IsDemandeurProject { get; set; }
    public bool CanAccessCharges { get; set; }

    // Données onglet UAT (cas de tests)
    public List<CasTestProjet> CasTests { get; set; } = new();
    public List<CampagneTestProjet> Campagnes { get; set; } = new();

    // Données onglet Collaboration + Exécution
    public CollaborationProjet? Collaboration { get; set; }

    // Données onglet Planification (dossiers de signature)
    public List<DossierSignatureProjet> DossiersSignature { get; set; } = new();

    // Données onglet Historique
    public IEnumerable<AuditLog> AuditLogs { get; set; } = Enumerable.Empty<AuditLog>();

    // Données onglet Avenants (gestion du changement)
    public List<AvenantProjet> Avenants { get; set; } = new();

    // Données onglet Bénéfices (réalisation de la valeur)
    public List<BeneficeProjet> Benefices { get; set; } = new();

    // Données onglet Clôture (évaluation des membres)
    public List<EvaluationMembreProjet> EvaluationsMembres { get; set; } = new();

    // Données pour sélection du chef de projet (synthèse)
    public List<Utilisateur> ChefsProjet { get; set; } = new();

    // Utilisateurs non supprimés, sélectionnables comme membre du projet (onglet Analyse)
    public List<Utilisateur> UtilisateursDisponibles { get; set; } = new();
}
