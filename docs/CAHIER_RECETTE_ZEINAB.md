# Cahier de Recette — Zéïnab
## Plateforme de Gestion de Projets IT — Côte d'Ivoire Terminal

**Version :** 1.0  
**Date :** Juin 2026  
**URL de test :** http://10.88.179.103:8089  
**Mot de passe commun (tous les comptes) :** `Zeinab@2024!`

---

## Comptes testeurs disponibles

| Rôle | Matricule | Nom | Email |
|------|-----------|-----|-------|
| DSI | DSI001 | Paul Koffi | paul.koffi@cit.ci |
| Responsable Solutions IT | RSI001 | Seydou Bamba | seydou.bamba@cit.ci |
| Chef de Projet | CP001 | Fatou Diallo | fatou.diallo@cit.ci |
| Chef de Projet | CP002 | Ibrahim Touré | ibrahim.toure@cit.ci |
| Directeur Métier (Finance) | DM001 | Marie Yao | marie.yao@cit.ci |
| Directeur Métier (Opérations) | DM002 | Serge Kouamé | serge.kouame@cit.ci |
| Directeur Métier (RH) | DM003 | Blanche Assi | blanche.assi@cit.ci |
| Demandeur (Finance) | DEM001 | Jean Kouassi | jean.kouassi@cit.ci |
| Demandeur (Opérations) | DEM002 | Aminata Traoré | aminata.traore@cit.ci |
| Demandeur (RH) | DEM003 | Moussa Fofana | moussa.fofana@cit.ci |
| Demandeur (Commercial) | DEM004 | Cécile Aka | cecile.aka@cit.ci |
| Admin IT | admin | Administrateur DSI | admin@cit.ci |

> **Note :** Le mot de passe admin peut être différent (généré automatiquement). Contacter l'Admin IT si besoin.

---

## Données de démonstration pré-chargées

| Titre | Statut demande | Phase projet | Matricule demandeur |
|-------|---------------|--------------|---------------------|
| Système de gestion des congés | Validée DSI | Analyse & Clarification | DEM001 |
| Refonte du portail intranet | Validée DSI | Planification | DEM002 |
| Application mobile de réservation de bus | Validée DSI | Exécution | DEM003 |
| Tableau de bord commercial | En attente DSI | — | DEM004 |
| Migration Oracle → SQL Server | Brouillon | — | DEM001 |
| Système de ticketing IT | En attente DM | — | DEM002 |

---

## Conventions du cahier de recette

- **✅ PASS** : le résultat correspond au résultat attendu
- **❌ FAIL** : le résultat ne correspond pas — noter l'écart observé
- **⚠️ PARTIEL** : fonctionne partiellement — noter ce qui manque
- **🔄 BLOQUÉ** : impossible d'exécuter à cause d'un prérequis non rempli

---

---

# MODULE 1 — AUTHENTIFICATION

