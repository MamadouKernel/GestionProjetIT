namespace GestionProjects.Domain.Enums
{
    /// <summary>
    /// État d'un bénéfice attendu du projet, évalué lors de la revue post-implémentation.
    /// </summary>
    public enum StatutBenefice
    {
        Attendu = 1,
        Realise = 2,
        PartiellementRealise = 3,
        NonRealise = 4
    }
}
