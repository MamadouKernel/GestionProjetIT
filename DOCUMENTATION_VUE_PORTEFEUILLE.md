# Documentation : Vue Portefeuille de Projets

## 📋 Vue d'ensemble

La vue **Portefeuille** est une vue stratégique qui affiche tous les projets IT de la DSI dans un contexte global. Elle permet de visualiser l'ensemble du portefeuille avec ses objectifs stratégiques, avantages attendus, risques globaux et la liste de tous les projets.

---

## 🔄 Flux de Fonctionnement

### 1. **Accès à la Vue**

**Route :** `GET /Projet/Portefeuille`

**Autorisation :** 
- ✅ DSI
- ✅ AdminIT  
- ✅ Responsable Solutions IT

**Contrôleur :** `ProjetController.Portefeuille()`

---

### 2. **Chargement des Données (Côté Serveur)**

#### Étape 1 : Récupération ou Création du Portefeuille

```csharp
// 1. Chercher le portefeuille actif
var portefeuille = await _db.PortefeuillesProjets
    .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);
```

**Logique :**
- Le système cherche un portefeuille avec `EstActif = true` et `EstSupprime = false`
- Il ne peut y avoir qu'**un seul portefeuille actif** à la fois
- Si aucun portefeuille n'existe, le système en crée un **automatiquement** avec des valeurs par défaut

#### Étape 2 : Création Automatique du Portefeuille (si nécessaire)

Si aucun portefeuille actif n'existe, le système crée automatiquement :

```csharp
portefeuille = new PortefeuilleProjet
{
    Nom = "Portefeuille de Projet DSI",
    ObjectifStrategiqueGlobal = "Assurer l'amélioration globale...",
    AvantagesAttendus = "• Liste des avantages...",
    RisquesEtMitigations = "Risque: Mitigation...",
    EstActif = true
};
```

**Valeurs par défaut pré-remplies :**
- Objectif stratégique global
- Liste des avantages attendus (12 avantages)
- Liste des risques et mitigations (5 risques)

#### Étape 3 : Récupération des Projets

```csharp
var projets = await _db.Projets
    .Include(p => p.Direction)
    .Include(p => p.Sponsor)
    .Include(p => p.ChefProjet)
    .Include(p => p.DemandeProjet)
    .Where(p => !p.EstSupprime && 
                p.PortefeuilleProjetId == portefeuille.Id)
    .OrderBy(p => p.Titre)
    .ToListAsync();
```

**Critères de sélection :**
- ✅ Projets non supprimés (`!p.EstSupprime`)
- ✅ Projets assignés au portefeuille actif (`p.PortefeuilleProjetId == portefeuille.Id`)
- ✅ Triés par titre alphabétique

**Relations chargées (Eager Loading) :**
- `Direction` : Direction métier du projet
- `Sponsor` : Directeur Métier (sponsor)
- `ChefProjet` : Chef de Projet assigné
- `DemandeProjet` : Demande à l'origine du projet

---

### 3. **Affichage dans la Vue**

La vue est structurée en **4 sections principales** :

#### Section 1 : En-tête
- Titre : Nom du portefeuille
- Boutons d'action :
  - **Télécharger Excel** : Export du portefeuille
  - **Modifier le Portefeuille** : Édition (DSI/AdminIT uniquement)

#### Section 2 : Objectif Stratégique Global
- **Bannière bleue** avec l'objectif stratégique
- Texte : `portefeuille.ObjectifStrategiqueGlobal`
- Affichage en grand format pour visibilité

#### Section 3 : Deux Colonnes (Avantages / Risques)

**Colonne Gauche : Avantages Attendus**
- **Header vert** avec icône checkmark
- Liste des avantages extraits de `portefeuille.AvantagesAttendus`
- Format : Chaque ligne commençant par `•` devient un item
- Scrollable si contenu long (max-height: 500px)

