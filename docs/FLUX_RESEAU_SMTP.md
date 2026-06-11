# Flux réseau à ouvrir — Envoi d'emails via SMTP
**Application : Gestion Projets IT — DSI**
**Date : 2026-05-13**

---

## 1. Schéma des flux

```
┌─────────────────────────────────────────────────────────┐
│                  RÉSEAU INTERNE (LAN/DMZ)                │
│                                                         │
│   ┌──────────────────┐          ┌────────────────────┐  │
│   │  Serveur Web App │  TCP ──► │   Serveur SMTP     │  │
│   │  (GestionProjets)│  PORT    │  (smtp.cit.ci)     │  │
│   │  192.168.x.x     │  587/465 │  192.168.x.y       │  │
│   └──────────────────┘          └────────────┬───────┘  │
│                                              │          │
└──────────────────────────────────────────────┼──────────┘
                                               │ (si relais externe)
                                               ▼ TCP 25/587/465
                                      ┌─────────────────┐
                                      │  Internet / MX  │
                                      │  cit.ci         │
                                      └─────────────────┘
```

---

## 2. Flux à ouvrir — tableau récapitulatif

| # | Source | Destination | Port | Protocole | Usage | Obligatoire |
|---|--------|-------------|------|-----------|-------|-------------|
| 1 | Serveur Web App | Serveur SMTP interne | **587** | TCP | SMTP STARTTLS (recommandé) | **OUI** |
| 2 | Serveur Web App | Serveur SMTP interne | **465** | TCP | SMTP SSL/TLS implicite (alternatif) | Si port 587 indisponible |
| 3 | Serveur Web App | Serveur SMTP interne | **25** | TCP | SMTP non chiffré (legacy — déconseillé) | NON |
| 4 | Serveur SMTP | Internet (MX cit.ci) | **25** | TCP | Relais sortant vers les MX externes | Si boîtes destinataires externes |
| 5 | Serveur SMTP | Serveurs DNS | **53** | UDP/TCP | Résolution MX des domaines destinataires | OUI (côté SMTP) |
| 6 | Serveur Web App | Serveurs DNS | **53** | UDP/TCP | Résolution du nom `smtp.cit.ci` | OUI |

> **Flux prioritaire à ouvrir en premier** : Flux **#1** (App → SMTP, TCP 587).
> Les autres flux dépendent de l'architecture choisie (SMTP interne vs externe, relay vs direct).

---

## 3. Détail des scénarios

### Scénario A — SMTP interne avec STARTTLS (recommandé en production)

Configuration `appsettings.json` :
```json
"Smtp": {
  "Host": "smtp.cit.ci",
  "Port": 587,
  "UseStartTls": true,
  "UseSsl": false
}
```

Flux à ouvrir :
- **App → SMTP** : TCP **587** sortant depuis le serveur web, entrant sur le serveur SMTP
- **SMTP → DNS** : UDP/TCP **53** pour la résolution MX si le serveur SMTP relaie vers l'extérieur
- **SMTP → Internet** : TCP **25** sortant si relais vers des boîtes externes (Gmail, Orange, etc.)

---

### Scénario B — SMTP interne avec SSL/TLS implicite

Configuration `appsettings.json` :
```json
"Smtp": {
  "Host": "smtp.cit.ci",
  "Port": 465,
  "UseStartTls": false,
  "UseSsl": true
}
```

Flux à ouvrir :
- **App → SMTP** : TCP **465** sortant depuis le serveur web, entrant sur le serveur SMTP

---

### Scénario C — SMTP Exchange / Office 365 (cloud)

Configuration `appsettings.json` :
```json
"Smtp": {
  "Host": "smtp.office365.com",
  "Port": 587,
  "UseStartTls": true,
  "UseSsl": false,
  "Username": "gestion-projets@cit.ci",
  "Password": "APP_PASSWORD_ICI"
}
```

Flux à ouvrir :
- **App → Internet** : TCP **587** sortant vers `smtp.office365.com` (IP Microsoft : 40.x.x.x / 52.x.x.x)
- **App → DNS** : UDP/TCP **53** pour résoudre `smtp.office365.com`
- Autoriser l'adresse IP du serveur web dans le **connecteur d'envoi Exchange** (relay autorisé)

---

### Scénario D — Environnement de développement / recette (MailHog)