## TC-AUTH-01 : Connexion avec un compte valide
**Rôle testé :** Tous  
**Prérequis :** Aucun

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Aller sur http://10.88.179.103:8089 | Redirection vers la page de connexion | | |
| 2 | Saisir matricule `DEM001` et mot de passe `Zeinab@2024!` | Connexion réussie, redirection vers le tableau de bord | | |
| 3 | Vérifier que le nom "Jean Kouassi" apparaît dans l'interface | Nom affiché correctement | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-AUTH-02 : Connexion avec un mauvais mot de passe
**Rôle testé :** Tous

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Saisir matricule `DEM001` et mot de passe `mauvais` | Message d'erreur "Matricule ou mot de passe incorrect" | | |
| 2 | Vérifier que la page de connexion reste affichée | Pas de redirection | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-AUTH-03 : Accès refusé selon le rôle
**Rôle testé :** DEM001 (Demandeur)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DEM001` | Connexion réussie | | |
| 2 | Tenter d'accéder à `/Admin/Users` | Accès refusé (403) ou redirection | | |
| 3 | Tenter d'accéder à `/Admin/ListeRoles` | Accès refusé (403) ou redirection | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-AUTH-04 : Déconnexion
**Rôle testé :** Tous

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter avec n'importe quel compte | Connexion réussie | | |
| 2 | Cliquer sur "Déconnexion" | Redirection vers la page de connexion | | |
| 3 | Tenter d'accéder à `/Projet/Index` sans se reconnecter | Redirection vers la connexion | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 2 — DEMANDE DE PROJET (Rôle : Demandeur)

## TC-DEM-01 : Créer une nouvelle demande
**Rôle testé :** DEM001

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DEM001` | Connexion réussie | | |
| 2 | Aller dans "Mes demandes" > "Nouvelle demande" | Formulaire de création affiché | | |
| 3 | Remplir : Titre = "Test recette CAS-01", Description, Contexte, Objectifs | Champs acceptés | | |
| 4 | Sélectionner Direction : "Direction Financière" | Direction sélectionnée | | |
| 5 | Sélectionner Directeur Métier : "Marie Yao" | DM sélectionné | | |
| 6 | Cliquer "Enregistrer en brouillon" | Demande créée avec statut **Brouillon** | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-DEM-02 : Soumettre une demande au Directeur Métier
**Rôle testé :** DEM001  
**Prérequis :** TC-DEM-01 exécuté

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Ouvrir la demande créée en TC-DEM-01 | Détail de la demande affiché | | |
| 2 | Cliquer "Soumettre" | Message de confirmation | | |
| 3 | Vérifier le statut | Statut = **En attente validation Directeur Métier** | | |
| 4 | Vérifier que le bouton "Modifier" n'est plus disponible | Demande verrouillée en édition | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-DEM-03 : Validation par le Directeur Métier
**Rôle testé :** DM001  
**Prérequis :** TC-DEM-02 exécuté

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DM001` | Connexion réussie | | |
| 2 | Aller dans "Validations" > "Demandes à valider" | Demande TC-DEM-01 visible | | |
| 3 | Ouvrir la demande | Détail affiché avec boutons Valider / Rejeter / Demander correction | | |
| 4 | Saisir un commentaire et cliquer "Valider" | Message de succès | | |
| 5 | Vérifier le statut | Statut = **En attente validation DSI** | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-DEM-04 : Rejet par le Directeur Métier
**Rôle testé :** DM001  
**Prérequis :** Une demande en attente DM (utiliser "Système de ticketing IT" / DEM002)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DM002` | Connexion réussie | | |
| 2 | Ouvrir la demande "Système de ticketing IT" | Détail affiché | | |
| 3 | Cliquer "Rejeter" sans commentaire | Message d'erreur : commentaire obligatoire | | |
| 4 | Saisir un commentaire et cliquer "Rejeter" | Message de succès | | |
| 5 | Vérifier le statut | Statut = **Rejetée par le Directeur Métier** | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-DEM-05 : Validation par la DSI — Création automatique du projet
**Rôle testé :** DSI001 ou RSI001  
**Prérequis :** TC-DEM-03 exécuté (demande en attente DSI)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DSI001` | Connexion réussie | | |
| 2 | Aller dans "Validations DSI" | Demande visible | | |
| 3 | Ouvrir la demande | Formulaire avec liste des chefs de projet | | |
| 4 | Sélectionner Chef de Projet = "Fatou Diallo" | CP sélectionné | | |
| 5 | Saisir un commentaire et cliquer "Valider" | Message de succès | | |
| 6 | Vérifier : un projet est créé automatiquement | Redirection vers la fiche projet | | |
| 7 | Vérifier les données du projet : Phase = **Analyse & Clarification**, Statut = **Non démarré** | Données correctes | | |
| 8 | Vérifier que le Chef de Projet est bien "Fatou Diallo" | Affiché sur la fiche | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-DEM-06 : Validation DSI par le Responsable Solutions IT
**Rôle testé :** RSI001  
**Prérequis :** Une demande en attente DSI disponible

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `RSI001` | Connexion réussie | | |
| 2 | Aller dans "Validations DSI" | Demandes en attente visibles | | |
| 3 | Valider une demande en choisissant un CP | Projet créé comme avec la DSI | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 3 — GESTION DE PROJET (Rôle : Chef de Projet / DSI / DM)

## TC-PROJ-01 : Accès et lecture de la fiche projet
**Rôle testé :** CP001, DSI001, DM001

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `CP001` | Connexion réussie | | |
| 2 | Aller dans "Mes projets" | Liste des projets assignés visible | | |
| 3 | Ouvrir "Système de gestion des congés" | Fiche projet avec onglets | | |
| 4 | Naviguer sur chaque onglet : Synthèse, Analyse, Planification, Exécution, UAT, Clôture, Historique | Onglets accessibles, données affichées | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-02 : Validation de la charte — DM puis DSI/RSI
**Rôle testé :** DM001, puis RSI001  
**Prérequis :** Projet "Système de gestion des congés" en phase Analyse

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `CP001` | Connexion réussie | | |
| 2 | Ouvrir le projet > onglet Analyse > déposer un livrable de type "Charte signée" | Livrable déposé avec succès | | |
| 3 | Se connecter en tant que `DM001` | Connexion réussie | | |
| 4 | Aller dans "Validations" > "Validation charte" | Projet visible | | |
| 5 | Valider la charte | Message de succès, CharteValidéeParDM = vrai | | |
| 6 | Se connecter en tant que `RSI001` | Connexion réussie | | |
| 7 | Valider la charte côté DSI | Message de succès, Charte = **entièrement validée** | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-03 : Passage en phase Planification
**Rôle testé :** CP001  
**Prérequis :** TC-PROJ-02 exécuté (charte validée)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `CP001` | Connexion réussie | | |
| 2 | Ouvrir le projet > onglet Analyse | Bouton "Valider phase Analyse" visible | | |
| 3 | Cliquer "Valider phase Analyse" | Message de succès | | |
| 4 | Vérifier la phase | Phase = **Planification & Validation** | | |
| 5 | Vérifier le statut | Statut = **En cours** | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-04 : Commentaire technique par le Responsable Solutions IT
**Rôle testé :** RSI001

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `RSI001` | Connexion réussie | | |
| 2 | Ouvrir un projet > onglet Analyse | Zone "Commentaire technique" visible | | |
| 3 | Saisir un commentaire et enregistrer | Commentaire sauvegardé avec la date et l'auteur | | |
| 4 | Se connecter en tant que `DEM001` et tenter d'éditer le commentaire technique | Zone non éditable (accès refusé) | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-05 : Validation planification DM + RSI → Phase Exécution
**Rôle testé :** DM001, RSI001  
**Prérequis :** Projet en phase Planification (utiliser "Refonte du portail intranet")

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `CP001` — déposer un livrable WBS | Livrable déposé | | |
| 2 | Se connecter en tant que `DM001` — valider la planification | PlanningValideParDM = vrai | | |
| 3 | Se connecter en tant que `RSI001` — valider la planification | Phase = **Exécution & Suivi**, date de début renseignée | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-06 : Passage en phase UAT
**Rôle testé :** CP001  
**Prérequis :** Projet en phase Exécution (utiliser "Application mobile de réservation de bus")

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `CP002` | Connexion réussie | | |
| 2 | Ouvrir le projet > onglet Exécution | Bouton "Passer en UAT" visible | | |
| 3 | Déposer un livrable CR de réunion si requis | Livrable déposé | | |
| 4 | Cliquer "Passer en UAT" | Phase = **UAT & MEP** | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-07 : Validation de la recette par le DM
**Rôle testé :** DM (sponsor du projet)  
**Prérequis :** TC-PROJ-06 exécuté

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que DM sponsor | Connexion réussie | | |
| 2 | Aller dans "Validations" > "Validation recette" | Projet visible | | |
| 3 | Valider la recette | RecetteValidee = vrai, message de succès | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PROJ-08 : Clôture complète du projet (3 validations)
**Rôle testé :** CP001/CP002, DEM, DM, DSI  
**Prérequis :** Projet en phase UAT avec recette validée et MEP effectuée

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | CP : renseigner bilan, leçons apprises, hypercare terminé | Données enregistrées | | |
| 2 | CP : cliquer "Fin UAT" | Phase = **Clôture & Leçons apprises** | | |
| 3 | CP : cliquer "Demander clôture" | Demande de clôture créée, statuts EnAttente | | |
| 4 | Demandeur : valider la clôture | StatutDemandeur = Validée | | |
| 5 | DM : valider la clôture | StatutDM = Validée | | |
| 6 | DSI : valider la clôture | StatutDSI = Validée | | |
| 7 | Vérifier le statut final du projet | Statut = **Clôturé**, DateFinRéelle renseignée | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 4 — ADMINISTRATION (Rôle : Admin IT)

## TC-ADMIN-01 : Créer un utilisateur
**Rôle testé :** admin

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `admin` | Connexion réussie | | |
| 2 | Aller dans Admin > Utilisateurs > "Nouvel utilisateur" | Formulaire affiché | | |
| 3 | Remplir les informations et attribuer le rôle "Demandeur" | Utilisateur créé | | |
| 4 | Tenter de créer un utilisateur avec le même matricule | Message d'erreur : matricule déjà utilisé | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-ADMIN-02 : Règle AdminIT exclusif
**Rôle testé :** admin

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Ouvrir la fiche d'un utilisateur existant | Grille des rôles affichée | | |
| 2 | Cocher "Admin IT" | Tous les autres rôles se décoches et se grisent | | |
| 3 | Tenter de cocher "Demandeur" en même temps que "Admin IT" | Impossible — bouton bloqué | | |
| 4 | Décocher "Admin IT" | Les autres rôles redeviennent disponibles | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-ADMIN-03 : Import d'utilisateurs via Excel
**Rôle testé :** admin

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Admin > Utilisateurs > "Import Excel" — télécharger le modèle | Fichier Excel téléchargé | | |
| 2 | Remplir le fichier avec 2-3 utilisateurs valides | Fichier prêt | | |
| 3 | Importer le fichier avec un mot de passe valide (≥12 car, 1 maj, 1 chiffre) | Import réussi, utilisateurs créés | | |
| 4 | Importer un fichier avec un matricule déjà existant | Ligne ignorée ou erreur signalée | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-ADMIN-04 : Gestion des directions
**Rôle testé :** admin

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Admin > Directions > "Nouvelle direction" | Formulaire affiché | | |
| 2 | Créer une direction "Direction Test" | Direction créée avec un code généré | | |
| 3 | Modifier le libellé | Modification enregistrée | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 5 — TABLEAU DE BORD & PORTEFEUILLE

## TC-DASH-01 : Tableau de bord selon le rôle
| # | Rôle | Action | Résultat attendu | Statut |
|---|------|--------|-----------------|--------|
| 1 | DEM001 | Se connecter et voir le dashboard | Ses demandes et projets uniquement | |
| 2 | DM001 | Se connecter et voir le dashboard | Projets de sa direction + indicateurs DM | |
| 3 | CP001 | Se connecter et voir le dashboard | Projets où il est chef + indicateurs CP | |
| 4 | DSI001 | Se connecter et voir le dashboard | Vue globale de tous les projets | |
| 5 | RSI001 | Se connecter et voir le dashboard | Vue globale + indicateurs techniques | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-DASH-02 : Vue Portefeuille
**Rôle testé :** DSI001, RSI001

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DSI001` | Connexion réussie | | |
| 2 | Aller dans "Portefeuille" | Vue stratégique avec tous les projets | | |
| 3 | Vérifier les filtres (direction, chef de projet, phase) | Filtres fonctionnels | | |
| 4 | Se connecter en tant que `DEM001` et tenter d'accéder au portefeuille | Accès refusé | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 6 — NOTIFICATIONS

