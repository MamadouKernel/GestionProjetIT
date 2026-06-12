using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels.DemandesAcces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    [Authorize]
    public class DemandesAccesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly IPermissionService _permissionService;

        public DemandesAccesController(
            ApplicationDbContext db,
            ICurrentUserService currentUserService,
            IAuditService auditService,
            IEmailService emailService,
            IPermissionService permissionService)
        {
            _db = db;
            _currentUserService = currentUserService;
            _auditService = auditService;
            _emailService = emailService;
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? recherche = null,
            StatutDemandeAcces? statut = null,
            int page = 1, int pageSize = 15)
        {
            if (!await CanManageAccessRequestsAsync())
                return Forbid();

            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = _db.DemandesAccesAzureAd
                .Include(d => d.DirectionDetectee)
                .Include(d => d.TraitePar)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
                query = query.Where(d =>
                    d.Nom.Contains(recherche) ||
                    d.Prenoms.Contains(recherche) ||
                    d.Email.Contains(recherche) ||
                    d.Matricule.Contains(recherche));

            if (statut.HasValue)
                query = query.Where(d => d.Statut == statut.Value);

            query = query.OrderBy(d => d.Statut).ThenByDescending(d => d.DateCreation);

            var paged = await query.ToPagedResultAsync(page, pageSize);

            var directions = await _db.Directions
                .Where(d => !d.EstSupprime && d.EstActive)
                .OrderBy(d => d.Libelle)
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Libelle })
                .ToListAsync();

            var vm = new DemandesAccesIndexViewModel
            {
                Items          = paged.Items,
                Directions     = directions,
                Recherche      = recherche,
                SelectedStatut = statut,
                TotalCount     = paged.TotalCount,
                PageNumber     = paged.PageNumber,
                TotalPages     = paged.TotalPages,
                PageSize       = paged.PageSize
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approuver(Guid id, Guid? directionId, string? commentaire)
        {
            if (!await CanManageAccessRequestsAsync())
            {
                return Forbid();
            }

            var demande = await _db.DemandesAccesAzureAd
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
            {
                TempData["Error"] = "Demande introuvable.";
                return RedirectToAction(nameof(Index));
            }

            if (demande.Statut != StatutDemandeAcces.EnAttente)
            {
                TempData["Error"] = "Cette demande a déjà été traitée.";
                return RedirectToAction(nameof(Index));
            }

            var utilisateur = await _db.Utilisateurs
                .Include(u => u.UtilisateurRoles)
                .FirstOrDefaultAsync(u => u.Email == demande.Email && !u.EstSupprime);

            if (utilisateur == null)
            {
                utilisateur = new Utilisateur
                {
                    Id = Guid.NewGuid(),
                    Matricule = demande.Matricule,
                    MotDePasse = BCrypt.Net.BCrypt.HashPassword($"{Guid.NewGuid():N}Aa1!"),
                    Nom = demande.Nom,
                    Prenoms = demande.Prenoms,
                    Email = demande.Email,
                    DirectionId = directionId ?? demande.DirectionDetecteeId,
                    PeutCreerDemandeProjet = true,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                };

                _db.Utilisateurs.Add(utilisateur);
            }
            else
            {
                utilisateur.Nom = string.IsNullOrWhiteSpace(utilisateur.Nom) ? demande.Nom : utilisateur.Nom;
                utilisateur.Prenoms = string.IsNullOrWhiteSpace(utilisateur.Prenoms) ? demande.Prenoms : utilisateur.Prenoms;
                utilisateur.Matricule = string.IsNullOrWhiteSpace(utilisateur.Matricule) ? demande.Matricule : utilisateur.Matricule;
                utilisateur.DirectionId = directionId ?? demande.DirectionDetecteeId;
                utilisateur.PeutCreerDemandeProjet = true;
            }

            var aRoleDemandeur = utilisateur.UtilisateurRoles.Any(ur =>
                !ur.EstSupprime &&
                ur.Role == RoleUtilisateur.Demandeur &&
                (!ur.DateDebut.HasValue || ur.DateDebut.Value <= DateTime.Now) &&
                (!ur.DateFin.HasValue || ur.DateFin.Value >= DateTime.Now));

            if (!aRoleDemandeur)
            {
                _db.UtilisateurRoles.Add(new UtilisateurRole
                {
                    Id = Guid.NewGuid(),
                    UtilisateurId = utilisateur.Id,
                    Role = RoleUtilisateur.Demandeur,
                    DateDebut = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                });
            }

            demande.Statut = StatutDemandeAcces.Approuvee;
            demande.DirectionDetecteeId = directionId ?? demande.DirectionDetecteeId;
            demande.CommentaireTraitement = commentaire?.Trim() ?? string.Empty;
            demande.DateTraitement = DateTime.Now;
            demande.TraiteParId = User.GetUserIdOrThrow();
            demande.UtilisateurCreeId = utilisateur.Id;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync(
                "APPROBATION_DEMANDE_ACCES_AZURE_AD",
                "DemandeAccesAzureAd",
                demande.Id,
                null,
                new
                {
                    demande.Email,
                    UtilisateurId = utilisateur.Id,
                    DirectionId = demande.DirectionDetecteeId
                });

            var body = $"""
                <p>Bonjour {demande.Prenoms} {demande.Nom},</p>
                <p>Votre demande d'accès à l'application Zéïnab a été approuvée.</p>
                <p>Vous pouvez désormais vous connecter avec votre compte Microsoft 365 CIT.</p>
                {(string.IsNullOrWhiteSpace(demande.CommentaireTraitement) ? string.Empty : $"<p>Commentaire administrateur : {demande.CommentaireTraitement}</p>")}
                <p>Cordialement,<br />DSI - Zéïnab</p>
                """;

            await _emailService.SendEmailAsync(
                demande.Email,
                "Accès approuvé - Zéïnab",
                body,
                $"Bonjour {demande.Prenoms} {demande.Nom}, votre demande d'accès a été approuvée.");

            TempData["Success"] = "Demande approuvée et accès utilisateur préparé.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rejeter(Guid id, string commentaire)
        {
            if (!await CanManageAccessRequestsAsync())
            {
                return Forbid();
            }

            var demande = await _db.DemandesAccesAzureAd
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
            {
                TempData["Error"] = "Demande introuvable.";
                return RedirectToAction(nameof(Index));
            }

            if (demande.Statut != StatutDemandeAcces.EnAttente)
            {
                TempData["Error"] = "Cette demande a déjà été traitée.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire de rejet est obligatoire.";
                return RedirectToAction(nameof(Index));
            }

            demande.Statut = StatutDemandeAcces.Rejetee;
            demande.CommentaireTraitement = commentaire.Trim();
            demande.DateTraitement = DateTime.Now;
            demande.TraiteParId = User.GetUserIdOrThrow();

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync(
                "REJET_DEMANDE_ACCES_AZURE_AD",
                "DemandeAccesAzureAd",
                demande.Id,
                null,
                new { demande.Email, Commentaire = demande.CommentaireTraitement });

            var body = $"""
                <p>Bonjour {demande.Prenoms} {demande.Nom},</p>
                <p>Votre demande d'accès à l'application Zéïnab n'a pas été approuvée.</p>
                <p>Commentaire administrateur : {demande.CommentaireTraitement}</p>
                <p>Cordialement,<br />DSI - Zéïnab</p>
                """;

            await _emailService.SendEmailAsync(
                demande.Email,
                "Accès refusé - Zéïnab",
                body,
                $"Bonjour {demande.Prenoms} {demande.Nom}, votre demande d'accès a été refusée. Commentaire : {demande.CommentaireTraitement}");

            TempData["Success"] = "Demande rejetée.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanManageAccessRequestsAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("DemandesAcces", "Index");
        }
    }
}