Utiliser [MailHog](https://github.com/mailhog/MailHog) ou [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) comme serveur SMTP local simulé.

Configuration `appsettings.Development.json` :
```json
"Smtp": {
  "Host": "localhost",
  "Port": 1025,
  "UseStartTls": false,
  "UseSsl": false,
  "SimulerEnvoi": true
}
```

Flux : aucun flux réseau nécessaire (localhost uniquement).  
Interface web MailHog : `http://localhost:8025`

---

## 4. Règles de pare-feu (Firewall)

### 4.1 Règles à créer sur le pare-feu applicatif / sortant

| Règle | Action | Protocole | Source | Destination | Port dest. | Commentaire |
|-------|--------|-----------|--------|-------------|------------|-------------|
| SMTP-OUT-587 | **AUTORISER** | TCP | IP Serveur Web | IP Serveur SMTP | 587 | STARTTLS — **flux principal** |
| SMTP-OUT-465 | AUTORISER | TCP | IP Serveur Web | IP Serveur SMTP | 465 | SSL — alternatif si 587 bloqué |
| SMTP-OUT-25 | BLOQUER | TCP | IP Serveur Web | ANY | 25 | Bloquer le port 25 direct depuis l'app (anti-spam) |

### 4.2 Règles à créer sur le serveur SMTP (entrant)

| Règle | Action | Protocole | Source | Destination | Port dest. | Commentaire |
|-------|--------|-----------|--------|-------------|------------|-------------|
| SMTP-IN-587 | **AUTORISER** | TCP | IP Serveur Web | IP Serveur SMTP | 587 | Accepter la soumission depuis l'app |
| SMTP-IN-465 | AUTORISER | TCP | IP Serveur Web | IP Serveur SMTP | 465 | Alternatif SSL |
| SMTP-IN-ANY | BLOQUER | TCP | ANY | IP Serveur SMTP | 587/465 | Bloquer le reste |

### 4.3 Règles sortantes du serveur SMTP (relais vers Internet)

| Règle | Action | Protocole | Source | Destination | Port dest. | Commentaire |
|-------|--------|-----------|--------|-------------|------------|-------------|
| MX-OUT-25 | AUTORISER | TCP | IP Serveur SMTP | ANY | 25 | Livraison MX vers serveurs de messagerie externes |
| DNS-OUT | AUTORISER | UDP+TCP | IP Serveur SMTP | DNS interne | 53 | Résolution MX / A / PTR |

---

## 5. Certificat TLS

| Environnement | Recommandation |
|---------------|----------------|
| **Production** | Certificat signé par une CA reconnue ou CA interne PKI. `AccepterCertificatAutoSigne: false` |
| **Recette/UAT** | Certificat auto-signé toléré. `AccepterCertificatAutoSigne: true` |
| **Développement** | `SimulerEnvoi: true` — aucun certificat requis |

> **Important** : ne jamais mettre `AccepterCertificatAutoSigne: true` en production — cela désactive la validation du certificat et expose aux attaques man-in-the-middle.

---

## 6. Authentification SMTP

Le serveur SMTP doit accepter l'authentification `AUTH LOGIN` ou `AUTH PLAIN` sur le port 587 (STARTTLS) pour les soumissions de l'application.

Si le serveur SMTP est Exchange (on-premise), il faut créer un **connecteur de réception** dédié à l'application avec :
- Adresse IP source autorisée = IP du serveur web
- Mécanisme d'authentification : `Anonymous` (si réseau interne sécurisé) ou `BasicAuth` avec compte de service dédié
- Taille max de message : ≥ 10 Mo (pour les pièces jointes éventuelles)

---

## 7. Test de connectivité

Depuis le serveur web, tester la connexion SMTP avant la mise en production :

```powershell
# Test TCP basique (vérifier que le port est joignable)
Test-NetConnection -ComputerName smtp.cit.ci -Port 587

# Test SMTP complet avec PowerShell
$smtp = New-Object Net.Mail.SmtpClient("smtp.cit.ci", 587)
$smtp.EnableSsl = $true
$smtp.Credentials = New-Object Net.NetworkCredential("gestion-projets@cit.ci", "MOT_DE_PASSE")
$smtp.Send("gestion-projets@cit.ci", "test@cit.ci", "Test flux SMTP", "Connexion SMTP OK depuis GestionProjets")
Write-Host "Email envoyé avec succès"
```

```bash
# Linux/Bash — test avec openssl
openssl s_client -connect smtp.cit.ci:587 -starttls smtp
```

---

## 8. Résumé des actions à réaliser

| # | Action | Responsable | Priorité |
|---|--------|-------------|----------|
| 1 | Ouvrir TCP 587 entre le serveur web et le serveur SMTP | Équipe Réseau / Firewall | **HAUTE** |
| 2 | Créer un compte de messagerie dédié `gestion-projets@cit.ci` | Équipe Messagerie | **HAUTE** |
| 3 | Configurer le connecteur SMTP (relay autorisé pour l'IP du serveur web) | Équipe Messagerie | **HAUTE** |
| 4 | Renseigner le mot de passe dans `appsettings.json` (ou variable d'environnement) | Équipe Déploiement | **HAUTE** |
| 5 | Valider le certificat TLS du serveur SMTP | Équipe Sécurité | MOYENNE |
| 6 | Tester l'envoi depuis le serveur web via `Test-NetConnection` | Équipe Dev/Ops | MOYENNE |
| 7 | Ouvrir TCP 25 sortant depuis le serveur SMTP vers Internet (si relay externe) | Équipe Réseau | BASSE |
| 8 | Configurer un enregistrement SPF/DKIM pour `cit.ci` | Équipe DNS | BASSE |
