# Audit UI/UX Complet

Date: 2026-05-19

Perimetre audite: toutes les vues Razor `Views/**/*.cshtml`

Methodologie:
- inventaire complet de 85 vues Razor
- lecture structurelle de chaque vue
- verification de l'usage du design system (`site.css`) vs marqueurs legacy Bootstrap
- classement par module et par maturite visuelle

Limite:
- audit exhaustif du code des vues
- pas de passage navigateur live complet dans cette session, car l'application locale ne demarre pas ici a cause du blocage SQL Server chiffrement

## Statut Global

- Niveau global UI: bon
- Niveau global UX: moyen a bon
- Maturite estimee: 75% a 80%
- Vues avec marqueurs modernes: 65
- Vues avec marqueurs legacy detectes: 27

## Legende

- `Fort` : ecran coherent, moderne, lisible, bon niveau de finition
- `Moyen` : ecran propre mais encore dense, heterogene, ou trop inline
- `Faible` : ecran encore ancien, hors design system, ou a reprendre
- `Technique` : vue support ou partielle non jugee comme ecran final autonome

## Audit Par Module

### Shared / Shell

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Shared/_Layout.cshtml` | `Fort` | shell principal propre, sidebar/topbar modernes |
| `Views/Shared/Error.cshtml` | `Faible` | trop minimal, peu travaille visuellement |
| `Views/Shared/_Pagination.cshtml` | `Technique` | composant utilitaire |
| `Views/Shared/_ValidationScriptsPartial.cshtml` | `Technique` | composant utilitaire |
| `Views/Shared/_ValidationSummary.cshtml` | `Moyen` | fonctionnel, mais peu harmonise |
| `Views/Shared/Components/SidebarMenu/Default.cshtml` | `Technique` | composant menu |
| `Views/_ViewStart.cshtml` | `Technique` | technique |
| `Views/_ViewImports.cshtml` | `Technique` | technique |

### Home / Dashboard

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Home/Index.cshtml` | `Fort` | vrai cockpit, design moderne, hierarchie solide |
| `Views/Home/Privacy.cshtml` | `Faible` | page secondaire non retravaillee |
| `Views/Dashboard/Index.cshtml` | `Moyen` | moderne mais tres dense et encore charge en styles inline |

### Account / Auth

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Account/Login.cshtml` | `Moyen` | visuellement fort, mais ecran isole du design system global |
| `Views/Account/Profil.cshtml` | `Moyen` | bonne base, encore mixte et charge |
| `Views/Account/AccessDenied.cshtml` | `Faible` | trop simple, style legacy |
| `Views/Account/DemandeAcces.cshtml` | `Faible` | encore Bootstrap classique, hors systeme moderne |
| `Views/Account/Inscription.cshtml` | `Faible` | encore ancien et peu harmonise |
| `Views/AzureAuth/DemanderAcces.cshtml` | `Moyen` | propre, mais encore beaucoup de styles inline |

### Aide

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Aide/Index.cshtml` | `Faible` | gros ecran de contenu en Bootstrap classique |
| `Views/Aide/_GuideDemandeur.cshtml` | `Faible` | contenu utile mais presentation ancienne |
| `Views/Aide/_GuideDirecteurMetier.cshtml` | `Faible` | presentation ancienne |
| `Views/Aide/_GuideChefProjet.cshtml` | `Faible` | presentation ancienne |
| `Views/Aide/_GuideDSI.cshtml` | `Faible` | presentation ancienne et texte charge |
| `Views/Aide/_GuideAdminIT.cshtml` | `Faible` | presentation ancienne |
| `Views/Aide/_GuideResponsableSolutionsIT.cshtml` | `Faible` | presentation ancienne |