## TC-NOTIF-01 : Notification entrée en UAT
**Rôle testé :** RSI001  
**Prérequis :** Un projet passe en phase UAT (TC-PROJ-06)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `RSI001` après passage en UAT | Connexion réussie | | |
| 2 | Vérifier le centre de notifications | Notification "Projet XXX entré en phase UAT" visible | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-NOTIF-02 : Notification anomalie critique
**Rôle testé :** CP001, RSI001

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `CP001` | Connexion réussie | | |
| 2 | Ouvrir un projet > onglet Exécution > ajouter une anomalie de priorité **Critique** | Anomalie ajoutée | | |
| 3 | Se connecter en tant que `RSI001` | Connexion réussie | | |
| 4 | Vérifier les notifications | Notification anomalie critique reçue | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 7 — TESTS DE SÉCURITÉ

## TC-SEC-01 : Isolation des données entre directions
**Rôle testé :** DM001 (Direction Finance), DM002 (Direction Opérations)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | Se connecter en tant que `DM001` | Connexion réussie | | |
| 2 | Consulter la liste des projets | Uniquement les projets de la Direction Financière | | |
| 3 | Se connecter en tant que `DM002` | Connexion réussie | | |
| 4 | Consulter la liste des projets | Uniquement les projets de la Direction Opérations | | |
| 5 | Vérifier que DM001 ne voit pas les projets de DM002 | Isolation correcte | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-SEC-02 : Tentative de validation de sa propre demande
**Rôle testé :** Utilisateur avec rôle DM et Demandeur simultanément (non applicable selon règle AdminIT exclusive)

