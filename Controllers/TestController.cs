using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GestionProjects.Controllers
{
    /// <summary>
    /// Contrôleur de test - UNIQUEMENT accessible en environnement de développement
    /// et nécessite le rôle AdminIT pour des raisons de sécurité
    /// </summary>
    [Authorize(Roles = "AdminIT")]
    public class TestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;

        public TestController(ApplicationDbContext db, IWebHostEnvironment environment)
        {
            _db = db;
            _environment = environment;
        }

        public IActionResult CheckAdmin()
        {
            // Ne permettre que en développement
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            var admin = _db.Utilisateurs.FirstOrDefault(u => u.Matricule == "admin");
            
            if (admin == null)
            {
                return Content("❌ Utilisateur admin NON TROUVÉ dans la base de données.");
            }

            // Test de vérification du mot de passe
            bool testPassword = false;
            string passwordTestResult = "";
            string hashInfo = "";
            try
            {
                if (string.IsNullOrEmpty(admin.MotDePasse))
                {
                    passwordTestResult = "❌ ERREUR: Mot de passe vide en base";
                }
                else
                {
                    testPassword = BCrypt.Net.BCrypt.Verify("Admin@123", admin.MotDePasse);
                    passwordTestResult = testPassword ? "✅ CORRECT - Le mot de passe 'Admin@123' fonctionne" : "❌ INCORRECT - Le mot de passe 'Admin@123' ne correspond pas au hash";
                    hashInfo = $"Hash (premiers 30 caractères): {admin.MotDePasse.Substring(0, Math.Min(30, admin.MotDePasse.Length))}...";
                }
            }
            catch (Exception ex)
            {
                passwordTestResult = $"❌ ERREUR: {ex.Message}";
            }

            // Test avec différents mots de passe possibles
            var testResults = new List<string>();
            var testPasswords = new[] { "Admin@123", "admin", "Admin", "admin123", "Admin123" };
            foreach (var pwd in testPasswords)
            {
                try
                {
                    if (!string.IsNullOrEmpty(admin.MotDePasse))
                    {
                        var result = BCrypt.Net.BCrypt.Verify(pwd, admin.MotDePasse);
                        if (result)
                        {
                            testResults.Add($"✅ '{pwd}' = CORRECT");
                        }
                    }
                }
                catch { }
            }

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Test Admin</title>
    <style>
        body {{ font-family: Arial; padding: 20px; }}
        .success {{ color: green; }}
        .error {{ color: red; }}
        .info {{ background: #f0f0f0; padding: 15px; margin: 10px 0; border-radius: 5px; }}
    </style>
</head>
<body>
    <h2>Diagnostic Utilisateur Admin</h2>
    
    <div class='info'>
        <h3>Informations utilisateur :</h3>
        <ul>
            <li><strong>Matricule:</strong> {admin.Matricule}</li>
            <li><strong>Nom:</strong> {admin.Nom} {admin.Prenoms}</li>
            <li><strong>Email:</strong> {admin.Email}</li>
            <li><strong>Role:</strong> {admin.Role}</li>
            <li><strong>EstSupprime:</strong> {admin.EstSupprime}</li>
            <li><strong>Nombre de connexions:</strong> {admin.NombreConnexion}</li>
        </ul>
    </div>

    <div class='info'>
        <h3>Test mot de passe 'Admin@123':</h3>
        <p class='{(testPassword ? "success" : "error")}'><strong>{passwordTestResult}</strong></p>
        <p>{hashInfo}</p>
    </div>

    {(testResults.Any() ? $@"
    <div class='info'>
        <h3>Mots de passe qui fonctionnent :</h3>
        <ul>
            {string.Join("", testResults.Select(r => $"<li>{r}</li>"))}
        </ul>
    </div>
    " : "")}

    <div class='info'>
        <h3>Actions :</h3>
        <p><a href='/Test/ResetAdminPassword' style='background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Réinitialiser le mot de passe admin</a></p>
        <p><a href='/Account/Login' style='background: #28a745; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Retour à la connexion</a></p>
    </div>
</body>
</html>";

            return Content(html, "text/html");
        }

        public IActionResult ResetAdminPassword()
        {
            // Ne permettre que en développement
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            var admin = _db.Utilisateurs.FirstOrDefault(u => u.Matricule == "admin");
            
            if (admin == null)
            {
                return Content("❌ Utilisateur admin non trouvé.");
            }

            try
            {
                var newHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                var oldHash = admin.MotDePasse;
                admin.MotDePasse = newHash;
                _db.SaveChanges();
                
                // Vérifier que le nouveau hash fonctionne
                var testVerify = BCrypt.Net.BCrypt.Verify("Admin@123", newHash);
                
                var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Réinitialisation Mot de Passe</title>
    <style>
        body {{ font-family: Arial; padding: 20px; }}
        .success {{ color: green; background: #d4edda; padding: 15px; border-radius: 5px; }}
        .info {{ background: #f0f0f0; padding: 15px; margin: 10px 0; border-radius: 5px; }}
        a {{ background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 5px; }}
    </style>
</head>
<body>
    <div class='success'>
        <h2>✅ Mot de passe réinitialisé avec succès !</h2>
        <p><strong>Nouveau mot de passe:</strong> Admin@123</p>
        <p><strong>Test de vérification:</strong> {(testVerify ? "✅ PASSÉ" : "❌ ÉCHOUÉ")}</p>
    </div>
    
    <div class='info'>
        <h3>Informations :</h3>
        <p><strong>Ancien hash:</strong> {(string.IsNullOrEmpty(oldHash) ? "N/A" : oldHash.Substring(0, Math.Min(30, oldHash.Length)))}...</p>
        <p><strong>Nouveau hash:</strong> {newHash.Substring(0, Math.Min(30, newHash.Length))}...</p>
    </div>
    
    <p>
        <a href='/Account/Login'>Retour à la connexion</a>
        <a href='/Test/CheckAdmin'>Vérifier l'utilisateur</a>
    </p>
</body>
</html>";
                
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Content($"❌ Erreur: {ex.Message}\n\nStack trace: {ex.StackTrace}");
            }
        }
    }
}