### Admin

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Admin/Users.cshtml` | `Fort` | bonne refonte, lisible, industrialisee |
| `Views/Admin/Directions.cshtml` | `Fort` | bien alignee avec le design system |
| `Views/Admin/Services.cshtml` | `Fort` | propre et coherente |
| `Views/Admin/Parametres.cshtml` | `Fort` | bonne lisibilite pour un ecran dense |
| `Views/Admin/ListeRoles.cshtml` | `Fort` | clair et moderne |
| `Views/Admin/GererRoles.cshtml` | `Fort` | propre et fonctionnel |
| `Views/Admin/Delegations.cshtml` | `Fort` | bon niveau visuel |
| `Views/Admin/ImportUsers.cshtml` | `Fort` | bien refondu |
| `Views/Admin/DelegationsChefProjet.cshtml` | `Moyen` | plutot propre mais encore mixte |
| `Views/Admin/_ModalDelegationDSI.cshtml` | `Moyen` | modal correcte, peu premium |
| `Views/Admin/_ModalDelegationChefProjet.cshtml` | `Moyen` | modal correcte, peu premium |
| `Views/Admin/DemandesCreationCompte.cshtml` | `Faible` | principal outlier admin, encore tres legacy |

### Autorisations / Notifications / Acces

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Autorisations/Index.cshtml` | `Fort` | bon cockpit admin, lisible et credible |
| `Views/Notification/Index.cshtml` | `Moyen` | base correcte mais ecran encore peu raffine |
| `Views/DemandesAcces/Index.cshtml` | `Moyen` | fonctionnel mais heterogene |

### DemandeProjet

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/DemandeProjet/Index.cshtml` | `Fort` | propre et bien structure |
| `Views/DemandeProjet/Details.cshtml` | `Fort` | une des meilleures vues du projet |
| `Views/DemandeProjet/ListeValidationDM.cshtml` | `Fort` | simple et bien alignee |
| `Views/DemandeProjet/ListeValidationDSI.cshtml` | `Fort` | simple et bien alignee |
| `Views/DemandeProjet/HistoriqueValidationsDSI.cshtml` | `Moyen` | correct mais encore un peu standard |
| `Views/DemandeProjet/HistoriqueActionsDM.cshtml` | `Moyen` | propre mais moins abouti |
| `Views/DemandeProjet/VerificationDoublons.cshtml` | `Moyen` | utile, mais encore dense |
| `Views/DemandeProjet/Create.cshtml` | `Moyen` | bien avance, mais beaucoup de styles inline et forte densite |
| `Views/DemandeProjet/Edit.cshtml` | `Moyen` | meme limite que `Create` |
| `Views/DemandeProjet/_ModalValiderDM.cshtml` | `Moyen` | modal fonctionnelle |
| `Views/DemandeProjet/_ModalValiderDSI.cshtml` | `Moyen` | modal fonctionnelle |
| `Views/DemandeProjet/_ModalRenvoyerDM.cshtml` | `Moyen` | modal fonctionnelle |
| `Views/DemandeProjet/_ModalRenvoyerDemandeur.cshtml` | `Moyen` | modal fonctionnelle |
| `Views/DemandeProjet/_ModalRejeterDM.cshtml` | `Moyen` | modal fonctionnelle |
| `Views/DemandeProjet/_ModalRejeterDSI.cshtml` | `Moyen` | modal fonctionnelle |
| `Views/DemandeProjet/_ModalCorrectionDM.cshtml` | `Moyen` | modal fonctionnelle |

### Projet

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Projet/Details.cshtml` | `Fort` | bonne page container, bonne navigation d'onglets |
| `Views/Projet/Portefeuille.cshtml` | `Fort` | tres bon niveau, lisible, exploitable |
| `Views/Projet/Charges.cshtml` | `Fort` | module bien structure |
| `Views/Projet/HistoriqueDM.cshtml` | `Fort` | bien refondu |
| `Views/Projet/Index.cshtml` | `Fort` | propre et stable |
| `Views/Projet/ValidationsProjet.cshtml` | `Fort` | bon ecran de validation |
| `Views/Projet/ListeValidationClotureDemandeur.cshtml` | `Fort` | simple et coherent |
| `Views/Projet/ListeValidationClotureDM.cshtml` | `Fort` | simple et coherent |
| `Views/Projet/ListeValidationClotureDSI.cshtml` | `Fort` | simple et coherent |
| `Views/Projet/SignatureCharte.cshtml` | `Moyen` | correct, mais encore peu raffine |
| `Views/Projet/FicheProjet.cshtml` | `Moyen` | tres riche mais trop lourd, beaucoup d'inline et de densite |
| `Views/Projet/CharteProjet.cshtml` | `Moyen` | tres complete mais surchargee visuellement |
| `Views/Projet/_ProjetSynthese.cshtml` | `Moyen` | utile mais trop dense |
| `Views/Projet/_ProjetAnalyse.cshtml` | `Moyen` | gros volume, nombreux blocs, beaucoup d'inline |
| `Views/Projet/_ProjetPlanification.cshtml` | `Moyen` | bonne base, encore lourde |
| `Views/Projet/_ProjetExecution.cshtml` | `Moyen` | bonne base, encore lourde |
| `Views/Projet/_ProjetUAT.cshtml` | `Moyen` | bonne base, encore lourde |
| `Views/Projet/_ProjetCloture.cshtml` | `Moyen` | bonne base, encore lourde |
| `Views/Projet/_ProjetCollaboration.cshtml` | `Moyen` | correct mais heterogene |
| `Views/Projet/_ProjetCasTests.cshtml` | `Moyen` | correct mais mixte |
| `Views/Projet/_ProjetHistorique.cshtml` | `Moyen` | utile, moins travaille |
| `Views/Projet/_DossierSignature.cshtml` | `Moyen` | fonctionnel mais charge |
| `Views/Projet/_ValidationCharte.cshtml` | `Moyen` | fonctionnel mais peu harmonise |
| `Views/Projet/_UploadLivrableModal.cshtml` | `Moyen` | modal correcte |
| `Views/Projet/_AjouterMembreModal.cshtml` | `Moyen` | modal correcte |
| `Views/Projet/_ModifierMembreModal.cshtml` | `Moyen` | modal correcte |
| `Views/Projet/_AjouterRisqueModal.cshtml` | `Moyen` | modal correcte |
| `Views/Projet/_ModifierRisqueModal.cshtml` | `Moyen` | modal correcte |
| `Views/Projet/_AjouterAnomalieModal.cshtml` | `Moyen` | modal correcte |

