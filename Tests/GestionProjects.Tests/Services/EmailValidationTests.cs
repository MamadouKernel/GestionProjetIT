using FluentAssertions;
using GestionProjects.Application.Common.Helpers;
using Xunit;

namespace GestionProjects.Tests.Services
{
    /// <summary>
    /// Tests rapides sur la validation email (<see cref="ValidationHelper.IsValidEmail"/>).
    /// </summary>
    public class EmailValidationTests
    {
        // -----------------------------------------------------------------------
        // Emails valides
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("user@example.com")]
        [InlineData("jean.kouassi@cit.ci")]
        [InlineData("admin+tag@sub.domain.org")]
        [InlineData("USER@EXAMPLE.COM")]               // insensible à la casse
        [InlineData("prenom.nom@societe.fr")]
        public void IsValidEmail_EmailValide_RetourneTrue(string email)
        {
            ValidationHelper.IsValidEmail(email).Should().BeTrue(
                because: $"'{email}' est un email valide");
        }

        // -----------------------------------------------------------------------
        // Emails invalides
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("sans-arobase")]                   // pas d'@
        [InlineData("manque@domaine")]                 // pas de point dans le domaine
        [InlineData("  espacesautour@test.com  ")]     // espaces de bord (trim non effectué → IsNullOrWhiteSpace = false mais regex échoue)
        [InlineData("@pasdenom.com")]                  // pas de partie locale
        [InlineData("double@@domaine.com")]            // deux arobase
        [InlineData("")]                               // chaîne vide
        [InlineData(null)]                             // null
        public void IsValidEmail_EmailInvalide_RetourneFalse(string? email)
        {
            ValidationHelper.IsValidEmail(email).Should().BeFalse(
                because: $"'{email}' n'est pas un email valide");
        }
    }
}