| # | Action | Résultat attendu | Résultat observé | Statut |
|---|--------|-----------------|------------------|--------|
| 1 | DM001 tente de valider une demande dont il est le demandeur | Message d'erreur : "Vous ne pouvez pas valider votre propre demande" | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-SEC-03 : Accès direct aux URLs protégées
| # | URL testée | Rôle utilisé | Résultat attendu | Statut |
|---|-----------|-------------|-----------------|--------|
| 1 | `/Admin/Users` | DEM001 | Accès refusé (403) | |
| 2 | `/Admin/ListeRoles` | CP001 | Accès refusé (403) | |
| 3 | `/Projet/Portefeuille` | DEM001 | Accès refusé (403) | |
| 4 | `/Admin/Users` | admin | Accès autorisé | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# MODULE 8 — PERFORMANCE & ERGONOMIE

## TC-PERF-01 : Temps de chargement des pages principales
| # | Page | Temps max acceptable | Temps mesuré | Statut |
|---|------|---------------------|-------------|--------|
| 1 | Page de connexion | < 2 s | | |
| 2 | Tableau de bord (DSI) | < 3 s | | |
| 3 | Liste des projets | < 3 s | | |
| 4 | Fiche projet (tous onglets) | < 4 s | | |
| 5 | Portefeuille | < 4 s | | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

