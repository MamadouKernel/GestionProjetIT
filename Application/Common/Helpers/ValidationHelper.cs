using System.Text.RegularExpressions;

namespace GestionProjects.Application.Common.Helpers
{
    /// <summary>
    /// Helpers pour les validations communes
    /// </summary>
    public static class ValidationHelper
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Valide le format d'un email
        /// </summary>
        public static bool IsValidEmail(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);
        }

        /// <summary>
        /// Valide la longueur minimale d'un mot de passe
        /// </summary>
        public static bool IsValidPasswordLength(string? password, int minLength = 6)
        {
            return !string.IsNullOrWhiteSpace(password) && password.Length >= minLength;
        }

        /// <summary>
        /// Valide qu'une chaîne n'est pas vide ou null
        /// </summary>
        public static bool IsNotEmpty(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Normalise une chaîne (trim et suppression des espaces multiples)
        /// </summary>
        public static string? NormalizeString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }
    }
}

