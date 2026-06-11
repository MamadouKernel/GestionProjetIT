using System.IO;

namespace GestionProjects.Infrastructure.Services
{
    public static class DocumentBrandingHelper
    {
        public const string CompanyName = "Côte d'Ivoire Terminal";
        public const string CompanySite = "Abidjan";
        public const string ApplicationName = "Zéïnab";

        private static readonly string[] CandidateLogoFiles =
        {
            "LOGO_COTE_D_IVOIRE_TERMINAL.png",
            "logo_cit.jpeg"
        };

        public static string? GetLogoAbsolutePath()
        {
            var root = Directory.GetCurrentDirectory();
            foreach (var logoFile in CandidateLogoFiles)
            {
                var absolutePath = Path.Combine(root, "wwwroot", "images", logoFile);
                if (File.Exists(absolutePath))
                {
                    return absolutePath;
                }
            }

            return null;
        }

        public static byte[]? TryGetLogoBytes()
        {
            var absolutePath = GetLogoAbsolutePath();
            return absolutePath != null ? File.ReadAllBytes(absolutePath) : null;
        }
    }
}