**Colonne Droite : Risques et Mitigations**
- **Header orange** avec icône warning
- Liste des risques extraits de `portefeuille.RisquesEtMitigations`
- Format : `"Risque: Mitigation"` (séparés par `:`)
- Chaque risque affiché dans une carte avec bordure orange
- Scrollable si contenu long (max-height: 500px)

#### Section 4 : Tableau des Projets

**Colonnes affichées :**
1. **#** : Numéro d'ordre
2. **Nom du Projet** : Code projet + Titre
3. **Objectif** : Objectif du projet (si défini)
4. **Parties Prenantes Clés** : Sponsor, Chef Projet, Direction
5. **Statut Actuel** : Badge coloré (En cours, Suspendu, Clôturé, etc.)
6. **Phase** : Phase actuelle du projet
7. **RAG** : Indicateur Vert/Orange/Rouge
8. **Avancement** : Barre de progression + pourcentage
9. **Actions** : Bouton "Détails"

**Fonctionnalités :**
- Badge avec nombre total de projets
- Tri automatique par titre
- Indicateur RAG calculé automatiquement
- Barre de progression visuelle

---

## 🔗 Assignation Automatique des Projets

### Quand un projet est créé

Lorsqu'une **demande est validée par la DSI** :

```csharp
// Dans DemandeProjetController.ValiderDSI()
var portefeuilleActif = await _db.PortefeuillesProjets
    .FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);

var projet = new Projet
{
    // ...
    PortefeuilleProjetId = portefeuilleActif?.Id, // ← Assignation automatique
    // ...
};
```

**Comportement :**
- ✅ Le projet est **automatiquement assigné** au portefeuille actif
- ✅ Si aucun portefeuille actif n'existe, le projet n'a pas de portefeuille (`PortefeuilleProjetId = null`)
- ✅ Le projet apparaîtra dans la vue Portefeuille **après rechargement de la page**

---

## ✏️ Modification du Portefeuille

### Action : `UpdatePortefeuille`

**Route :** `POST /Projet/UpdatePortefeuille`

**Autorisation :** DSI, AdminIT uniquement

**Champs modifiables :**
1. **Objectif Stratégique Global** (obligatoire)
2. **Avantages Attendus** (obligatoire)
   - Format : Un avantage par ligne, commencer par `•`
3. **Risques et Mitigations** (obligatoire)
   - Format : `Risque: Mitigation` (un par ligne)

**Procédure :**
1. Cliquer sur "Modifier le Portefeuille"
2. Modal s'ouvre avec les champs pré-remplis
3. Modifier les valeurs
4. Cliquer sur "Enregistrer"
5. Redirection vers la vue Portefeuille avec message de succès

**Validation :**
- Tous les champs sont obligatoires
- En cas d'erreur, le modal reste ouvert avec les erreurs affichées

---

## 📊 Export Excel

### Action : `GenererPortefeuilleExcel`

**Route :** `POST /Projet/GenererPortefeuilleExcel`

**Fonctionnalité :**
- Génère un fichier Excel avec tous les projets du portefeuille
- Inclut : Direction, Chef Projet, Dates, Budget, Charges, Risques, RAG
- Nom du fichier : `PortefeuilleProjets_YYYYMMDD.xlsx`
- Téléchargement automatique

---

## 🔄 Cycle de Vie d'un Projet dans le Portefeuille

```
1. Demande créée par Demandeur
   ↓
2. Validation Directeur Métier
   ↓
3. Validation DSI
   ↓
4. Projet créé automatiquement
   ↓
5. Projet assigné au Portefeuille actif (automatique)
   ↓
6. Projet apparaît dans la vue Portefeuille (après rechargement)
   ↓
7. Projet évolue (phases, statuts, RAG)
   ↓
8. Projet reste dans le portefeuille jusqu'à clôture
```

---

## ⚠️ Points Importants

