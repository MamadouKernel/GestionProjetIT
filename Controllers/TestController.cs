using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class TestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly IPermissionService _permissionService;

        public TestController(
            ApplicationDbContext db,
            IWebHostEnvironment environment,
            IPermissionService permissionService)
        {
            _db = db;
            _environment = environment;
            _permissionService = permissionService;
        }

        public async Task<IActionResult> CheckAdmin()
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (!await _permissionService.CurrentUserHasPermissionAsync("Admin", "Users") &&
                !await _permissionService.CurrentUserHasPermissionAsync("Autorisations", "Index"))
            {
                return Forbid();
            }

            var admin = _db.Utilisateurs.FirstOrDefault(u => u.Matricule == "admin");

            if (admin == null)
                return Content("Utilisateur admin non trouvé dans la base de données.");

            var html = $@"
<!DOCTYPE html>
<html>
<head><title>Diagnostic Admin</title>
<style>body{{font-family:Arial;padding:20px}}.info{{background:#f0f0f0;padding:15px;margin:10px 0;border-radius:5px}}</style>
</head>
<body>
<h2>Diagnostic Utilisateur Admin</h2>
<div class='info'>
  <ul>
    <li><strong>Matricule:</strong> {HtmlEncoder.Default.Encode(admin.Matricule)}</li>
    <li><strong>Nom:</strong> {HtmlEncoder.Default.Encode(admin.Nom)} {HtmlEncoder.Default.Encode(admin.Prenoms)}</li>
    <li><strong>Email:</strong> {HtmlEncoder.Default.Encode(admin.Email)}</li>
    <li><strong>Rôle:</strong> {HtmlEncoder.Default.Encode(admin.Role.ToString())}</li>
    <li><strong>Hash présent:</strong> {(!string.IsNullOrEmpty(admin.MotDePasse) ? "Oui" : "Non")}</li>
    <li><strong>EstSupprime:</strong> {admin.EstSupprime}</li>
    <li><strong>Connexions:</strong> {admin.NombreConnexion}</li>
  </ul>
</div>
<p><a href='/Account/Login'>Retour à la connexion</a></p>
</body></html>";

            return Content(html, "text/html");
        }
    }
}
