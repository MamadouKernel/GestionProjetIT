using GestionProjects.Application.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GestionProjects.Controllers
{
    public partial class HomeController
    {
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Route("Home/Error/{statusCode?}")]
        public IActionResult Error(int? statusCode = null)
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var requestId = HttpContext.TraceIdentifier;
            var itemStatusCode = HttpContext.Items.TryGetValue("ErrorStatusCode", out var errorStatusCodeRaw) &&
                                 errorStatusCodeRaw is int rawStatusCode
                ? rawStatusCode
                : (int?)null;

            var resolvedStatusCode =
                statusCode ??
                itemStatusCode ??
                HttpContext.Response?.StatusCode ??
                500;

            var detail = HttpContext.Items["ErrorDetail"] as string;
            var originalPath =
                HttpContext.Items["ErrorOriginalPath"] as string ??
                (reExecuteFeature == null
                    ? exceptionFeature?.Path
                    : $"{reExecuteFeature.OriginalPathBase}{reExecuteFeature.OriginalPath}{reExecuteFeature.OriginalQueryString}");

            var model = BuildErrorViewModel(resolvedStatusCode, requestId, originalPath, detail);
            Response.StatusCode = resolvedStatusCode;
            return View(model);
        }

        private ErrorViewModel BuildErrorViewModel(int statusCode, string requestId, string? originalPath, string? detail)
        {
            var defaultHomeUrl = User?.Identity?.IsAuthenticated == true
                ? (Url.Action("Index", "Home") ?? "/")
                : (Url.Action("Login", "Account") ?? "/");

            var model = new ErrorViewModel
            {
                RequestId = requestId,
                StatusCode = statusCode,
                OriginalPath = originalPath,
                Detail = detail,
                ShowTechnicalDetails = !string.IsNullOrWhiteSpace(detail),
                PrimaryActionUrl = defaultHomeUrl
            };

            switch (statusCode)
            {
                case 400:
                    model.BadgeLabel = "Requête invalide";
                    model.Title = "La requête n'est pas exploitable";
                    model.Heading = "Code 400";
                    model.Description = "Les données envoyées sont incomplètes, incohérentes ou ne respectent pas le format attendu.";
                    model.IconClass = "bi-slash-circle-fill";
                    model.AccentClass = "is-warning";
                    model.PrimaryActionText = "Revenir au formulaire";
                    break;

                case 401:
                    model.BadgeLabel = "Authentification requise";
                    model.Title = "Votre session n'est plus valide";
                    model.Heading = "Code 401";
                    model.Description = "Vous devez vous connecter pour accéder à cette ressource ou reprendre votre session.";
                    model.IconClass = "bi-box-arrow-in-right";
                    model.AccentClass = "is-info";
                    model.PrimaryActionText = "Se connecter";
                    model.PrimaryActionUrl = Url.Action("Login", "Account") ?? "/";
                    break;

                case 403:
                    model.BadgeLabel = "Accès refusé";
                    model.Title = "Cette action n'est pas ouverte pour votre profil";
                    model.Heading = "Code 403";
                    model.Description = "Votre compte est bien reconnu, mais les permissions actives ne permettent pas d'accéder à cette page ou de lancer cette action.";
                    model.IconClass = "bi-shield-lock-fill";
                    model.AccentClass = "is-warning";
                    model.PrimaryActionText = "Retour au tableau de bord";
                    model.PrimaryActionUrl = Url.Action("Index", "Home") ?? "/";
                    break;

                case 404:
                    model.BadgeLabel = "Page introuvable";
                    model.Title = "La ressource demandée n'existe pas";
                    model.Heading = "Code 404";
                    model.Description = "Le lien est incorrect, la page a été déplacée ou l'élément demandé n'existe plus dans l'application.";
                    model.IconClass = "bi-search";
                    model.AccentClass = "is-neutral";
                    model.PrimaryActionText = "Retour à l'accueil";
                    model.PrimaryActionUrl = defaultHomeUrl;
                    break;

                case 405:
                    model.BadgeLabel = "Méthode non autorisée";
                    model.Title = "Le mode d'appel n'est pas accepté";
                    model.Heading = "Code 405";
                    model.Description = "La ressource existe, mais elle n'accepte pas cette méthode d'appel. Cela arrive souvent après un lien direct vers une action prévue pour un formulaire.";
                    model.IconClass = "bi-arrow-repeat";
                    model.AccentClass = "is-info";
                    break;

                case 408:
                    model.BadgeLabel = "Temps dépassé";
                    model.Title = "La requête a expiré";
                    model.Heading = "Code 408";
                    model.Description = "Le traitement a pris trop de temps ou la session réseau a été interrompue avant la fin de l'opération.";
                    model.IconClass = "bi-clock-history";
                    model.AccentClass = "is-warning";
                    model.PrimaryActionText = "Réessayer";
                    break;

                case 409:
                    model.BadgeLabel = "Conflit métier";
                    model.Title = "L'opération entre en conflit avec l'état actuel";
                    model.Heading = "Code 409";
                    model.Description = "Une autre action, une validation déjà effectuée ou un état incompatible empêche la poursuite du traitement.";
                    model.IconClass = "bi-diagram-3-fill";
                    model.AccentClass = "is-warning";
                    break;

                case 429:
                    model.BadgeLabel = "Trop de requêtes";
                    model.Title = "Le rythme d'utilisation est temporairement limité";
                    model.Heading = "Code 429";
                    model.Description = "Plusieurs tentatives ont été détectées dans un intervalle court. Attendez quelques instants avant de recommencer.";
                    model.IconClass = "bi-speedometer2";
                    model.AccentClass = "is-info";
                    model.PrimaryActionText = "Réessayer plus tard";
                    break;

                default:
                    model.BadgeLabel = "Erreur interne";
                    model.Title = "Une erreur inattendue a interrompu le traitement";
                    model.Heading = $"Code {statusCode}";
                    model.Description = "Le système n'a pas pu terminer la demande. Le support pourra exploiter l'identifiant technique ci-dessous si le problème persiste.";
                    model.IconClass = "bi-exclamation-octagon-fill";
                    model.AccentClass = "is-danger";
                    model.PrimaryActionText = "Retour au tableau de bord";
                    model.PrimaryActionUrl = Url.Action("Index", "Home") ?? "/";
                    break;
            }

            return model;
        }
    }
}
