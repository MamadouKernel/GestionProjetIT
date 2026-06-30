namespace GestionProjects.Application.Common.Constants;

public static class DomainEntityTypes
{
    public const string Projet = "Projet";
    public const string DemandeProjet = "DemandeProjet";
    public const string DemandeAccesAzureAd = "DemandeAccesAzureAd";
    public const string DemandeCreationCompte = "DemandeCreationCompte";
    public const string DemandeClotureProjet = "DemandeClotureProjet";
    public const string AnomalieProjet = "AnomalieProjet";
    public const string LivrableProjet = "LivrableProjet";
    public const string ChargeProjet = "ChargeProjet";
    public const string BeneficeProjet = "BeneficeProjet";

    /// <summary>
    /// Marqueur virtuel (pas une vraie entité) utilisé pour router une suggestion
    /// d'avenant automatique vers l'onglet Avenants du projet concerné.
    /// </summary>
    public const string AvenantSuggestion = "AvenantSuggestion";
}