## TC-PERF-02 : Compatibilité navigateur
| # | Navigateur | Version | Page testée | Statut |
|---|-----------|---------|-------------|--------|
| 1 | Chrome | Dernière | Connexion + Dashboard | |
| 2 | Edge | Dernière | Connexion + Dashboard | |
| 3 | Firefox | Dernière | Connexion + Dashboard | |

**Statut global :** ☐ PASS  ☐ FAIL  ☐ PARTIEL  
**Commentaire :**

---

---

# RÉSUMÉ D'EXÉCUTION

| Module | Nb cas | PASS | FAIL | PARTIEL | BLOQUÉ |
|--------|--------|------|------|---------|--------|
| 1 — Authentification | 4 | | | | |
| 2 — Demande de projet | 6 | | | | |
| 3 — Gestion de projet | 8 | | | | |
| 4 — Administration | 4 | | | | |
| 5 — Dashboard / Portefeuille | 2 | | | | |
| 6 — Notifications | 2 | | | | |
| 7 — Sécurité | 3 | | | | |
| 8 — Performance | 2 | | | | |
| **TOTAL** | **31** | | | | |

---

## Anomalies remontées

| # | Cas de test | Description | Priorité | Statut |
|---|------------|-------------|----------|--------|
| | | | | |

---

## Visa et signatures

| Rôle | Nom | Date | Signature |
|------|-----|------|-----------|
| Responsable recette | | | |
| Chef de projet | | | |
| Représentant DSI | | | |

---

*Document généré automatiquement — Zéïnab v1.0 — Côte d'Ivoire Terminal — DSI*
