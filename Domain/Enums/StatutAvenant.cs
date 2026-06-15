namespace GestionProjects.Domain.Enums
{
    /// <summary>
    /// Cycle de vie d'un avenant : soumis -> validation Métier (DM) -> validation DSI
    /// (qui applique le changement au projet) ; rejet possible à chaque étape.
    /// </summary>
    public enum StatutAvenant
    {
        EnAttenteValidationDM = 1,
        EnAttenteValidationDSI = 2,
        Applique = 3,
        Rejete = 4
    }
}
