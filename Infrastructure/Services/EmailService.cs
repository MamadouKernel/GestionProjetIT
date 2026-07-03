using GestionProjects.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace GestionProjects.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public Task EnvoyerAsync(string destinataire, string sujet, string corpsHtml)
            => EnvoyerAsync(new[] { destinataire }, sujet, corpsHtml);

        public async Task EnvoyerAsync(IEnumerable<string> destinataires, string sujet, string corpsHtml)
        {
            var section = _config.GetSection("SmtpSettings");
            var host = section["Host"];
            var from = section["From"] ?? "no-reply@cit.ci";
            var enabled = bool.TryParse(section["Enabled"], out var smtpEnabled) && smtpEnabled;

            if (!enabled || string.IsNullOrWhiteSpace(host))
            {
                _logger.LogDebug("SMTP désactivé ou non configuré. Sujet ignoré : {Sujet}", sujet);
                return;
            }

            var validRecipients = destinataires
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (validRecipients.Count == 0)
            {
                _logger.LogWarning("Aucun destinataire valide pour le mail : {Sujet}", sujet);
                return;
            }

            try
            {
                using var client = CreateSmtpClient(section);
                using var mail = new MailMessage
                {
                    From = new MailAddress(from, $"{DocumentBrandingHelper.ApplicationName} - CIT"),
                    Subject = sujet,
                    Body = EnsureWrappedHtml(sujet, corpsHtml),
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };

                foreach (var recipient in validRecipients)
                {
                    mail.To.Add(recipient);
                }

                await client.SendMailAsync(mail);
                _logger.LogInformation("Email envoyé. Sujet : {Sujet}. Destinataires : {Destinataires}", sujet, string.Join(", ", validRecipients));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email. Sujet : {Sujet}", sujet);
            }
        }

        public async Task EnvoyerPdfCharteAsync(string emailDM, string emailDSI, string titreDemande, string nomChefProjet, byte[] pdfBytes, string nomFichier)
        {
            var section = _config.GetSection("SmtpSettings");
            var host = section["Host"];
            var from = section["From"] ?? "no-reply@cit.ci";
            var enabled = bool.TryParse(section["Enabled"], out var smtpEnabled) && smtpEnabled;

            if (!enabled || string.IsNullOrWhiteSpace(host))
            {
                return;
            }

            var destinataires = new[] { emailDM, emailDSI }
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (destinataires.Count == 0)
            {
                return;
            }

            try
            {
                using var client = CreateSmtpClient(section);
                using var mail = new MailMessage
                {
                    From = new MailAddress(from, $"{DocumentBrandingHelper.ApplicationName} - CIT"),
                    Subject = $"[Action requise] Charte projet à signer - {titreDemande}",
                    Body = CorpsHtml(
                        $"Charte projet prête pour signature - <strong>{titreDemande}</strong>",
                        $"<p>Le Chef de projet <strong>{nomChefProjet}</strong> a généré la charte pour le projet <strong>{titreDemande}</strong>.</p>" +
                        "<p>Vous trouverez le document en pièce jointe. Connectez-vous à l'application pour finaliser la signature et poursuivre le workflow projet.</p>",
                        "Charte projet",
                        "Ouvrir Zéïnab"),
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };

                foreach (var dest in destinataires)
                {
                    mail.To.Add(dest);
                }

                using var ms = new MemoryStream(pdfBytes);
                mail.Attachments.Add(new Attachment(ms, nomFichier, "application/pdf"));
                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi du PDF de charte par email");
            }
        }

        public Task EnvoyerNouvelleDemandeAuDMAsync(string emailDM, string nomDM, string titreDemande, string nomDemandeur, string direction)
            => EnvoyerAsync(
                emailDM,
                $"[Action requise] Nouvelle demande de projet - {titreDemande}",
                CorpsHtml(
                    $"Nouvelle demande de projet - <strong>{titreDemande}</strong>",
                    $"<p>Bonjour <strong>{nomDM}</strong>,</p>" +
                    $"<p>Une nouvelle demande de projet a été soumise par <strong>{nomDemandeur}</strong> pour la direction <strong>{direction}</strong>.</p>" +
                    "<p>Merci de vous connecter à Zéïnab pour consulter le dossier, l'analyser et rendre votre décision.</p>",
                    "Demande à valider",
                    "Traiter la demande"));

        public Task EnvoyerValidationDMVersdsIAsync(string emailDSI, string titreDemande, string nomDM, string? commentaire)
            => EnvoyerAsync(
                emailDSI,
                $"[Action requise] Validation DM reçue - attente DSI - {titreDemande}",
                CorpsHtml(
                    $"Validation Directeur Métier reçue - <strong>{titreDemande}</strong>",
                    $"<p>La demande <strong>{titreDemande}</strong> a été validée par le Directeur Métier <strong>{nomDM}</strong>.</p>" +
                    $"{FormatOptionalParagraph("Commentaire", commentaire)}" +
                    "<p>Merci de vous connecter à Zéïnab pour poursuivre la validation côté DSI.</p>",
                    "Validation DSI",
                    "Ouvrir la validation"));

        public Task EnvoyerRejetOuRenvoiAuDemandeurAsync(string emailDemandeur, string nomDemandeur, string titreDemande, string acteur, string action, string? commentaire)
            => EnvoyerAsync(
                emailDemandeur,
                $"[Information] {action} - {titreDemande}",
                CorpsHtml(
                    $"{action} - <strong>{titreDemande}</strong>",
                    $"<p>Bonjour <strong>{nomDemandeur}</strong>,</p>" +
                    $"<p>Votre demande <strong>{titreDemande}</strong> a fait l'objet de l'action suivante par <strong>{acteur}</strong> : <strong>{action}</strong>.</p>" +
                    $"{FormatOptionalParagraph("Motif ou commentaire", commentaire)}" +
                    "<p>Connectez-vous à Zéïnab pour consulter le détail et, si nécessaire, compléter le dossier.</p>",
                    "Mise à jour du dossier",
                    "Voir la demande"));

        public Task EnvoyerRejetDSIAsync(string emailDemandeur, string? emailDM, string titreDemande, string nomDSI, string? commentaire)
        {
            var destinataires = new[] { emailDemandeur, emailDM }
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return EnvoyerAsync(
                destinataires,
                $"[Information] Demande rejetée par la DSI - {titreDemande}",
                CorpsHtml(
                    $"Demande rejetée par la DSI - <strong>{titreDemande}</strong>",
                    $"<p>La demande <strong>{titreDemande}</strong> a été rejetée par <strong>{nomDSI}</strong>.</p>" +
                    $"{FormatOptionalParagraph("Motif", commentaire)}" +
                    "<p>Merci de vous connecter à Zéïnab pour prendre connaissance du détail.</p>",
                    "Décision DSI",
                    "Consulter le dossier"));
        }

        public Task EnvoyerValidationDSIProjetCreeAsync(string emailDemandeur, string? emailDM, string titreDemande, string codeProjet)
        {
            var destinataires = new[] { emailDemandeur, emailDM }
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return EnvoyerAsync(
                destinataires,
                $"[Information] Demande validée DSI - projet {codeProjet} créé - {titreDemande}",
                CorpsHtml(
                    $"Demande validée - projet <strong>{codeProjet}</strong> créé",
                    $"<p>La demande <strong>{titreDemande}</strong> a été validée par la DSI.</p>" +
                    $"<p>Le projet <strong>{codeProjet}</strong> a été créé automatiquement dans le portefeuille.</p>" +
                    "<p>Connectez-vous à Zéïnab pour suivre l'avancement et démarrer le pilotage projet.</p>",
                    "Projet créé",
                    "Ouvrir le projet"));
        }

        public Task EnvoyerDemandeAccesAsync(string emailAdmin, string nomDemandeur, string emailDemandeur, string rolesSouhaites)
            => EnvoyerAsync(
                emailAdmin,
                $"[Action requise] Demande d'accès - {nomDemandeur}",
                CorpsHtml(
                    $"Nouvelle demande d'accès - <strong>{nomDemandeur}</strong>",
                    $"<p>L'utilisateur <strong>{nomDemandeur}</strong> ({emailDemandeur}) a demandé un accès à l'application.</p>" +
                    $"<p>Rôles souhaités : <strong>{rolesSouhaites}</strong></p>" +
                    "<p>Connectez-vous à Zéïnab pour vérifier la demande et créer le compte si elle est approuvée.</p>",
                    "Accès à traiter",
                    "Ouvrir l'administration"));

        public Task EnvoyerDemandeAccesAuDmAsync(string emailDM, string nomDM, string nomDemandeur, string emailDemandeur, string direction, string roleSouhaite)
            => EnvoyerAsync(
                emailDM,
                $"[Action requise] Validation d'accès dans votre direction - {nomDemandeur}",
                CorpsHtml(
                    $"Validation d'accès à effectuer - <strong>{nomDemandeur}</strong>",
                    $"<p>Bonjour <strong>{nomDM}</strong>,</p>" +
                    $"<p><strong>{nomDemandeur}</strong> ({emailDemandeur}) a soumis une demande d'accès à {DocumentBrandingHelper.ApplicationName} " +
                    $"en se rattachant à la direction <strong>{direction}</strong> dont vous êtes Directeur Métier.</p>" +
                    $"<p>Rôle souhaité : <strong>{roleSouhaite}</strong></p>" +
                    "<p>Votre validation est requise comme premier rang du workflow : confirmez le rattachement et le rôle, " +
                    "ou refusez si la demande est incorrecte. La création du compte par l'AdminIT/DSI/RSIT n'aura lieu qu'après votre validation.</p>",
                    "Validation accès - DM",
                    "Valider la demande"));

        public Task EnvoyerDemandeCreationCompteAuDMAsync(string emailDM, string nomDM, string nomComplet, string direction, string service, string emailDemandeur)
            => EnvoyerAsync(
                emailDM,
                $"[Action requise] Demande de création de compte - {nomComplet}",
                CorpsHtml(
                    $"Demande de création de compte - <strong>{nomComplet}</strong>",
                    $"<p>Bonjour <strong>{nomDM}</strong>,</p>" +
                    $"<p><strong>{nomComplet}</strong> ({emailDemandeur}) souhaite créer un compte dans l'application {DocumentBrandingHelper.ApplicationName}.</p>" +
                    $"<p>Direction : <strong>{direction}</strong><br/>Service : <strong>{service}</strong></p>" +
                    "<p>Merci de vous connecter à Zéïnab pour valider ou refuser cette demande.</p>",
                    "Création de compte",
                    "Traiter la demande"));

        public Task EnvoyerDemandeCreationCompteAuDSIAsync(string emailDSI, string nomComplet, string nomDM, string direction, string service)
            => EnvoyerAsync(
                emailDSI,
                $"[Action requise] Compte validé par le DM - {nomComplet}",
                CorpsHtml(
                    $"Compte validé par le Directeur Métier - <strong>{nomComplet}</strong>",
                    $"<p>La demande de création de compte de <strong>{nomComplet}</strong> a été validée par le Directeur Métier <strong>{nomDM}</strong>.</p>" +
                    $"<p>Direction : <strong>{direction}</strong><br/>Service : <strong>{service}</strong></p>" +
                    "<p>Merci de vous connecter à Zéïnab pour créer le compte et finaliser les accès.</p>",
                    "Action DSI",
                    "Ouvrir le dossier"));

        public Task EnvoyerActivationCompteAsync(string email, string nomComplet, string username, string lienActivation, DateTime dateExpiration)
        {
            // Défense en profondeur : si SmtpSettings:BaseUrl n'est pas configuré, le lien
            // arrive en relatif (/Account/...) et le client mail n'a aucun moyen de le résoudre.
            // On le signale au log pour éviter d'envoyer un email sans lien cliquable en silence.
            if (!Uri.TryCreate(lienActivation, UriKind.Absolute, out _))
            {
                _logger.LogWarning(
                    "Lien d'activation relatif envoyé à {Email} : configurez SmtpSettings:BaseUrl pour générer un lien absolu cliquable.",
                    email);
            }

            // On affiche aussi le lien en clair (texte) sous le bouton stylisé : certains
            // clients (Outlook strict, anti-phishing, dark mode) masquent les <a> stylisés ;
            // l'URL textuelle est toujours visible et copiable.
            return EnvoyerAsync(
                email,
                $"Activez votre accès à {DocumentBrandingHelper.ApplicationName}",
                CorpsHtml(
                    $"Bienvenue sur {DocumentBrandingHelper.ApplicationName} - <strong>{nomComplet}</strong>",
                    $"<p>Bonjour <strong>{nomComplet}</strong>,</p>" +
                    "<p>Votre compte a été créé avec succès. Pour finaliser l'activation, définissez votre mot de passe via le lien sécurisé ci-dessous.</p>" +
                    "<div style=\"background:#0f172a;border-radius:20px;padding:20px 22px;margin:20px 0;color:#e2e8f0;\">" +
                    $"<p style=\"margin:0 0 10px 0;\"><strong>Identifiant</strong><br/><span style=\"font-size:18px;color:#ffffff;\">{username}</span></p>" +
                    $"<p style=\"margin:0;\"><strong>Expiration du lien</strong><br/><span style=\"font-size:18px;color:#ffffff;\">{dateExpiration:dd/MM/yyyy HH:mm}</span></p>" +
                    "</div>" +
                    "<p style=\"margin-top:18px;font-size:13px;color:#475569;\">Si le bouton ne s'affiche pas, copiez-collez ce lien dans votre navigateur :<br/>" +
                    $"<a href=\"{lienActivation}\" style=\"color:#0f4c81;word-break:break-all;\">{lienActivation}</a></p>" +
                    "<p>Si vous n'êtes pas à l'origine de cette demande, ignorez ce message et contactez la DSI.</p>",
                    "Accès utilisateur",
                    "Définir mon mot de passe",
                    lienActivation));
        }

        public Task EnvoyerReinitialisationMotDePasseAsync(string email, string nomComplet, string username, string lienReinitialisation, DateTime dateExpiration)
        {
            if (!Uri.TryCreate(lienReinitialisation, UriKind.Absolute, out _))
            {
                _logger.LogWarning(
                    "Lien de réinitialisation relatif envoyé à {Email} : configurez SmtpSettings:BaseUrl pour générer un lien absolu cliquable.",
                    email);
            }

            return EnvoyerAsync(
                email,
                $"Réinitialisation de votre mot de passe {DocumentBrandingHelper.ApplicationName}",
                CorpsHtml(
                    $"Réinitialisation du mot de passe - <strong>{nomComplet}</strong>",
                    $"<p>Bonjour <strong>{nomComplet}</strong>,</p>" +
                    "<p>Une demande de réinitialisation de mot de passe a été effectuée pour votre compte. Définissez un nouveau mot de passe via le lien sécurisé ci-dessous.</p>" +
                    "<div style=\"background:#0f172a;border-radius:20px;padding:20px 22px;margin:20px 0;color:#e2e8f0;\">" +
                    $"<p style=\"margin:0 0 10px 0;\"><strong>Identifiant</strong><br/><span style=\"font-size:18px;color:#ffffff;\">{username}</span></p>" +
                    $"<p style=\"margin:0;\"><strong>Expiration du lien</strong><br/><span style=\"font-size:18px;color:#ffffff;\">{dateExpiration:dd/MM/yyyy HH:mm}</span></p>" +
                    "</div>" +
                    "<p style=\"margin-top:18px;font-size:13px;color:#475569;\">Si le bouton ne s'affiche pas, copiez-collez ce lien dans votre navigateur :<br/>" +
                    $"<a href=\"{lienReinitialisation}\" style=\"color:#0f4c81;word-break:break-all;\">{lienReinitialisation}</a></p>" +
                    "<p>Si vous n'êtes pas à l'origine de cette demande, ignorez ce message : votre mot de passe actuel reste valide. Contactez la DSI si cela se reproduit.</p>",
                    "Mot de passe oublié",
                    "Définir mon nouveau mot de passe",
                    lienReinitialisation));
        }

        public Task EnvoyerConfirmationCreationCompteAuDMAsync(string emailDM, string nomDM, string nomNouvelUtilisateur)
            => EnvoyerAsync(
                emailDM,
                $"[Information] Compte créé - {nomNouvelUtilisateur}",
                CorpsHtml(
                    $"Compte créé - <strong>{nomNouvelUtilisateur}</strong>",
                    $"<p>Bonjour <strong>{nomDM}</strong>,</p>" +
                    $"<p>Le compte de <strong>{nomNouvelUtilisateur}</strong> a été créé avec succès.</p>" +
                    "<p>Un lien d'activation sécurisé a été transmis directement à l'utilisateur.</p>",
                    "Compte activé",
                    "Ouvrir Zéïnab"));

        public Task EnvoyerRefusCreationCompteAsync(string emailDemandeur, string nomComplet, string acteur, string? commentaire)
            => EnvoyerAsync(
                emailDemandeur,
                "[Information] Demande de compte refusée",
                CorpsHtml(
                    "Demande de compte refusée",
                    $"<p>Bonjour <strong>{nomComplet}</strong>,</p>" +
                    $"<p>Votre demande de création de compte a été refusée par <strong>{acteur}</strong>.</p>" +
                    $"{FormatOptionalParagraph("Motif", commentaire)}" +
                    "<p>Pour plus d'informations, contactez votre Directeur Métier ou la DSI.</p>",
                    "Compte refusé",
                    "Ouvrir Zéïnab"));

        public Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null)
            => EnvoyerAsync(to, subject, LooksLikeFullHtml(htmlBody) ? htmlBody : CorpsHtml(subject, htmlBody));

        private SmtpClient CreateSmtpClient(IConfigurationSection section)
        {
            var host = section["Host"] ?? throw new InvalidOperationException("SMTP host is required.");
            var port = int.TryParse(section["Port"], out var parsedPort) ? parsedPort : 587;
            var enableSsl = bool.TryParse(section["EnableSsl"], out var ssl) && ssl;
            var useDefaultCredentials = bool.TryParse(section["UseDefaultCredentials"], out var defaultCreds) && defaultCreds;
            var username = section["Username"]?.Trim();
            var password = section["Password"] ?? string.Empty;

            var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15_000
            };

            if (useDefaultCredentials)
            {
                client.UseDefaultCredentials = true;
                _logger.LogDebug("SMTP configuré avec les identifiants Windows par défaut vers {Host}:{Port}", host, port);
            }
            else if (!string.IsNullOrWhiteSpace(username))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(username, password);
                _logger.LogDebug("SMTP configuré en mode authentifié vers {Host}:{Port} pour {Username}", host, port, username);
            }
            else
            {
                client.UseDefaultCredentials = false;
                client.Credentials = null;
                _logger.LogDebug("SMTP configuré en mode relais anonyme vers {Host}:{Port}", host, port);
            }

            return client;
        }

        private string EnsureWrappedHtml(string sujet, string corpsHtml)
            => LooksLikeFullHtml(corpsHtml) ? corpsHtml : CorpsHtml(sujet, corpsHtml);

        private static bool LooksLikeFullHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return false;
            }

            return html.Contains("<html", StringComparison.OrdinalIgnoreCase)
                || html.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
        }

        private string CorpsHtml(string titre, string corps, string? badge = null, string? actionLabel = null, string? actionUrl = null)
        {
            var finalActionUrl = actionUrl ?? GetApplicationUrl();
            var finalActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? $"Ouvrir {DocumentBrandingHelper.ApplicationName}" : actionLabel;
            var actionBlock = !string.IsNullOrWhiteSpace(finalActionUrl)
                ? $"""
                    <div style="margin-top:28px;">
                      <a href="{finalActionUrl}" style="display:inline-block;padding:14px 22px;border-radius:999px;background:linear-gradient(135deg,#0f4c81 0%,#1d8cf8 100%);color:#ffffff;font-weight:700;text-decoration:none;">
                        {finalActionLabel}
                      </a>
                    </div>
                    """
                : string.Empty;

            var badgeBlock = string.IsNullOrWhiteSpace(badge)
                ? string.Empty
                : $"""
                    <div style="display:inline-block;margin-bottom:18px;padding:8px 14px;border-radius:999px;background:#e7f0ff;color:#0f4c81;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">
                      {badge}
                    </div>
                    """;

            return $"""
            <!DOCTYPE html>
            <html lang="fr">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{DocumentBrandingHelper.ApplicationName}</title>
            </head>
            <body style="margin:0;padding:0;background:#eef4ff;font-family:'Segoe UI',Arial,sans-serif;color:#0f172a;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef4ff;padding:28px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:720px;background:#ffffff;border-radius:28px;overflow:hidden;box-shadow:0 24px 60px rgba(15,23,42,.12);">
                      <tr>
                        <td style="padding:0;background:linear-gradient(135deg,#0f2e4f 0%,#0f4c81 52%,#d9a441 100%);">
                          <div style="padding:30px 34px 26px 34px;">
                            <div style="display:flex;align-items:center;gap:14px;">
                              <div style="display:inline-flex;width:54px;height:54px;border-radius:18px;background:rgba(255,255,255,.16);color:#ffffff;align-items:center;justify-content:center;font-weight:800;font-size:18px;letter-spacing:.08em;">ZE</div>
                              <div>
                                <div style="color:#ffffff;font-size:24px;font-weight:800;line-height:1.15;">{DocumentBrandingHelper.ApplicationName}</div>
                                <div style="color:rgba(255,255,255,.82);font-size:13px;">{DocumentBrandingHelper.CompanyName} - Pilotage projets et validations</div>
                              </div>
                            </div>
                          </div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:34px;">
                          {badgeBlock}
                          <h1 style="margin:0 0 16px 0;font-size:28px;line-height:1.15;color:#0f172a;">{titre}</h1>
                          <div style="font-size:15px;line-height:1.75;color:#334155;">
                            {corps}
                          </div>
                          {actionBlock}
                          <div style="margin-top:30px;padding:18px 20px;border-radius:20px;background:#f8fafc;border:1px solid #dbe7f5;color:#64748b;font-size:13px;line-height:1.65;">
                            <strong style="color:#0f172a;">Bon à savoir</strong><br />
                            Ce message est envoyé automatiquement par {DocumentBrandingHelper.ApplicationName}. Les actions, validations et livrables restent tracés dans l'application.
                          </div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:22px 34px;background:#0f172a;color:#cbd5e1;font-size:12px;line-height:1.7;">
                          <div style="font-weight:700;color:#ffffff;margin-bottom:4px;">{DocumentBrandingHelper.ApplicationName} - {DocumentBrandingHelper.CompanyName}</div>
                          <div>Message automatique. Merci de ne pas répondre directement à cet email.</div>
                          <div style="margin-top:6px;">Site : {DocumentBrandingHelper.CompanySite}</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
        }

        private string? GetApplicationUrl()
        {
            var baseUrl = _config["SmtpSettings:BaseUrl"]?.Trim();
            return string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
        }

        private static string FormatOptionalParagraph(string label, string? value)
            => string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : $"<p><strong>{label} :</strong> {value}</p>";
    }
}
