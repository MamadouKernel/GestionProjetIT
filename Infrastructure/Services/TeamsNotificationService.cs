using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace GestionProjects.Infrastructure.Services
{
    public class TeamsNotificationService : ITeamsNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TeamsNotificationService> _logger;

        public TeamsNotificationService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<TeamsNotificationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task EnvoyerNouvelleDemandeAsync(string titreDemande, string demandeur, string direction, string directeurMetier, Guid demandeId)
        {
            var payload = ConstruireMessageCard(
                titre: "📋 Nouvelle demande de projet",
                sousTitre: titreDemande,
                couleur: "0076D7",
                faits: new Dictionary<string, string>
                {
                    ["Demandeur"] = demandeur,
                    ["Direction"] = direction,
                    ["Directeur métier"] = directeurMetier,
                    ["Statut"] = "En attente de validation Directeur métier"
                },
                labelAction: "Voir la demande"
            );
            await EnvoyerAsync(payload);
        }

        public async Task EnvoyerValidationDMAsync(string titreDemande, string directeurMetier, bool approuve, string? commentaire, Guid demandeId)
        {
            var payload = ConstruireMessageCard(
                titre: approuve ? "✅ Demande validée par le Directeur métier" : "🔁 Demande renvoyée par le Directeur métier",
                sousTitre: titreDemande,
                couleur: approuve ? "00B050" : "FF8C00",
                faits: new Dictionary<string, string>
                {
                    ["Directeur métier"] = directeurMetier,
                    ["Décision"] = approuve ? "Validé — en attente DSI" : "Correction demandée / Rejeté",
                    ["Commentaire"] = commentaire ?? "—"
                },
                labelAction: "Voir la demande"
            );
            await EnvoyerAsync(payload);
        }

        public async Task EnvoyerValidationDSIAsync(string titreDemande, string dsi, bool approuve, string? commentaire, Guid demandeId)
        {
            var payload = ConstruireMessageCard(
                titre: approuve ? "✅ Demande validée par le DSI — Projet créé" : "❌ Demande rejetée par le DSI",
                sousTitre: titreDemande,
                couleur: approuve ? "00B050" : "FF0000",
                faits: new Dictionary<string, string>
                {
                    ["DSI"] = dsi,
                    ["Décision"] = approuve ? "Validé — projet créé" : "Rejeté",
                    ["Commentaire"] = commentaire ?? "—"
                },
                labelAction: "Voir la demande"
            );
            await EnvoyerAsync(payload);
        }

        public async Task EnvoyerRejetOuRenvoiAsync(string titreDemande, string acteur, string action, string? commentaire, Guid demandeId)
        {
            var payload = ConstruireMessageCard(
                titre: $"🔔 {action}",
                sousTitre: titreDemande,
                couleur: "FF8C00",
                faits: new Dictionary<string, string>
                {
                    ["Action par"] = acteur,
                    ["Action"] = action,
                    ["Commentaire"] = commentaire ?? "—"
                },
                labelAction: "Voir la demande"
            );
            await EnvoyerAsync(payload);
        }

        public async Task EnvoyerChangementPhaseAsync(string titreProjet, string anciennePhase, string nouvellePhase, string modifiePar, Guid projetId)
        {
            var payload = ConstruireMessageCard(
                titre: "🔄 Changement de phase projet",
                sousTitre: titreProjet,
                couleur: "0076D7",
                faits: new Dictionary<string, string>
                {
                    ["Phase précédente"] = anciennePhase,
                    ["Nouvelle phase"] = nouvellePhase,
                    ["Modifié par"] = modifiePar
                },
                labelAction: "Voir le projet"
            );
            await EnvoyerAsync(payload);
        }

        public async Task EnvoyerDemandeClotureAsync(string titreProjet, string demandePar, Guid projetId)
        {
            var payload = ConstruireMessageCard(
                titre: "🏁 Demande de clôture projet",
                sousTitre: titreProjet,
                couleur: "7030A0",
                faits: new Dictionary<string, string>
                {
                    ["Demandé par"] = demandePar,
                    ["Statut"] = "En attente de validation (Demandeur → DM → DSI)"
                },
                labelAction: "Voir le projet"
            );
            await EnvoyerAsync(payload);
        }

        public async Task EnvoyerNotificationGeneraleAsync(string titre, string message, string couleur = "0076D7")
        {
            var payload = new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.4",
                            body = new object[]
                            {
                                new { type = "TextBlock", text = titre, weight = "Bolder", size = "Medium" },
                                new { type = "TextBlock", text = message, wrap = true }
                            }
                        }
                    }
                }
            };
            await EnvoyerAsync(payload);
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------

        private object ConstruireMessageCard(string titre, string sousTitre, string couleur, Dictionary<string, string> faits, string labelAction)
        {
            return new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.4",
                            body = new object[]
                            {
                                new
                                {
                                    type = "Container",
                                    style = "emphasis",
                                    items = new object[]
                                    {
                                        new { type = "TextBlock", text = titre, weight = "Bolder", size = "Medium", color = "Accent" },
                                        new { type = "TextBlock", text = sousTitre, wrap = true, spacing = "None" }
                                    }
                                },
                                new
                                {
                                    type = "FactSet",
                                    facts = faits.Select(f => new { title = f.Key, value = f.Value }).ToArray()
                                }
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    type = "Action.OpenUrl",
                                    title = labelAction,
                                    url = "about:blank"
                                }
                            },
                            schema = "http://adaptivecards.io/schemas/adaptive-card.json"
                        }
                    }
                }
            };
        }

        private async Task EnvoyerAsync(object payload)
        {
            try
            {
                var webhookUrl = await ObtenirWebhookUrlAsync();
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    _logger.LogDebug("Teams webhook non configuré — notification ignorée.");
                    return;
                }

                var client = _httpClientFactory.CreateClient("Teams");
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(webhookUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Teams webhook a retourné {Status}: {Body}", response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de la notification Teams");
            }
        }

        private async Task<string?> ObtenirWebhookUrlAsync()
        {
            var param = await _context.ParametresSysteme
                .FirstOrDefaultAsync(p => p.Cle == "TeamsWebhookUrl" && !p.EstSupprime);
            return param?.Valeur;
        }
    }
}
