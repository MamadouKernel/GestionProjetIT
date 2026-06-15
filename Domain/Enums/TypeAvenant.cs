namespace GestionProjects.Domain.Enums
{
    /// <summary>
    /// Nature d'un avenant projet (demande de changement maîtrisée après baseline).
    /// </summary>
    public enum TypeAvenant
    {
        Perimetre = 1,
        Budget = 2,
        Delai = 3,
        Mixte = 4
    }
}
