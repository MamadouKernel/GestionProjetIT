namespace GestionProjects.Application.Common.Helpers;

/// <summary>
/// Génère un code (sigle) à partir d'un libellé : initiales des mots
/// significatifs, en ignorant les mots-outils. Utilisé pour les codes
/// de directions et de services.
/// </summary>
public static class CodeGenerator
{
    private static readonly HashSet<string> MotsIgnores = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "des", "du", "d'", "le", "la", "les", "un", "une",
        "et", "ou", "à", "au", "aux", "en", "pour", "par", "avec",
        "sans", "sous", "sur", "dans", "entre", "vers"
    };

    public static string FromLibelle(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            return string.Empty;

        var normalise = libelle.Trim().ToLowerInvariant()
            .Replace("'", " ").Replace("-", " ").Replace("  ", " ").Trim();

        if (normalise.Contains("direction") && normalise.Contains("exploitation"))
            return "DEX";

        var mots = libelle
            .Replace("'", " ").Replace("-", " ").Replace("  ", " ").Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var code = new System.Text.StringBuilder();
        foreach (var mot in mots)
        {
            var m = mot.Trim();
            if (m.Length >= 2 && !MotsIgnores.Contains(m))
                code.Append(char.ToUpperInvariant(m[0]));
        }

        return code.ToString();
    }
}