### Document

| Vue | Statut | Note |
| --- | --- | --- |
| `Views/Document/Preview.cshtml` | `Moyen` | utile mais encore tres inline |

## Problemes Transverses

1. Certaines vues restent hors design system moderne:
- `Account/DemandeAcces`
- `Account/Inscription`
- `Account/AccessDenied`
- `Admin/DemandesCreationCompte`
- l'ensemble du module `Aide`

2. Certaines grandes vues sont trop denses:
- `Projet/CharteProjet`
- `Projet/FicheProjet`
- `Projet/_ProjetAnalyse`
- `Projet/_ProjetSynthese`
- `Projet/_ProjetCloture`
- `Dashboard/Index`

3. Trop de styles inline dans plusieurs vues riches:
- `Projet/CharteProjet`
- `Projet/FicheProjet`
- `Projet/_ProjetAnalyse`
- `Projet/_ProjetSynthese`
- `Document/Preview`
- `Notification/Index`

4. Plusieurs vues de contenu affichent encore des chaines texte mal encodees ou du moins douteuses en lecture source, notamment dans:
- `Aide/Index`
- `Aide/_GuideDSI`
- `DemandesAcces/Index`
- certaines vues `Projet`

## Priorites de Refonte

### Lot 1 - priorite haute
- `Views/Admin/DemandesCreationCompte.cshtml`
- `Views/Account/AccessDenied.cshtml`
- `Views/Account/DemandeAcces.cshtml`
- `Views/Account/Inscription.cshtml`
- `Views/Aide/*.cshtml`

### Lot 2 - priorite forte
- `Views/Projet/CharteProjet.cshtml`
- `Views/Projet/FicheProjet.cshtml`
- `Views/Projet/_ProjetAnalyse.cshtml`
- `Views/Projet/_ProjetSynthese.cshtml`
- `Views/Dashboard/Index.cshtml`

### Lot 3 - priorite moyenne
- `Views/Projet/_ProjetExecution.cshtml`
- `Views/Projet/_ProjetPlanification.cshtml`
- `Views/Projet/_ProjetUAT.cshtml`
- `Views/Projet/_ProjetCloture.cshtml`
- `Views/Document/Preview.cshtml`
- `Views/Notification/Index.cshtml`

## Conclusion

Le projet n'est plus dans un etat "legacy global". Il possede deja:
- un shell moderne
- un dashboard principal fort
- des ecrans admin propres
- une fiche projet principale credible
- une matrice d'autorisations lisible

Mais il n'est pas encore "100% fini" cote UI/UX, car plusieurs zones restent:
- anciennes
- surchargees
- trop inline
- pas assez unifiees avec le design system principal
