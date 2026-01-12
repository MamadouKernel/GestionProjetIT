namespace GestionProjects.Domain.Enums
{
    public enum TypeNotification
    {
        // Notifications Responsable Solutions IT
        ProjetEntreEnUAT = 1,
        ProjetEntreEnMEP = 2,
        DemandeSupportTechnique = 3,
        
        // Notifications générales
        DemandeValidationDSI = 4,
        DemandeValidationDM = 5,
        ProjetPhaseChangee = 6,
        NouveauRisqueCritique = 7,
        NouvelleAnomalie = 8,
        LivrableDepose = 9,
        
        // Notifications DSI
        DelegationDSIActivee = 10,
        DelegationDSIExpiree = 11
    }
}