### 1. **Un seul Portefeuille Actif**
- Il ne peut y avoir qu'un seul portefeuille avec `EstActif = true`
- Tous les nouveaux projets sont assignés à ce portefeuille

### 2. **Pas de Mise à Jour Automatique**
- La vue ne se met **pas à jour automatiquement**
- Il faut **recharger la page** (F5) pour voir les nouveaux projets
- Les modifications de statut/phase nécessitent aussi un rechargement

### 3. **Filtrage Automatique**
- Seuls les projets du portefeuille actif sont affichés
- Les projets sans portefeuille (`PortefeuilleProjetId = null`) ne sont **pas** affichés

### 4. **Calcul RAG**
- L'indicateur RAG est calculé automatiquement par le service `RAGCalculationService`
- Basé sur : Budget, Planning, Risques, Livrables
- Mis à jour lors des modifications de projet

### 5. **Permissions**
- **Lecture** : DSI, AdminIT, Responsable Solutions IT
- **Modification** : DSI, AdminIT uniquement
- **Export** : Tous les rôles ayant accès à la vue

---

## 🎯 Cas d'Usage

### Scénario 1 : Premier Accès
1. DSI accède à `/Projet/Portefeuille`
2. Aucun portefeuille n'existe
3. Système crée automatiquement un portefeuille avec valeurs par défaut
4. Vue affichée avec portefeuille vide (aucun projet)

### Scénario 2 : Validation d'une Demande
1. DSI valide une demande
2. Projet créé automatiquement
3. Projet assigné au portefeuille actif
4. **Action requise** : Recharger la page Portefeuille pour voir le nouveau projet

### Scénario 3 : Modification du Portefeuille
1. DSI clique sur "Modifier le Portefeuille"
2. Modal s'ouvre avec les valeurs actuelles
3. DSI modifie l'objectif stratégique
4. DSI enregistre
5. Redirection vers la vue avec message de succès
6. Vue affichée avec les nouvelles valeurs

### Scénario 4 : Export pour Reporting
1. DSI clique sur "Télécharger Excel"
2. Fichier Excel généré avec tous les projets
3. Téléchargement automatique
4. Fichier peut être partagé avec la Direction Générale

---

## 🔍 Structure des Données

### Modèle PortefeuilleProjet

```csharp
public class PortefeuilleProjet
{
    public Guid Id { get; set; }
    public string Nom { get; set; }
    public string ObjectifStrategiqueGlobal { get; set; }
    public string AvantagesAttendus { get; set; }      // Format: "• Avantage 1\n• Avantage 2"
    public string RisquesEtMitigations { get; set; }   // Format: "Risque: Mitigation\n..."
    public bool EstActif { get; set; }                 // Un seul portefeuille actif
}
```

### Lien Projet ↔ Portefeuille

```csharp
public class Projet
{
    public Guid? PortefeuilleProjetId { get; set; }  // Nullable : peut être sans portefeuille
    public PortefeuilleProjet? PortefeuilleProjet { get; set; }
}
```

---

## 📝 Notes Techniques

1. **Performance :**
   - Utilisation d'`Include()` pour éviter les requêtes N+1
   - Chargement eager des relations nécessaires
   - Pas de pagination (tous les projets affichés)

2. **Sécurité :**
   - Autorisation par rôle dans le contrôleur
   - Validation des données dans `UpdatePortefeuille`
   - Audit trail pour les modifications

3. **UX :**
   - Design moderne avec cartes colorées
   - Scrollable pour contenu long
   - Badges visuels pour statuts et RAG
   - Barres de progression pour avancement

---

## 🚀 Améliorations Possibles

1. **Mise à jour automatique** : Ajouter SignalR ou polling JavaScript
2. **Pagination** : Si beaucoup de projets
3. **Filtres** : Par direction, phase, statut, RAG
4. **Recherche** : Par nom de projet
5. **Graphiques** : Visualisation des statistiques du portefeuille

