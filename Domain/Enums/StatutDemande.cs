namespace GestionProjects.Domain.Enums
{
    public enum StatutDemande
    {
        Brouillon = 1,

        EnAttenteValidationDirecteurMetier = 2,
        CorrectionDemandeeParDirecteurMetier = 3,
        RejeteeParDirecteurMetier = 4,

        EnAttenteValidationDSI = 5,
        RetourneeAuDemandeurParDSI = 6,
        RetourneeAuDirecteurMetierParDSI = 7,
        RejeteeParDSI = 8,

        ValideeParDSI = 9
    }
}
