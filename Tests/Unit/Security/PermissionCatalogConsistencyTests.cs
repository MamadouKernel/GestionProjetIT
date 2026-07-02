using System.Text.RegularExpressions;
using FluentAssertions;
using GestionProjects.Application.Common.Security;
using Xunit;

namespace GestionProjects.Tests.Unit.Security;

/// <summary>
/// Garde-fou contre la dérive entre le code (CurrentUserHasPermissionAsync) et le
/// catalogue de permissions affiché/configurable depuis l'écran Autorisations.
/// Sans ce test, une permission ajoutée dans le code mais oubliée dans
/// PermissionCatalog reste invisible et non configurable, pour toujours.
/// </summary>
public sealed class PermissionCatalogConsistencyTests
{
    private static readonly Regex PermissionCallPattern = new(
        @"CurrentUserHasPermissionAsync\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\)",
        RegexOptions.Compiled);

    [Fact]
    public void ToutesLesVerificationsDePermissionSontRepertorieesDansLeCatalogue()
    {
        var racine = TrouverRacineDepot();
        var dossiers = new[] { "Controllers", Path.Combine("Infrastructure", "Services"), "Web" };

        var permissionsUtilisees = new HashSet<(string Controleur, string Action)>();

        foreach (var dossier in dossiers)
        {
            var chemin = Path.Combine(racine, dossier);
            if (!Directory.Exists(chemin))
            {
                continue;
            }

            foreach (var fichier in Directory.EnumerateFiles(chemin, "*.cs", SearchOption.AllDirectories))
            {
                var contenu = File.ReadAllText(fichier);
                foreach (Match match in PermissionCallPattern.Matches(contenu))
                {
                    permissionsUtilisees.Add((match.Groups[1].Value, match.Groups[2].Value));
                }
            }
        }

        permissionsUtilisees.Should().NotBeEmpty("le scan de fichiers doit trouver au moins les verifications connues");

        var catalogue = new HashSet<(string Controleur, string Action)>(
            PermissionCatalog.GetDefinitions().Select(d => (d.Controleur, d.Action)));

        var manquantes = permissionsUtilisees
            .Where(p => !catalogue.Contains(p))
            .OrderBy(p => p.Controleur).ThenBy(p => p.Action)
            .Select(p => $"{p.Controleur}.{p.Action}")
            .ToList();

        manquantes.Should().BeEmpty(
            "chaque permission verifiee dans le code doit etre repertoriee dans PermissionCatalog.GetDefinitions() " +
            "pour rester visible et configurable depuis l'ecran Autorisations. Manquantes : " +
            string.Join(", ", manquantes));
    }

    private static string TrouverRacineDepot()
    {
        var repertoire = new DirectoryInfo(AppContext.BaseDirectory);
        while (repertoire != null && !File.Exists(Path.Combine(repertoire.FullName, "GestionProjects.csproj")))
        {
            repertoire = repertoire.Parent;
        }

        if (repertoire == null)
        {
            throw new InvalidOperationException(
                $"Racine du depot (GestionProjects.csproj) introuvable en remontant depuis {AppContext.BaseDirectory}");
        }

        return repertoire.FullName;
    }
}
