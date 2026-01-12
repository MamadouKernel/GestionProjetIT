using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace GestionProjects.Infrastructure.Services
{
        public interface IFileStorageService
        {
            Task<string> SaveFileAsync(IFormFile file, string subfolder, string? identifier = null, string[]? allowedExtensions = null, long? maxSizeBytes = null);
            Task<bool> DeleteFileAsync(string filePath);
            bool IsValidFileExtension(string fileName, string[] allowedExtensions);
            string GetFilePath(string subfolder, string fileName, string? identifier = null);
            bool ValidateFileSignature(IFormFile file, string[] allowedExtensions);
        }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string _baseUploadPath = "uploads";
        private const long DefaultMaxFileSize = 10 * 1024 * 1024; // 10 MB par défaut

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subfolder, string? identifier = null, string[]? allowedExtensions = null, long? maxSizeBytes = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Le fichier est vide.");

            // Validation de la taille
            var maxSize = maxSizeBytes ?? DefaultMaxFileSize;
            if (file.Length > maxSize)
                throw new ArgumentException($"Le fichier dépasse la taille maximale autorisée ({maxSize / 1024 / 1024} MB).");

            // Validation de l'extension si spécifiée
            if (allowedExtensions != null && allowedExtensions.Length > 0)
            {
                if (!IsValidFileExtension(file.FileName, allowedExtensions))
                    throw new ArgumentException($"Type de fichier non autorisé. Extensions autorisées : {string.Join(", ", allowedExtensions)}");
                
                // Validation MIME avec magic bytes (signature de fichier)
                if (!ValidateFileSignature(file, allowedExtensions))
                    throw new ArgumentException("Le type de fichier ne correspond pas à son extension. Fichier potentiellement malveillant.");
            }

            // Validation du type MIME déclaré (basique)
            var contentType = file.ContentType;
            if (string.IsNullOrEmpty(contentType))
                throw new ArgumentException("Le type de fichier ne peut pas être déterminé.");

            // Protection contre path traversal - valider que le chemin ne contient pas de ../
            // Permettre les sous-dossiers mais bloquer les tentatives de path traversal
            if (subfolder.Contains("..") || subfolder.Contains("//") || Path.IsPathRooted(subfolder))
                throw new ArgumentException("Nom de sous-dossier invalide (path traversal détecté).");
            
            // Normaliser le chemin pour éviter les problèmes
            var normalizedSubfolder = subfolder.Replace('\\', '/').Trim('/');
            if (normalizedSubfolder.Contains("../") || normalizedSubfolder.StartsWith("../"))
                throw new ArgumentException("Nom de sous-dossier invalide (path traversal détecté).");

            var uploadsPath = Path.Combine(_environment.WebRootPath, _baseUploadPath, normalizedSubfolder);
            string? normalizedIdentifier = null;
            
            if (!string.IsNullOrEmpty(identifier))
            {
                // Protection path traversal pour l'identifiant
                if (identifier.Contains("..") || identifier.Contains("//") || Path.IsPathRooted(identifier))
                    throw new ArgumentException("Identifiant invalide (path traversal détecté).");
                normalizedIdentifier = identifier.Replace('\\', '/').Trim('/');
                uploadsPath = Path.Combine(uploadsPath, normalizedIdentifier);
            }

            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Utiliser uniquement un GUID pour le nom de fichier (sécurité renforcée)
            var fileExtension = Path.GetExtension(file.FileName);
            var safeFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, safeFileName);

            // Vérifier que le chemin final est bien dans le répertoire autorisé
            var canonicalPath = Path.GetFullPath(filePath);
            var canonicalBasePath = Path.GetFullPath(uploadsPath);
            if (!canonicalPath.StartsWith(canonicalBasePath, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Tentative d'accès non autorisé au système de fichiers.");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Retourner le chemin relatif depuis wwwroot (utiliser les chemins normalisés)
            var relativePath = string.IsNullOrEmpty(identifier) 
                ? Path.Combine(_baseUploadPath, normalizedSubfolder, safeFileName)
                : Path.Combine(_baseUploadPath, normalizedSubfolder, normalizedIdentifier ?? string.Empty, safeFileName);
            return relativePath.Replace('\\', '/');
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fullPath = Path.Combine(_environment.WebRootPath, filePath);
            
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public bool IsValidFileExtension(string fileName, string[] allowedExtensions)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return allowedExtensions.Any(ext => ext.ToLowerInvariant() == extension);
        }

        public string GetFilePath(string subfolder, string fileName, string? identifier = null)
        {
            var path = Path.Combine(_baseUploadPath, subfolder);
            
            if (!string.IsNullOrEmpty(identifier))
            {
                path = Path.Combine(path, identifier);
            }

            return Path.Combine(path, fileName).Replace('\\', '/');
        }

        /// <summary>
        /// Valide la signature de fichier (magic bytes) pour s'assurer que le type réel correspond à l'extension
        /// </summary>
        public bool ValidateFileSignature(IFormFile file, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                return false;

            // Lire les premiers octets du fichier (magic bytes)
            using var stream = file.OpenReadStream();
            var buffer = new byte[Math.Min(20, (int)file.Length)];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            if (bytesRead < buffer.Length && bytesRead < 4)
                return false; // Fichier trop petit pour valider

            // Dictionnaire des signatures de fichiers (magic bytes)
            var fileSignatures = new Dictionary<string, byte[][]>
            {
                { ".pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } }, // %PDF
                { ".doc", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } }, // MS Office 97-2003
                { ".docx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // ZIP (Office 2007+)
                { ".xls", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } }, // MS Office 97-2003
                { ".xlsx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // ZIP (Office 2007+)
                { ".pptx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // ZIP (Office 2007+)
                { ".zip", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } } }, // ZIP
                { ".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } }, // JPEG
                { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } }, // JPEG
                { ".png", new byte[][] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } } // PNG
            };

            // Vérifier si l'extension est dans la liste des extensions autorisées
            if (!allowedExtensions.Any(ext => ext.ToLowerInvariant() == extension))
                return false;

            // Vérifier si on a une signature pour cette extension
            if (!fileSignatures.TryGetValue(extension, out var signatures))
            {
                // Si pas de signature connue, on accepte (pour les extensions moins communes)
                return true;
            }

            // Vérifier si les magic bytes correspondent à l'une des signatures attendues
            foreach (var signature in signatures)
            {
                if (buffer.Length >= signature.Length)
                {
                    bool matches = true;
                    for (int i = 0; i < signature.Length; i++)
                    {
                        if (buffer[i] != signature[i])
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                        return true;
                }
            }

            return false;
        }
    }
}

