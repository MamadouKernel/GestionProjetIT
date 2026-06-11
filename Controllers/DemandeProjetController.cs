using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestionProjects.Controllers
{
    [Authorize]
    public partial class DemandeProjetController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITeamsNotificationService _teams;
        private readonly IEmailService _email;
        private readonly IPermissionService _permissionService;

        public DemandeProjetController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IAuditService auditService,
            ICurrentUserService currentUserService,
            ITeamsNotificationService teams,
            IEmailService email,
            IPermissionService permissionService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _auditService = auditService;
            _currentUserService = currentUserService;
            _teams = teams;
            _email = email;
            _permissionService = permissionService;
        }

        private async Task<bool> IsActiveDsiDelegateAsync(Guid userId)
        {
            return await _db.DelegationsValidationDSI.AnyAsync(d =>
                d.DelegueId == userId &&
                d.EstActive &&
                d.DateDebut <= DateTime.Now &&
                d.DateFin >= DateTime.Now &&
                !d.EstSupprime);
        }

        private async Task<bool> HasAdminScopeAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("Admin", "Users");
        }

        private async Task<bool> CanManageDemandesBackofficeAsync()
        {
            return await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDSI");
        }

        private async Task<bool> CanHandleDsiValidationAsync(bool isDelegueActif)
        {
            var hasWorkflowAccess =
                await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDSI") ||
                await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ValiderDSI") ||
                await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "HistoriqueValidationsDSI");

            return hasWorkflowAccess || isDelegueActif;
        }

        private async Task<bool> CanAccessDemandeDetailsAsync(DemandeProjet demande, Guid userId)
        {
            if (await CanManageDemandesBackofficeAsync())
            {
                return true;
            }

            if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "ListeValidationDM") &&
                demande.DirecteurMetierId == userId)
            {
                return true;
            }

            if (await _permissionService.CurrentUserHasPermissionAsync("DemandeProjet", "Details") &&
                demande.DemandeurId == userId)
            {
                return true;
            }

            if (demande.Projet?.ChefProjetId == userId &&
                await _permissionService.CurrentUserHasPermissionAsync("Projet", "Details"))
            {
                return true;
            }

            return false;
        }

        // Méthode privée pour détecter les demandes similaires
        private async Task<List<DemandeSimilaireInfo>> DetecterDemandesSimilairesAsync(DemandeProjet demande)
        {
            var resultats = new List<DemandeSimilaireInfo>();

            if (string.IsNullOrWhiteSpace(demande.Titre))
                return resultats;

            // Normaliser le titre pour la comparaison (supprimer espaces, minuscules)
            var titreNormalise = NormaliserTexte(demande.Titre);

            // Rechercher les demandes avec un titre similaire (au moins 70% de similarité)
            var demandesExistantes = await _db.DemandesProjets
                .Include(d => d.Demandeur)
                .Include(d => d.Direction)
                .Include(d => d.Projet)
                    .ThenInclude(p => p != null ? p.ChefProjet : null)
                .Where(d => d.Id != demande.Id && !string.IsNullOrWhiteSpace(d.Titre))
                .ToListAsync();

            foreach (var demandeExistante in demandesExistantes)
            {
                var titreExistanteNormalise = NormaliserTexte(demandeExistante.Titre ?? string.Empty);
                
                // Calculer la similarité (simple comparaison de sous-chaînes)
                var similarite = CalculerSimilarite(titreNormalise, titreExistanteNormalise);
                
                if (similarite >= 0.7) // 70% de similarité minimum
                {
                    // Vérifier si un projet existe pour cette demande
                    var projetExistant = await _db.Projets
                        .Include(p => p.ChefProjet)
                        .FirstOrDefaultAsync(p => p.DemandeProjetId == demandeExistante.Id);

                    var info = new DemandeSimilaireInfo
                    {
                        DemandeId = demandeExistante.Id,
                        Titre = demandeExistante.Titre ?? string.Empty,
                        StatutDemande = demandeExistante.StatutDemande,
                        DateSoumission = demandeExistante.DateSoumission,
                        Demandeur = $"{demandeExistante.Demandeur?.Nom} {demandeExistante.Demandeur?.Prenoms}",
                        Direction = demandeExistante.Direction?.Libelle ?? "N/A",
                        CommentaireRejet = GetCommentaireRejet(demandeExistante),
                        ProjetExistant = projetExistant != null ? new ProjetExistantInfo
                        {
                            ProjetId = projetExistant.Id,
                            CodeProjet = projetExistant.CodeProjet,
                            Titre = projetExistant.Titre,
                            StatutProjet = projetExistant.StatutProjet,
                            PhaseActuelle = projetExistant.PhaseActuelle,
                            ChefProjet = projetExistant.ChefProjet != null 
                                ? $"{projetExistant.ChefProjet.Nom} {projetExistant.ChefProjet.Prenoms}" 
                                : "Non assigné"
                        } : null,
                        Similarite = similarite
                    };

                    resultats.Add(info);
                }
            }

            return resultats.OrderByDescending(r => r.Similarite).ToList();
        }

        private string NormaliserTexte(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte))
                return string.Empty;

            return texte.ToLowerInvariant()
                .Trim()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "");
        }

        private double CalculerSimilarite(string texte1, string texte2)
        {
            if (string.IsNullOrEmpty(texte1) || string.IsNullOrEmpty(texte2))
                return 0.0;

            // Algorithme simple de similarité basé sur la longueur de la sous-chaîne commune
            var longueurMin = Math.Min(texte1.Length, texte2.Length);
            var longueurMax = Math.Max(texte1.Length, texte2.Length);

            if (longueurMin == 0)
                return 0.0;

            // Compter les caractères communs
            var caracteresCommuns = 0;
            var minLength = Math.Min(texte1.Length, texte2.Length);
            
            for (int i = 0; i < minLength; i++)
            {
                if (i < texte1.Length && i < texte2.Length && texte1[i] == texte2[i])
                    caracteresCommuns++;
            }

            // Vérifier aussi si l'un contient l'autre
            if (texte1.Contains(texte2) || texte2.Contains(texte1))
                return 0.9;

            return (double)caracteresCommuns / longueurMax;
        }

        private string? GetCommentaireRejet(DemandeProjet demande)
        {
            if (demande.StatutDemande == StatutDemande.RejeteeParDirecteurMetier)
                return demande.CommentaireDirecteurMetier;
            
            if (demande.StatutDemande == StatutDemande.RejeteeParDSI)
                return demande.CommentaireDSI;

            if (demande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
                return demande.CommentaireDirecteurMetier;

            if (demande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                return demande.CommentaireDSI;

            return null;
        }

        // Classe pour stocker les informations sur les demandes similaires
        private class DemandeSimilaireInfo
        {
            public Guid DemandeId { get; set; }
            public string Titre { get; set; } = string.Empty;
            public StatutDemande StatutDemande { get; set; }
            public DateTime DateSoumission { get; set; }
            public string Demandeur { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public string? CommentaireRejet { get; set; }
            public ProjetExistantInfo? ProjetExistant { get; set; }
            public double Similarite { get; set; }
        }

        private class ProjetExistantInfo
        {
            public Guid ProjetId { get; set; }
            public string CodeProjet { get; set; } = string.Empty;
            public string Titre { get; set; } = string.Empty;
            public StatutProjet StatutProjet { get; set; }
            public PhaseProjet PhaseActuelle { get; set; }
            public string ChefProjet { get; set; } = string.Empty;
        }
    }
}
