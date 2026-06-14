using Microsoft.AspNetCore.Http;

namespace GestionProjects.Web.Ui;

public sealed record ScreenHelpContent(
    string Title,
    string Purpose,
    string HowTo,
    IReadOnlyList<string> Checklist,
    string? Attention = null);

public static class ScreenHelpCatalog
{
    private static readonly Dictionary<string, ScreenHelpContent> ExactPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Home/Index"] = Build(
            "Tableau de bord",
            "Cet écran donne une vue consolidée du portefeuille, des validations en attente, des alertes et des grands indicateurs de pilotage.",
            "Utilisez-le pour identifier les blocages, repérer les projets critiques et ouvrir les écrans de détail à partir des tableaux ou des liens d'action.",
            "Consultez d'abord les KPI en haut de page.",
            "Ouvrez ensuite les validations ou projets critiques qui demandent une action immédiate.",
            "Ne saisissez rien ici : cet écran sert d'aide à la décision et de navigation."
        ),
        ["Dashboard/Index"] = Build(
            "Analytics et reporting",
            "Cet écran présente des analyses transverses : tendances, répartitions, charges, santé du portefeuille et indicateurs consolidés.",
            "Filtrez la période ou le périmètre, puis utilisez les graphiques pour détecter les écarts et ouvrir le bon écran de traitement.",
            "Commencez par choisir la bonne période d'analyse.",
            "Comparez les indicateurs de charge, de budget et de risques.",
            "Utilisez les écarts pour déclencher des actions dans les modules projet."
        ),
        ["Notification/Index"] = Build(
            "Centre de notifications",
            "Cet écran centralise les messages générés par les workflows : validations attendues, changements d'état, alertes et rappels.",
            "Lisez les notifications, ouvrez l'écran cible si nécessaire, puis marquez-les comme lues une fois l'action traitée.",
            "Traitez d'abord les notifications liées aux validations.",
            "Ouvrez les projets ou demandes concernés avant de marquer comme lu.",
            "Utilisez le marquage global seulement après vérification."
        ),
        ["Aide/Index"] = Build(
            "Centre d'aide",
            "Cette page regroupe les règles métier, les parcours par rôle, les points de contrôle et les réponses aux questions fréquentes.",
            "Utilisez-la comme référence si vous hésitez sur l'ordre des actions, le sens d'un statut ou les prérequis d'un passage de phase.",
            "Cherchez d'abord votre rôle ou votre phase projet.",
            "Vérifiez les prérequis listés avant de valider une étape.",
            "Revenez sur l'écran opérationnel une fois la règle comprise."
        ),
        ["Autorisations/Index"] = Build(
            "Autorisations et droits",
            "Cette page pilote les accès de l'application par rôle. Elle ne sert pas à gérer un utilisateur au cas par cas, mais à définir les capacités d'un rôle entier.",
            "Choisissez le rôle, activez ou désactivez les écrans souhaités, puis vérifiez l'effet sur le menu et l'accès réel après rechargement.",
            "Modifiez les droits rôle par rôle.",
            "Réinitialisez si vous voulez revenir au standard par défaut.",
            "Gardez en tête que AdminIT reste un superutilisateur transversal."
        ),
        ["Account/Login"] = Build(
            "Connexion",
            "Cette page permet d'entrer dans l'application avec votre compte professionnel et votre profil métier associé.",
            "Renseignez vos identifiants ou utilisez la méthode de connexion proposée, puis vérifiez après connexion que le menu correspond bien à votre rôle.",
            "Connectez-vous avec le bon compte métier.",
            "Contrôlez ensuite le rôle affiché dans l'entête.",
            "En cas d'accès refusé, utilisez l'écran de demande d'accès."
        ),
        ["Account/Profil"] = Build(
            "Profil utilisateur",
            "Cette page récapitule votre identité, vos rôles, votre périmètre d'action et vos informations de sécurité ou de traçabilité.",
            "Utilisez-la pour vérifier quel rôle est actif et si votre nom ou votre périmètre sont correctement associés aux workflows.",
            "Vérifiez d'abord le rôle affiché.",
            "Confirmez ensuite vos rattachements métier ou projet.",
            "Signalez toute incohérence à l'administration."
        ),
        ["Account/Inscription"] = Build(
            "Demande de création de compte",
            "Cette page sert à créer une demande d'accès initiale pour un nouvel utilisateur de l'application.",
            "Renseignez l'identité, le rôle attendu et le périmètre fonctionnel avec précision pour faciliter la validation administrative.",
            "Saisissez l'identité complète de l'utilisateur.",
            "Choisissez le bon rôle métier.",
            "Ajoutez les éléments de justification si la page le demande."
        ),
        ["Account/DemandeAcces"] = Build(
            "Demande d'accès",
            "Cette page permet de solliciter un accès complémentaire ou une ouverture de droits à l'application.",
            "Décrivez clairement le besoin, le rôle demandé et le contexte d'utilisation pour accélérer le traitement par l'administration.",
            "Expliquez le besoin métier.",
            "Choisissez le bon rôle ou périmètre d'accès.",
            "Ajoutez les justifications si elles sont demandées."
        ),
        ["DemandeProjet/Create"] = Build(
            "Nouvelle demande projet",
            "Cet écran sert à formaliser le besoin initial : contexte, objectifs, direction, sponsor, priorité et pièces de cadrage.",
            "Renseignez la demande avec des informations claires et exploitables, car elles serviront ensuite de base à l'analyse et au futur projet.",
            "Décrivez le besoin de manière compréhensible et factuelle.",
            "Précisez le sponsor, la direction et les attentes métier.",
            "Ajoutez les pièces utiles si la page propose un dépôt documentaire."
        ),
        ["DemandeProjet/Edit"] = Build(
            "Modification d'une demande",
            "Cette page permet de corriger ou compléter une demande avant validation, après retour ou à la demande d'un validateur.",
            "Mettez à jour uniquement les éléments demandés, puis enregistrez pour relancer le circuit ou lever le blocage identifié.",
            "Relisez les commentaires de retour avant de modifier.",
            "Corrigez les champs réellement concernés.",
            "Enregistrez puis contrôlez le nouveau statut de la demande."
        ),
        ["DemandeProjet/HistoriqueActionsDM"] = Build(
            "Historique des actions DM",
            "Cet écran retrace les décisions et retours intervenus sur les demandes côté Directeur Métier.",
            "Utilisez-le pour comprendre qui a fait quoi, quand et pourquoi, notamment en cas de demande renvoyée ou rejetée.",
            "Consultez les dernières actions en priorité.",
            "Ouvrez la demande concernée si vous devez compléter ou corriger.",
            "Servez-vous de l'historique comme piste d'audit."
        ),
        ["Projet/Portefeuille"] = Build(
            "Portefeuille projets",
            "Cet écran donne une vision consolidée de tous les projets du périmètre : état, phase, avancement, charge, budget et risques.",
            "Filtrez le portefeuille, comparez les projets et ouvrez chaque fiche pour traiter les écarts ou les validations en attente.",
            "Utilisez les filtres avant d'analyser les lignes.",
            "Priorisez les projets en retard, rouges ou bloqués.",
            "Passez dans la fiche projet pour agir."
        ),
        ["Projet/HistoriqueDM"] = Build(
            "Historique et traçabilité",
            "Cette page regroupe l'historique détaillé des projets du périmètre métier, avec les phases, validations, alertes et audits.",
            "Ouvrez chaque projet repliable pour lire la chronologie, identifier les blocages et retrouver les décisions passées.",
            "Dépliez d'abord les projets critiques ou récents.",
            "Lisez les jalons et validations avant de demander un changement.",
            "Utilisez cet écran comme journal de référence."
        ),
        ["Projet/ValidationsProjet"] = Build(
            "Validations de charte projet",
            "Cette page sert au Directeur Métier puis à la DSI à valider la charte de projet en phase Analyse, avant le passage en Planification.",
            "Le Directeur Métier ne peut valider que si le dossier de charte est complet. La DSI ou le RSIT délégué interviennent ensuite.",
            "Vérifiez d'abord le statut 'dossier complet'.",
            "Utilisez l'icône de consultation pour relire la charte ou le projet si besoin.",
            "Validez seulement après contrôle des signatures et de la cohérence du dossier."
        ),
        ["Projet/Charges"] = Build(
            "Charges et capacité",
            "Cet écran sert à saisir, comparer et contrôler les charges prévues et réelles par ressource et par semaine.",
            "Renseignez les prévisions, les réalisés et les commentaires de charge, puis utilisez les alertes pour repérer les surcharges ou les manques de saisie.",
            "Complétez les lignes par ressource et par semaine.",
            "Vérifiez les écarts entre prévu et réel.",
            "Traitez les alertes avant la validation hebdomadaire."
        ),
        ["Projet/CharteProjet"] = Build(
            "Charte projet",
            "Cet écran sert à rédiger, compléter, générer et suivre la charte projet, puis à gérer sa version signée.",
            "Complétez la charte en phase Analyse, générez le document, déposez la version signée et enregistrez séparément les signatures si nécessaire.",
            "Complétez les rubriques métier et de gouvernance.",
            "Générez ensuite la charte officielle.",
            "Déposez la version signée et vérifiez les signatures Sponsor et Chef de Projet."
        ),
        ["Projet/ListeValidationClotureDM"] = Build(
            "Validations de clôture DM",
            "Cette page sert au Directeur Métier à valider les clôtures déjà acceptées par le demandeur et prêtes à son contrôle.",
            "Relisez le bilan, les livrables et le transfert RUN avant de valider ou de refuser la clôture.",
            "Vérifiez le bilan projet et les leçons apprises.",
            "Contrôlez l'existence des éléments de transfert RUN.",
            "Validez seulement si la clôture est réellement exploitable."
        ),
        ["Projet/ListeValidationClotureDemandeur"] = Build(
            "Validations de clôture demandeur",
            "Cette page permet au demandeur de confirmer que le besoin a été livré et que la clôture peut poursuivre son circuit.",
            "Vérifiez que la solution livrée correspond au besoin exprimé, puis validez ou signalez l'écart avant de laisser poursuivre la clôture.",
            "Contrôlez le périmètre livré.",
            "Lisez le bilan si la page le présente.",
            "Validez seulement si le résultat est conforme."
        )
    };

    private static readonly Dictionary<string, ScreenHelpContent> ControllerDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = Build(
            "Administration",
            "Ces écrans servent à paramétrer les utilisateurs, rôles, services, directions, délégations et référentiels de l'application.",
            "Modifiez les référentiels avec prudence, car ils influencent les menus, les validations et les circuits métier.",
            "Vérifiez toujours le périmètre avant d'enregistrer.",
            "Évitez les doublons dans les référentiels.",
            "Contrôlez ensuite l'effet sur les utilisateurs concernés."
        ),
        ["DemandeProjet"] = Build(
            "Gestion des demandes projet",
            "Ces écrans servent à créer, suivre, corriger et valider les demandes qui précèdent la création d'un projet.",
            "Renseignez les champs métier avec précision et suivez le statut pour savoir si une action de votre part est attendue.",
            "Lisez le statut de la demande avant d'agir.",
            "Corrigez ou complétez seulement les informations utiles au validateur.",
            "Ajoutez les pièces de cadrage si elles sont demandées."
        ),
        ["Projet"] = Build(
            "Gestion de projet",
            "Ces écrans couvrent tout le cycle du projet : analyse, planification, exécution, recette, clôture, charges et validations associées.",
            "Travaillez phase par phase, renseignez les preuves attendues, puis laissez les validations métier et DSI débloquer la phase suivante.",
            "Contrôlez d'abord la phase actuelle du projet.",
            "Renseignez les blocs de la phase avant de chercher à passer à la suivante.",
            "Utilisez les alertes et checklists comme contrôles de complétude."
        ),
        ["Notification"] = Build(
            "Notifications",
            "Ces écrans centralisent les événements et alertes envoyés par l'application.",
            "Utilisez-les comme file de travail personnelle pour ne pas manquer une validation ou un blocage.",
            "Traitez d'abord les notifications non lues.",
            "Ouvrez l'écran cible pour agir.",
            "Marquez ensuite la notification comme lue."
        ),
        ["Document"] = Build(
            "Prévisualisation documentaire",
            "Ces écrans servent à consulter les documents générés ou déposés dans le cadre des projets et des demandes.",
            "Utilisez la prévisualisation pour contrôler le contenu avant validation ou téléchargement.",
            "Vérifiez que le document correspond au bon projet.",
            "Contrôlez le type de document attendu.",
            "Téléchargez seulement si vous avez besoin d'une copie locale."
        )
    };

    public static ScreenHelpContent Resolve(HttpContext httpContext, string? pageTitle = null)
    {
        var controller = httpContext.Request.RouteValues.TryGetValue("controller", out var controllerValue)
            ? controllerValue?.ToString()
            : null;
        var action = httpContext.Request.RouteValues.TryGetValue("action", out var actionValue)
            ? actionValue?.ToString()
            : null;
        var tab = httpContext.Request.Query["tab"].ToString();

        if (string.Equals(controller, "Projet", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "Details", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveProjectTabHelp(tab);
        }

        var key = $"{controller}/{action}";
        if (ExactPages.TryGetValue(key, out var exact))
        {
            return exact;
        }

        if (!string.IsNullOrWhiteSpace(controller) && ControllerDefaults.TryGetValue(controller, out var fallback))
        {
            return fallback;
        }

        var title = string.IsNullOrWhiteSpace(pageTitle) ? "Aide écran" : pageTitle!;
        return Build(
            title,
            "Cet écran fait partie du parcours métier de l'application. Il sert à consulter, renseigner ou valider des données selon votre rôle.",
            "Lisez le titre et le statut affichés, puis complétez uniquement les champs demandés avant d'enregistrer ou de lancer une validation.",
            "Identifiez d'abord le rôle attendu sur cet écran.",
            "Renseignez les informations visibles comme obligatoires ou utiles au workflow.",
            "Contrôlez enfin le message de succès, d'erreur ou le statut affiché."
        );
    }

    private static ScreenHelpContent ResolveProjectTabHelp(string? tab)
    {
        return (tab ?? string.Empty).ToLowerInvariant() switch
        {
            "analyse" => Build(
                "Analyse et clarification",
                "Cet onglet sert à cadrer le besoin, préparer la charte, charger les pièces d'analyse et sécuriser les validations DM et DSI.",
                "Complétez le contexte, les objectifs, les livrables d'analyse, puis passez par la charte projet pour générer et déposer les pièces signées avant validation.",
                "Ajoutez les documents d'analyse obligatoires.",
                "Complétez puis générez la charte projet.",
                "Déposez la charte signée et obtenez les validations DM puis DSI."
            ),
            "planification" => Build(
                "Planification et validation",
                "Cet onglet sert à construire le planning de référence du projet et à préparer le dossier qui sera validé par le DM puis la DSI.",
                "Renseignez d'abord le planning interactif, puis les blocs RACI, communication, budget et kick-off. Enregistrez pour générer automatiquement les livrables natifs.",
                "Ajoutez au moins une tâche avant de générer Planning détaillé et WBS.",
                "Complétez la matrice RACI, le plan de communication, le budget et le PV de kick-off.",
                "Vérifiez que toute la checklist est à OK avant de demander les validations."
            ),
            "execution" => Build(
                "Exécution et suivi",
                "Cet onglet sert à piloter l'avancement opérationnel, les actions, les charges, les risques, les anomalies et les livrables d'exécution.",
                "L'avancement est recalculé automatiquement. Renseignez les actions menées, les dates réelles, les commentaires, les anomalies et les livrables pour refléter la réalité du projet.",
                "Mettez à jour régulièrement les actions réalisées et à venir.",
                "Contrôlez les charges et les écarts de budget.",
                "Préparez ensuite le passage en UAT quand le projet est prêt."
            ),
            "uat" => Build(
                "UAT, MEP et hypercare",
                "Cet onglet sert à préparer la recette, piloter les tests, organiser la MEP et suivre la période d'hypercare.",
                "Renseignez les campagnes, les résultats de recette, les données de changement et de MEP, puis les incidents et actions d'hypercare jusqu'à stabilisation.",
                "Complétez d'abord les éléments de recette et les cas de test.",
                "Validez la recette avant la MEP.",
                "Suivez ensuite l'hypercare jusqu'à la stabilisation finale."
            ),
            "collaboration" => Build(
                "Collaboration projet",
                "Cet onglet sert à suivre les acteurs, décisions, échanges, documents de travail et coordination transverse autour du projet.",
                "Renseignez les participants, les besoins de collaboration et les traces utiles pour faciliter le travail collectif et l'audit.",
                "Mettez à jour les acteurs et leurs rôles.",
                "Consignez les décisions importantes.",
                "Utilisez cet espace comme mémoire collaborative du projet."
            ),
            "cloture" => Build(
                "Clôture et capitalisation",
                "Cet onglet sert à formaliser le bilan du projet, préparer le transfert RUN et obtenir les validations finales de clôture.",
                "Renseignez le bilan, les retours, le statut final et tous les éléments de transfert avant de lancer la demande de clôture.",
                "Complétez le bilan fonctionnel et projet.",
                "Vérifiez le transfert RUN et la documentation.",
                "Lancez ensuite le circuit de validation finale."
            ),
            "historique" => Build(
                "Historique projet",
                "Cet onglet retrace l'ensemble des événements, validations, changements d'état et actions d'audit du projet.",
                "Utilisez-le pour comprendre le passé du projet, retrouver une décision ou prouver le respect d'un workflow.",
                "Lisez les derniers événements en priorité.",
                "Servez-vous des dates et utilisateurs comme traces d'audit.",
                "Revenez sur l'onglet opérationnel correspondant si une action est encore nécessaire."
            ),
            _ => Build(
                "Fiche projet",
                "Cette page sert de point d'entrée central vers toutes les phases du projet : analyse, planification, exécution, recette et clôture.",
                "Utilisez les onglets pour travailler la phase courante. Suivez les alertes, les validations et les checklists pour savoir ce qui manque.",
                "Commencez par vérifier la phase et le statut du projet.",
                "Ouvrez ensuite l'onglet qui correspond à l'action attendue.",
                "Tenez compte des alertes avant de lancer une transition de phase."
            )
        };
    }

    private static ScreenHelpContent Build(
        string title,
        string purpose,
        string howTo,
        params string[] checklist)
    {
        return new ScreenHelpContent(title, purpose, howTo, checklist);
    }
}
