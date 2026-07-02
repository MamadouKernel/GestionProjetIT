using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using GestionProjects.Web.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GestionProjects.Controllers
{
    [Authorize]
    public partial class ProjetController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IAuditService _auditService;
        private readonly IPdfService _pdfService;
        private readonly IExcelService _excelService;
        private readonly IWordService _wordService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly ILivrableValidationService _livrableValidationService;
        private readonly IRAGCalculationService _ragCalculationService;
        private readonly ICacheService _cacheService;
        private readonly IUatValidationService _uatValidation;
        private readonly ICollaborationProjetService _collaboration;
        private readonly IElectronicSignatureService _electronicSignature;
        private readonly IProjetQueryService _projetQuery;
        private readonly IClotureProjetWorkflowService _clotureWorkflow;
        private readonly ICharteProjetWorkflowService _charteWorkflow;
        private readonly IUatProjetWorkflowService _uatWorkflow;
        private readonly IChargeProjetService _chargeProjetService;
        private readonly IProjetProgressService _projetProgress;
        private readonly IPlanificationNativeService _planificationNative;

        public ProjetController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IAuditService auditService,
            IPdfService pdfService,
            IExcelService excelService,
            IWordService wordService,
            ICurrentUserService currentUserService,
            IPermissionService permissionService,
            INotificationService notificationService,
            ILivrableValidationService livrableValidationService,
            IRAGCalculationService ragCalculationService,
            ICacheService cacheService,
            IUatValidationService uatValidation,
            ICollaborationProjetService collaboration,
            IElectronicSignatureService electronicSignature,
            IProjetQueryService projetQuery,
            IClotureProjetWorkflowService clotureWorkflow,
            ICharteProjetWorkflowService charteWorkflow,
            IUatProjetWorkflowService uatWorkflow,
            IChargeProjetService chargeProjetService,
            IProjetProgressService projetProgress,
            IPlanificationNativeService planificationNative)
        {
            _db = db;
            _fileStorage = fileStorage;
            _auditService = auditService;
            _pdfService = pdfService;
            _excelService = excelService;
            _wordService = wordService;
            _currentUserService = currentUserService;
            _permissionService = permissionService;
            _notificationService = notificationService;
            _livrableValidationService = livrableValidationService;
            _ragCalculationService = ragCalculationService;
            _cacheService = cacheService;
            _uatValidation = uatValidation;
            _collaboration = collaboration;
            _electronicSignature = electronicSignature;
            _projetQuery = projetQuery;
            _clotureWorkflow = clotureWorkflow;
            _charteWorkflow = charteWorkflow;
            _uatWorkflow = uatWorkflow;
            _chargeProjetService = chargeProjetService;
            _projetProgress = projetProgress;
            _planificationNative = planificationNative;
        }
        private Task<bool> CurrentUserHasPermissionAsync(string controleur, string action)
            => _permissionService.CurrentUserHasPermissionAsync(controleur, action);

        /// <summary>
        /// Délègue à IProjetQueryService. Conservé pour compatibilité interne — à migrer progressivement.
        /// </summary>
        private Task<Guid?> GetCurrentUserDirectionIdAsync(Guid userId)
            => _projetQuery.GetUserDirectionIdAsync(userId);

        private async Task<bool> HasAdminScopeAsync()
        {
            return await CurrentUserHasPermissionAsync("Admin", "Users");
        }

        private async Task<bool> HasPortfolioGovernanceAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "Portefeuille");
        }

        private async Task<bool> HasChefProjetWorkflowAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "UpdateProgress") ||
                   await CurrentUserHasPermissionAsync("Projet", "ValiderAnalyse") ||
                   await CurrentUserHasPermissionAsync("Projet", "EditPlanification");
        }

        private async Task<bool> HasDmWorkflowAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "ValidationsProjet") ||
                   await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM");
        }

        private async Task<bool> HasDemandeurProjectAccessAsync()
        {
            return await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDemandeur");
        }

        private async Task<ProjetUiPermissions> BuildProjectUiAsync(Projet projet, bool isReadOnly = false)
        {
            var userId = User.GetUserIdOrThrow();
            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            var isDemandeurProject = projet.DemandeProjet?.DemandeurId == userId;

            return await ProjetUiPermissionBuilder.BuildAsync(
                _permissionService,
                User,
                projet,
                isReadOnly: isReadOnly,
                isDemandeurProject: isDemandeurProject,
                currentUserDirectionId: currentUserDirectionId);
        }

        private async Task<bool> CanManageProjectAsChefProjetOrAdminAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanActAsChefProjet || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanViewProjectAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanViewProject;
        }

        private async Task<bool> CanManageAnalyseAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanEditFicheProjetAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCharte || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanManagePlanificationAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditPlanification;
        }

        private async Task<bool> CanManageExecutionAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditExecution;
        }

        private async Task<bool> CanManageUatAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditUat;
        }

        private async Task<bool> CanManageClotureAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCloture;
        }

        private async Task<bool> CanManageCollaborationAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditCollaboration;
        }

        private async Task<bool> CanUpdateProjectProgressAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanUpdateProgress || ui.CanForceStatus;
        }

        private async Task<bool> CanManageProjectMembersAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditAnalyse || ui.HasDsiGovernanceAccess;
        }

        private async Task<bool> CanEditTechnicalCommentAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanEditTechnicalComment;
        }

        private async Task<bool> CanValidateClotureDmAsync(DemandeClotureProjet demande, Guid userId)
        {
            if (!await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDM"))
            {
                return false;
            }

            if (demande.Projet.SponsorId == userId)
            {
                return true;
            }

            if (await _permissionService.IsActiveDmDelegateAsync(demande.Projet.SponsorId, userId))
            {
                return true;
            }

            var currentUserDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            return currentUserDirectionId.HasValue && demande.Projet.DirectionId == currentUserDirectionId.Value;
        }

        private async Task<bool> CanValidateClotureDsiAsync(Guid userId)
        {
            return await CurrentUserHasPermissionAsync("Projet", "ListeValidationClotureDSI") &&
                   await CanValidateCharteAsDsiAsync(userId);
        }

        private static byte[] BuildCsvBytes(IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(';', row.Select(EscapeCsv)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        }

        // Helper method: Vérifie si un utilisateur peut agir comme chef de projet
        private async Task<bool> CanActAsChefProjetAsync(Projet projet)
        {
            var ui = await BuildProjectUiAsync(projet);
            return ui.CanActAsChefProjet || ui.HasDsiGovernanceAccess;
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

        private async Task<bool> CanValidateCharteAsDirecteurMetierAsync(Projet projet, Guid userId)
        {
            if (!await CurrentUserHasPermissionAsync("Projet", "ValiderCharteDM"))
            {
                return false;
            }

            if (projet.SponsorId == userId)
            {
                return true;
            }

            if (await _permissionService.IsActiveDmDelegateAsync(projet.SponsorId, userId))
            {
                return true;
            }

            var userDirectionId = await GetCurrentUserDirectionIdAsync(userId);
            return userDirectionId.HasValue && projet.DirectionId == userDirectionId.Value;
        }

        private async Task<bool> CanValidateCharteAsDsiAsync(Guid userId)
        {
            if (await CurrentUserHasPermissionAsync("Projet", "ValiderCharteDSI"))
            {
                return true;
            }

            return await IsActiveDsiDelegateAsync(userId);
        }

        private static bool AreRequiredCharteSignaturesCompleted(DossierSignatureProjet dossier)
        {
            var requiredSignataires = dossier.Signataires
                .Where(s => s.Role == RoleSignataireProjet.Sponsor || s.Role == RoleSignataireProjet.ChefDeProjet)
                .ToList();

            return requiredSignataires.Count == 2 &&
                   requiredSignataires.All(s => s.Statut == StatutSignataireDossierSignature.Signe);
        }

        private static void ResetCharteValidationState(Projet projet)
        {
            projet.CharteValideeParDM = false;
            projet.DateCharteValideeParDM = null;
            projet.CharteValideeParDMId = null;
            projet.CommentaireRefusCharteDM = null;

            projet.CharteValideeParDSI = false;
            projet.DateCharteValideeParDSI = null;
            projet.CharteValideeParDSIId = null;
            projet.CommentaireRefusCharteDSI = null;

            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
        }

        private static List<string> BuildAnalyseBlockingItems(Projet projet, LivrableValidationResult validationLivrables)
        {
            var blocages = new List<string>();

            foreach (var livrable in validationLivrables.LivrablesManquants.Distinct())
            {
                blocages.Add($"Livrable manquant : {GetLivrableDisplayName(livrable)}");
            }

            var hasSignedLivrable = projet.Livrables.Any(l =>
                !l.EstSupprime &&
                l.TypeLivrable == TypeLivrable.CharteProjetSignee);

            if (!hasSignedLivrable && validationLivrables.LivrablesManquants.All(l => l != TypeLivrable.CharteProjetSignee))
            {
                blocages.Add("Livrable manquant : Charte projet signée");
            }

            if (projet.CharteProjet?.SignatureSponsor != true)
            {
                blocages.Add("Signature manquante : Sponsor / Directeur Métier sur la charte signée");
            }

            if (projet.CharteProjet?.SignatureChefProjet != true)
            {
                blocages.Add("Signature manquante : Chef de Projet sur la charte signée");
            }

            if (!projet.CharteValideeParDM)
            {
                blocages.Add("Validation manquante : Directeur Métier");
            }

            if (!projet.CharteValideeParDSI)
            {
                blocages.Add("Validation manquante : DSI / RSIT délégué");
            }

            return blocages;
        }

        private static string BuildAnalyseBlockingAlertHtml(IEnumerable<string> blocages)
        {
            var items = string.Join(string.Empty, blocages.Select(item => $"<li>{System.Net.WebUtility.HtmlEncode(item)}</li>"));
            return "Blocage automatique : impossible de passer en phase Planification &amp; Validation tant que les éléments suivants ne sont pas complétés :" +
                   $"<ul class=\"mb-0 mt-2\">{items}</ul>";
        }

        private static string GetLivrableDisplayName(TypeLivrable type)
        {
            return type switch
            {
                TypeLivrable.CahierCharges => "Cahier des charges",
                TypeLivrable.CahierAnalyseTechnique => "Cahier d'analyse technique",
                TypeLivrable.CharteProjet => "Charte projet",
                TypeLivrable.CharteProjetSignee => "Charte projet signée",
                TypeLivrable.NoteCadrage => "Note de cadrage",
                TypeLivrable.Wbs => "WBS",
                TypeLivrable.PlanningDetaille => "Planning détaillé",
                TypeLivrable.MatriceRaci => "Matrice RACI",
                TypeLivrable.SchemaCommunication => "Schéma de communication",
                TypeLivrable.BudgetPrevisionnel => "Budget prévisionnel",
                TypeLivrable.PvKickOff => "PV de kick-off",
                TypeLivrable.CahierTests => "Cahier de tests",
                TypeLivrable.FeuilleAnomalies => "Feuille d'anomalies",
                TypeLivrable.PvRecette => "PV de recette",
                TypeLivrable.RapportHypercare => "Rapport hypercare",
                TypeLivrable.DossierMep => "Dossier MEP",
                TypeLivrable.PvMep => "PV MEP",
                TypeLivrable.RapportCloture => "Rapport de clôture",
                TypeLivrable.PvCloture => "PV de clôture",
                TypeLivrable.DossierExploitation => "Dossier d'exploitation",
                TypeLivrable.CompteRenduReunion => "Compte-rendu de réunion",
                _ => type.ToString()
            };
        }

        private static void NormalizeCharteProjetForPersistence(CharteProjet charte)
        {
            charte.NomProjet = NormalizeRequiredText(charte.NomProjet);
            charte.NumeroProjet = NormalizeOptionalText(charte.NumeroProjet);
            charte.ObjectifProjet = NormalizeRequiredText(charte.ObjectifProjet);
            charte.AssuranceQualite = NormalizeRequiredText(charte.AssuranceQualite);
            charte.Perimetre = NormalizeRequiredText(charte.Perimetre);
            charte.ContraintesInitiales = NormalizeRequiredText(charte.ContraintesInitiales);
            charte.RisquesInitiaux = NormalizeRequiredText(charte.RisquesInitiaux);
            charte.Sponsors = NormalizeRequiredText(charte.Sponsors);
            charte.EmailChefProjet = NormalizeOptionalText(charte.EmailChefProjet);
            charte.CodeDocument = NormalizeRequiredText(charte.CodeDocument);
            charte.TypeDocument = string.IsNullOrWhiteSpace(charte.TypeDocument) ? "Charte de projet" : charte.TypeDocument.Trim();
            charte.Departement = string.IsNullOrWhiteSpace(charte.Departement) ? "SYSTEME D'INFORMATION" : charte.Departement.Trim();
            charte.DescriptionRevision = NormalizeOptionalText(charte.DescriptionRevision);
            charte.RedigePar = NormalizeOptionalText(charte.RedigePar);
            charte.VerifiePar = NormalizeOptionalText(charte.VerifiePar);
            charte.ApprouvePar = NormalizeOptionalText(charte.ApprouvePar);
        }

        private static string NormalizeRequiredText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
