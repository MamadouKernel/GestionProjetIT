using FluentAssertions;
using GestionProjects.Application.Common.Helpers;
using Xunit;

namespace GestionProjects.Tests.Helpers
{
    public class ValidationHelperTests
    {
        [Fact]
        public void IsStrongPassword_MotDePasseVide_RetourneFaux()
        {
            ValidationHelper.IsStrongPassword("").Should().BeFalse();
        }

        [Fact]
        public void IsStrongPassword_MoinsDe12Caracteres_RetourneFaux()
        {
            // 11 caractères, majuscule et chiffre présents
            ValidationHelper.IsStrongPassword("Password1ab").Should().BeFalse();
        }

        [Fact]
        public void IsStrongPassword_SansMajuscule_RetourneFaux()
        {
            // 12 caractères, chiffre présent, mais aucune majuscule
            ValidationHelper.IsStrongPassword("motdepasse12").Should().BeFalse();
        }

        [Fact]
        public void IsStrongPassword_SansChiffre_RetourneFaux()
        {
            // 12 caractères, majuscule présente, mais aucun chiffre
            ValidationHelper.IsStrongPassword("MotDePasseAbc").Should().BeFalse();
        }

        [Fact]
        public void IsStrongPassword_ValideAvecPlusDe12Chars_RetourneVrai()
        {
            // 14 caractères, majuscule, chiffre
            ValidationHelper.IsStrongPassword("MotDePasse1234").Should().BeTrue();
        }

        [Fact]
        public void IsStrongPassword_Exactement12CharsAvecMajusculeEtChiffre_RetourneVrai()
        {
            // Exactement 12 caractères = longueur minimale
            ValidationHelper.IsStrongPassword("MotDePasse12").Should().BeTrue();
        }
    }
}
