(function () {
    const titleSelectors = [
        ".topbar-title",
        ".page-hero-title",
        ".project-hero-title",
        ".validation-hero-title",
        ".review-hero-title",
        ".document-hero-title",
        ".preview-hero-title",
        ".phase-title",
        ".phase-panel-title",
        ".card-title-modern",
        ".phase-card-title",
        ".dashboard-section-title",
        ".admin-form-section-title",
        ".auth-premium-title",
        ".profile-section-title",
        ".auth-premium-copy h1"
    ].join(", ");

    const routeHelpCatalog = [
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "synthese",
            title: "Synthèse projet",
            purpose: "Cet écran sert de cockpit de pilotage du projet. Il centralise l'état global, l'avancement, les risques et les informations clés avant d'ouvrir un onglet métier plus détaillé.",
            guidance: "Contrôlez d'abord la phase et le statut. Utilisez ensuite les blocs d'information, les risques et les onglets pour compléter ou corriger les données métier. Le pourcentage d'avancement est maintenant recalculé automatiquement."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "analyse",
            title: "Analyse & Clarification",
            purpose: "Cet écran sert à cadrer le besoin, déposer les pièces d'analyse, produire la charte et préparer la validation DM puis DSI avant le passage en planification.",
            guidance: "Renseignez le contexte et les objectifs, chargez les livrables obligatoires, générez la charte puis déposez la version signée avant de demander la validation. Ne validez la phase que lorsque les alertes de blocage ont disparu."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "planification",
            title: "Planification & Validation",
            purpose: "Cet écran sert à construire le planning de référence, les jalons, la RACI, la communication, le budget et le PV de kick-off avant la validation DM puis DSI.",
            guidance: "Commencez par ajouter les tâches dans le planning interactif, enregistrez, puis complétez les blocs RACI, communication, budget et kick-off. Vérifiez la checklist des livrables obligatoires avant de solliciter les validations."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "execution",
            title: "Exécution & Suivi",
            purpose: "Cet écran sert au pilotage opérationnel du projet en cours : avancement, budget, charge, risques, anomalies, actions et préparation UAT.",
            guidance: "Mettez à jour les faits réalisés, les points bloquants, les risques et les livrables. L'avancement est calculé à partir des tâches de planning et des données d'exécution."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "uat",
            title: "UAT & MEP",
            purpose: "Cet écran sert à préparer et piloter la recette, la mise en production, les incidents de changement et la période d'hypercare.",
            guidance: "Renseignez les informations de recette, chargez les preuves, enregistrez les anomalies et confirmez la MEP lorsque les prérequis sont complets."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "cloture",
            title: "Clôture projet",
            purpose: "Cet écran sert à consolider le bilan, transférer le projet au RUN et faire passer la demande de clôture dans le circuit de validation final.",
            guidance: "Complétez le bilan, les retours d'expérience, le transfert RUN et le statut final. Vérifiez ensuite le workflow Demandeur, DM et DSI jusqu'à obtention de la clôture complète."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "collaboration",
            title: "Collaboration projet",
            purpose: "Cet écran sert à suivre les membres, la coordination et les échanges transverses autour du projet.",
            guidance: "Ajoutez les membres utiles, mettez à jour les responsabilités et consultez les interactions pour maintenir une gouvernance claire."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "historique",
            title: "Historique projet",
            purpose: "Cet écran sert à retracer les événements, décisions et changements majeurs du projet.",
            guidance: "Utilisez l'historique comme piste d'audit. Vérifiez les événements clés avant une validation ou une clôture."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "CharteProjet",
            title: "Charte projet",
            purpose: "Cet écran sert à construire la charte, la faire signer, suivre le dossier de signature et sécuriser les validations de la phase Analyse.",
            guidance: "Complétez la charte, générez le document, déposez la version signée puis enregistrez les signatures Sponsor et Chef de Projet. Toute modification majeure peut réinitialiser les validations de charte."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Charges",
            title: "Charges & Capacité",
            purpose: "Cet écran sert à piloter l'effort prévu et réel par ressource, par semaine et par projet.",
            guidance: "Renseignez les charges prévues et réalisées, ajoutez les commentaires utiles puis vérifiez les alertes de surcharge avant les arbitrages de capacité. Une ressource qui n'a pas saisi sa charge de la semaine reçoit un rappel automatique chaque jeudi."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "avenants",
            title: "Avenants projet",
            purpose: "Cet écran sert à formaliser un changement maîtrisé de budget, de délai ou de périmètre après que la baseline a été posée.",
            guidance: "Créez l'avenant avec une justification claire (type Périmètre/Budget/Délai/Mixte), faites-le valider par le Directeur Métier puis par la DSI : c'est cette dernière validation qui applique réellement le changement au projet. Un avenant est automatiquement suggéré chaque lundi si l'écart budgétaire dépasse 15% ou le retard dépasse 15 jours."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Details" && ctx.tab === "benefices",
            title: "Bénéfices projet",
            purpose: "Cet écran sert à suivre la valeur métier attendue du projet (indicateur, valeur cible) puis à l'évaluer après mise en production.",
            guidance: "Définissez chaque bénéfice avec un indicateur mesurable et une date cible. Après la date cible, évaluez la valeur réellement obtenue (Réalisé / Partiellement réalisé / Non réalisé) : un rappel automatique est envoyé au chef de projet le jour de l'échéance."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "Portefeuille",
            title: "Portefeuille projets",
            purpose: "Cet écran sert à piloter l'ensemble du portefeuille projet et à lancer les exports de suivi.",
            guidance: "Utilisez les filtres, consultez les statuts consolidés et lancez les exports PDF ou Excel selon la vue attendue par la DSI ou le management."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "ValidationsProjet",
            title: "Validations projet",
            purpose: "Cet écran sert à faire valider la charte projet par le Directeur Métier puis par la DSI ou le RSIT délégué avant le passage en planification.",
            guidance: "Vérifiez que le dossier de charte est complet, utilisez l'oeil pour contrôler le projet et n'actionnez les validations que lorsque la version signée et les signatures requises sont présentes."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "HistoriqueDM",
            title: "Historique & Traçabilité",
            purpose: "Cet écran sert à consulter une vue consolidée des projets de direction et leur historique de gouvernance.",
            guidance: "Ouvrez les fiches repliables pour lire la chronologie, les risques et les validations. Servez-vous de cet écran comme vue d'audit et de contrôle."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "ListeValidationClotureDM",
            title: "Validations clôture DM",
            purpose: "Cet écran sert au Directeur Métier pour traiter les demandes de clôture déjà validées par le Demandeur.",
            guidance: "Ouvrez chaque dossier, contrôlez le bilan et le transfert RUN, puis validez ou refusez la clôture selon les preuves disponibles."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "ListeValidationClotureDSI",
            title: "Validations clôture DSI",
            purpose: "Cet écran sert à la DSI ou à son délégué pour la validation finale des clôtures projet.",
            guidance: "Vérifiez le parcours complet de clôture, les livrables finaux et la cohérence RUN avant de prononcer la clôture définitive."
        },
        {
            match: (ctx) => ctx.controller === "Projet" && ctx.action === "ListeValidationClotureDemandeur",
            title: "Validations clôture Demandeur",
            purpose: "Cet écran sert au Demandeur pour confirmer que le projet est bien livré et peut entrer en clôture.",
            guidance: "Relisez le bilan et les éléments de remise, puis confirmez uniquement si les attentes métier sont satisfaites."
        },
        {
            match: (ctx) => ctx.controller === "DemandeProjet" && ctx.action === "Create",
            title: "Nouvelle demande de projet",
            purpose: "Cet écran sert à formuler un nouveau besoin projet avant son passage dans le workflow DM puis DSI.",
            guidance: "Renseignez le besoin, le contexte, les objectifs, la gouvernance métier et joignez les pièces demandées. Utilisez le brouillon si toutes les informations ne sont pas encore disponibles."
        },
        {
            match: (ctx) => ctx.controller === "DemandeProjet" && ctx.action === "Edit",
            title: "Modification de la demande",
            purpose: "Cet écran sert à corriger ou enrichir une demande existante avant une nouvelle soumission ou après retour de validation.",
            guidance: "Mettez à jour uniquement les éléments demandés, rechargez les documents si nécessaire puis resoumettez la demande."
        },
        {
            match: (ctx) => ctx.controller === "DemandeProjet" && ctx.action === "Details",
            title: "Détail de la demande",
            purpose: "Cet écran sert à consulter l'ensemble des informations, décisions et pièces associées à une demande projet.",
            guidance: "Parcourez les blocs de synthèse, les validations, les documents et l'historique pour comprendre le cycle complet de la demande."
        },
        {
            match: (ctx) => ctx.controller === "DemandeProjet" && ctx.action === "Index",
            title: "Mes demandes",
            purpose: "Cet écran sert au Demandeur à suivre ses demandes, leur statut et leurs actions possibles.",
            guidance: "Utilisez les filtres, ouvrez les détails et déclenchez les modifications ou les compléments depuis les actions disponibles."
        },
        {
            match: (ctx) => ctx.controller === "DemandeProjet" && ctx.action === "ListeValidationDM",
            title: "Validations DM",
            purpose: "Cet écran sert au Directeur Métier pour traiter les demandes en attente de validation métier.",
            guidance: "Contrôlez le besoin, la gouvernance et les pièces jointes, puis validez, corrigez ou rejetez selon la qualité du dossier."
        },
        {
            match: (ctx) => ctx.controller === "DemandeProjet" && ctx.action === "ListeValidationDSI",
            title: "Validations DSI",
            purpose: "Cet écran sert à la DSI ou au RSIT délégué pour traiter les demandes validées par le métier.",
            guidance: "Vérifiez la faisabilité, les priorités et la complétude du dossier avant de valider, renvoyer ou rejeter la demande."
        },
        {
            match: (ctx) => ctx.controller === "Notification" && ctx.action === "Index",
            title: "Notifications",
            purpose: "Cet écran sert à centraliser les alertes, validations en attente et événements importants liés à vos projets.",
            guidance: "Consultez les notifications non lues en priorité, ouvrez les éléments concernés puis marquez-les comme lues une fois traitées."
        },
        {
            match: (ctx) => ctx.controller === "Autorisations" && ctx.action === "Index",
            title: "Autorisations et droits",
            purpose: "Cet écran sert à piloter les permissions par rôle sur les vues et actions principales de l'application.",
            guidance: "Sélectionnez un rôle, activez ou désactivez les accès nécessaires puis vérifiez l'effet métier sur le menu et les autorisations réelles."
        },
        {
            match: (ctx) => ctx.controller === "Home" && ctx.action === "Index",
            title: "Tableau de bord",
            purpose: "Cet écran sert de cockpit de pilotage transverse pour les projets, la gouvernance, les risques, la capacité et le budget.",
            guidance: "Lisez les KPI prioritaires, utilisez les tableaux et graphiques pour identifier les alertes, puis ouvrez les écrans détaillés si un arbitrage est nécessaire."
        },
        {
            match: (ctx) => ctx.controller === "Dashboard" && ctx.action === "Index",
            title: "Analytics",
            purpose: "Cet écran sert à lire des indicateurs consolidés et des analyses comparatives sur le portefeuille projet.",
            guidance: "Utilisez cette vue pour comprendre les tendances, puis revenez dans les écrans de pilotage pour corriger les écarts observés."
        },
        {
            match: (ctx) => ctx.controller === "Aide" && ctx.action === "Index",
            title: "Centre d'aide",
            purpose: "Cet écran sert à retrouver les guides d'usage par rôle et les bonnes pratiques d'utilisation de l'application.",
            guidance: "Choisissez votre rôle ou votre besoin, puis utilisez les sections du guide comme référence pendant la saisie ou la validation."
        },
        {
            match: (ctx) => ctx.controller === "Account" && ctx.action === "Profil",
            title: "Mon profil",
            purpose: "Cet écran sert à consulter les informations de compte, les rôles et certains éléments de sécurité de l'utilisateur connecté.",
            guidance: "Vérifiez vos données, vos rôles actifs et les éléments de contexte avant de signaler un problème d'accès."
        },
        {
            match: (ctx) => ctx.controller === "Account" && ctx.action === "Login",
            title: "Connexion",
            purpose: "Cet écran sert à accéder à la plateforme selon la méthode d'authentification disponible sur l'environnement.",
            guidance: "Utilisez en priorité l'accès Microsoft si Azure AD est configuré. En environnement local, utilisez la connexion interne si elle est autorisée."
        },
        {
            match: (ctx) => ctx.controller === "Admin",
            title: "Administration",
            purpose: "Cet écran sert à paramétrer les référentiels, les utilisateurs, les délégations et les règles d'administration de la plateforme.",
            guidance: "Renseignez les formulaires avec prudence, contrôlez les impacts fonctionnels puis enregistrez. Une modification d'administration affecte potentiellement plusieurs workflows métier."
        }
    ];

    const sectionHelpCatalog = {
        "informations generales": {
            purpose: "Ce bloc présente ou collecte les données de référence du dossier courant : identité, contexte, gouvernance ou métadonnées essentielles.",
            guidance: "Renseignez d'abord les champs obligatoires et gardez ces informations cohérentes avec les documents joints et les validations attendues."
        },
        "pilotage du statut": {
            purpose: "Ce bloc sert à mettre à jour l'état métier du projet tout en laissant l'avancement se recalculer automatiquement.",
            guidance: "Choisissez le bon statut de pilotage, enregistrez puis vérifiez que l'avancement recalculé correspond bien à la réalité des livrables et validations."
        },
        "mise a jour avancement": {
            purpose: "Ce bloc sert au pilotage de la situation courante du projet et à la mise à jour de son état métier.",
            guidance: "L'avancement étant désormais automatique, concentrez-vous sur le statut et sur la qualité des informations renseignées dans les autres onglets."
        },
        "risques prioritaires": {
            purpose: "Ce bloc sert à suivre les risques les plus sensibles pour le projet afin d'anticiper les écarts de délai, coût ou qualité.",
            guidance: "Ajoutez un risque par menace réelle, précisez la probabilité, l'impact, le plan d'action et maintenez le statut à jour."
        },
        "documents d analyse obligatoires": {
            purpose: "Ce bloc liste les pièces minimales attendues pour cadrer la phase d'analyse et soutenir la charte projet.",
            guidance: "Chargez le bon document dans le bon emplacement, contrôlez la version déposée puis remplacez-la si le cadrage évolue."
        },
        "autres livrables d analyse": {
            purpose: "Ce bloc sert à archiver les pièces complémentaires utiles au cadrage et à la clarification du besoin.",
            guidance: "Ajoutez ici les documents de support qui ne correspondent pas aux deux emplacements obligatoires, en privilégiant des noms de fichier explicites."
        },
        "analyse technique continue": {
            purpose: "Ce bloc sert à documenter les constats techniques, hypothèses et points d'attention apparus pendant l'analyse.",
            guidance: "Saisissez des notes courtes mais actionnables, mettez-les à jour au fil du cadrage et veillez à leur cohérence avec la charte."
        },
        "validation directeur metier": {
            purpose: "Ce bloc sert au Directeur Métier pour confirmer que le dossier est conforme aux attentes du métier.",
            guidance: "Ne validez qu'après contrôle des livrables, du planning ou de la charte selon la phase. Utilisez le refus si un point structurant manque."
        },
        "evaluation de l equipe": {
            purpose: "Ce bloc sert à évaluer chaque membre actif de l'équipe projet (qualité du travail, respect des délais, collaboration) typiquement à la clôture.",
            guidance: "Notez chaque critère de 1 à 5 et ajoutez un commentaire si utile, dans l'onglet Clôture. Une seule évaluation par membre : la noter à nouveau met à jour l'évaluation existante."
        },
        "validation dsi": {
            purpose: "Ce bloc sert à la DSI ou à son délégué pour prononcer la validation technique ou de gouvernance finale de la phase.",
            guidance: "Vérifiez les prérequis métier, les livrables obligatoires et les éléments de faisabilité avant de faire passer la phase suivante."
        },
        "checklist livrables obligatoires": {
            purpose: "Ce bloc sert de contrôle de complétude : il indique quels livrables officiels existent déjà et lesquels restent à produire.",
            guidance: "Traitez en priorité les éléments marqués manquants. La phase ne doit pas être validée tant que la checklist n'est pas entièrement au vert."
        },
        "planning interactif": {
            purpose: "Ce bloc sert à construire le planning vivant du projet directement dans l'application, avec tâches, jalons et responsables.",
            guidance: "Ajoutez les tâches dans l'ordre logique du projet, renseignez les dates et les responsables, puis enregistrez avant de générer les livrables officiels."
        },
        "editeur du planning": {
            purpose: "Ce bloc sert à saisir chaque tâche, son WBS, sa durée, son avancement et ses dépendances.",
            guidance: "Ajoutez au moins une tâche exploitable, contrôlez les dates et utilisez le commentaire pour les précisions de planification utiles."
        },
        "planning & jalons": {
            purpose: "Ce bloc sert à mettre en évidence le prochain jalon et la séquence de jalons qui balisent la phase de planification.",
            guidance: "Renseignez les jalons majeurs du projet avec des formulations courtes et datables. Le prochain jalon doit rester réaliste et actionnable."
        },
        "wbs / decoupage / raci": {
            purpose: "Ce bloc sert à décrire le découpage du travail, les responsabilités et la structure de gouvernance associée.",
            guidance: "Présentez le WBS par lots ou par domaines et veillez à ce que chaque activité ait une responsabilité claire."
        },
        "matrice raci": {
            purpose: "Ce bloc sert à formaliser qui est Responsable, Approbateur, Consulté ou Informé pour chaque activité majeure.",
            guidance: "Ajoutez une ligne par activité, gardez un seul Approbateur par ligne si possible et évitez les matrices ambiguës."
        },
        "communication & gouvernance": {
            purpose: "Ce bloc sert à cadrer le rythme des instances projet, les circuits de communication et la gouvernance opérationnelle.",
            guidance: "Définissez des instances réalistes, précisez la fréquence et les participants puis vérifiez que les canaux correspondent aux pratiques de l'équipe."
        },
        "plan de communication": {
            purpose: "Ce bloc sert à détailler chaque instance de communication projet avec son objectif, son canal et son responsable.",
            guidance: "Ajoutez une ligne par rituel ou par comité, décrivez l'objectif de façon utile et gardez des participants explicites."
        },
        "budget natif & risques initiaux": {
            purpose: "Ce bloc sert à formaliser le budget prévisionnel et les principaux risques financiers ou organisationnels de la phase.",
            guidance: "Décomposez le budget par poste significatif et consignez les hypothèses ou limites dans les commentaires."
        },
        "budget previsionnel detaille": {
            purpose: "Ce bloc sert à détailler les postes budgétaires qui composeront le livrable officiel de budget prévisionnel.",
            guidance: "Ajoutez une ligne par poste de coût et utilisez le commentaire pour justifier les hypothèses de chiffrage."
        },
        "pv de kick-off natif": {
            purpose: "Ce bloc sert à construire le compte-rendu officiel de lancement du projet directement dans l'application.",
            guidance: "Renseignez la réunion, les participants, les décisions et les actions issues du kick-off afin que le document généré soit exploitable sans reprise manuelle."
        },
        "pilotage d avancement": {
            purpose: "Ce bloc sert à suivre le déroulement de l'exécution au travers des informations réellement produites par l'équipe.",
            guidance: "Concentrez-vous sur les faits réalisés, les prochaines actions et les écarts. L'avancement global se recalcule automatiquement."
        },
        "actions risques et decisions": {
            purpose: "Ce bloc sert à enregistrer les actions menées, les blocages et les décisions de pilotage prises pendant l'exécution.",
            guidance: "Privilégiez des formulations factuelles avec une suite d'action claire. Chaque blocage doit avoir un propriétaire ou un arbitrage associé."
        },
        "budget & charge": {
            purpose: "Ce bloc sert à comparer le prévu et le réel en budget comme en charge, afin d'identifier les dérives le plus tôt possible.",
            guidance: "Mettez à jour les consommés, les justifications et les synthèses de charge avec des données consolidées."
        },
        "taches / actions projet": {
            purpose: "Ce bloc sert à lister ou suivre les actions opérationnelles du projet pendant l'exécution.",
            guidance: "Découpez en actions simples, attribuez des responsables clairs et mettez à jour l'état au fur et à mesure."
        },
        "livrables d execution": {
            purpose: "Ce bloc sert à suivre les livrables produits pendant l'exécution du projet.",
            guidance: "Déposez les livrables significatifs, utilisez des noms de fichier explicites et remplacez les documents lorsqu'une nouvelle version devient la référence."
        },
        "anomalies": {
            purpose: "Ce bloc sert à consigner les défauts, incidents ou anomalies à traiter pendant l'exécution ou la préparation UAT.",
            guidance: "Déclarez chaque anomalie avec un titre clair, un niveau de gravité cohérent et une action de traitement."
        },
        "preparation uat": {
            purpose: "Ce bloc sert à préparer la recette : périmètre, acteurs, calendrier et preuves attendues.",
            guidance: "Renseignez les dates, le périmètre, les utilisateurs testeurs et les éléments de validation avant de lancer la recette."
        },
        "preparation mep": {
            purpose: "Ce bloc sert à préparer la mise en production et les prérequis de bascule.",
            guidance: "Documentez les prérequis, le plan de MEP, le rollback et les références de change avant d'autoriser la bascule."
        },
        "change mep et incidents": {
            purpose: "Ce bloc sert à suivre la préparation du change, le résultat de MEP et les incidents avant ou après mise en production.",
            guidance: "Complétez les champs factuels et gardez une trace claire des incidents et de leur résolution."
        },
        "hypercare": {
            purpose: "Ce bloc sert à suivre la période de stabilisation post-MEP et les conditions de sortie.",
            guidance: "Renseignez la période d'hypercare, le statut courant et la date de fin dès que la stabilisation est acquise."
        },
        "validation recette": {
            purpose: "Ce bloc sert à contrôler la validation de la recette avant d'autoriser la fin de la phase UAT.",
            guidance: "Vérifiez le PV de recette, l'état des cas de test et les anomalies bloquantes avant de valider."
        },
        "livrables uat & mep": {
            purpose: "Ce bloc sert à centraliser les pièces de recette et de mise en production.",
            guidance: "Chargez les preuves attendues pour la recette, la MEP et l'hypercare en conservant un nommage de fichier clair."
        },
        "feuille d anomalies": {
            purpose: "Ce bloc sert à suivre les anomalies de recette avec leur gravité, leur statut et leur résolution.",
            guidance: "Mettez à jour les anomalies en continu et clôturez-les uniquement lorsque la correction est réellement validée."
        },
        "workflow de validation": {
            purpose: "Ce bloc sert à visualiser et piloter le circuit de validation de la clôture du projet.",
            guidance: "Contrôlez qui doit encore agir, vérifiez les preuves de transfert et ne clôturez définitivement qu'après le dernier feu vert."
        },
        "bilan projet": {
            purpose: "Ce bloc sert à consigner le bilan global du projet : résultats, coûts, délais et écarts.",
            guidance: "Renseignez un bilan sincère et exploitable pour la capitalisation. Mentionnez les écarts majeurs et leurs causes."
        },
        "retours & capitalisation": {
            purpose: "Ce bloc sert à formaliser les leçons apprises, réussites et points d'amélioration.",
            guidance: "Privilégiez des retours concrets et réutilisables par les prochains projets plutôt qu'un texte purement descriptif."
        },
        "transfert run": {
            purpose: "Ce bloc sert à vérifier que le passage au RUN est prêt : accès, documentation, support et exploitation.",
            guidance: "Cochez ou renseignez uniquement les éléments réellement transférés et validés avec les parties prenantes concernées."
        },
        "statut final": {
            purpose: "Ce bloc sert à statuer explicitement sur l'issue du projet : clôturé, abandonné ou suspendu selon le cas métier.",
            guidance: "Choisissez le statut final seulement lorsque le bilan et la justification sont complets et cohérents."
        },
        "livrables de cloture": {
            purpose: "Ce bloc sert à centraliser les pièces finales de clôture et de transfert.",
            guidance: "Déposez uniquement les versions finales utiles à l'audit ou au RUN et vérifiez leur cohérence avec le bilan."
        },
        "validations de la charte projet": {
            purpose: "Ce bloc sert à traiter les validations métier et DSI de la charte avant le démarrage de la planification.",
            guidance: "Contrôlez la complétude du dossier, la version signée et les signatures attendues avant d'utiliser les boutons de validation."
        },
        "utilisateurs": {
            purpose: "Ce bloc sert à gérer les comptes, les profils et les accès des utilisateurs de la plateforme.",
            guidance: "Créez ou modifiez les comptes avec prudence, vérifiez les directions et les rôles, puis contrôlez l'impact sur les workflows."
        },
        "roles": {
            purpose: "Ce bloc sert à visualiser et piloter la répartition des rôles applicatifs.",
            guidance: "Assignez ou révisez les rôles en cohérence avec l'organisation réelle et les délégations actives."
        },
        "directions": {
            purpose: "Ce bloc sert à gérer le référentiel des directions métier utilisé dans les demandes et projets.",
            guidance: "Ajoutez ou modifiez une direction uniquement si le référentiel métier a évolué, puis vérifiez les utilisateurs associés."
        },
        "services": {
            purpose: "Ce bloc sert à maintenir le référentiel des services ou entités internes rattachées aux utilisateurs et aux projets.",
            guidance: "Conservez une nomenclature stable pour éviter les doublons et les incohérences de rattachement."
        },
        "delegations": {
            purpose: "Ce bloc sert à paramétrer les remplacements et délégations qui autorisent certains rôles à agir à la place d'un autre.",
            guidance: "Définissez des dates, un périmètre clair et contrôlez l'effet attendu sur les validations avant d'activer une délégation."
        },
        "parametres": {
            purpose: "Ce bloc sert à maintenir des paramètres applicatifs qui impactent le comportement global des workflows.",
            guidance: "Modifiez les paramètres avec prudence, documentez l'intention et vérifiez les impacts transverses après enregistrement."
        },
        "autorisations et droits": {
            purpose: "Ce bloc sert à piloter la matrice des permissions par rôle sur les vues et actions principales.",
            guidance: "Activez ou désactivez une permission en gardant une logique métier cohérente. Vérifiez ensuite l'effet sur le menu et l'accès direct."
        },
        "mes demandes": {
            purpose: "Ce bloc sert à suivre la liste des demandes créées ou portées par l'utilisateur connecté.",
            guidance: "Utilisez les filtres, consultez le statut et ouvrez les détails pour corriger ou compléter une demande."
        },
        "nouvelle demande de projet": {
            purpose: "Ce bloc sert à saisir une nouvelle demande de projet avec ses pièces, son contexte et sa gouvernance métier.",
            guidance: "Complétez le formulaire du haut vers le bas, joignez les pièces utiles puis choisissez entre le brouillon et la soumission."
        },
        "historique & tracabilite": {
            purpose: "Ce bloc sert à retracer les actions et décisions sur un périmètre de projets ou de validations.",
            guidance: "Utilisez cette vue comme outil d'audit et de contrôle avant une validation ou une analyse d'écart."
        },
        "portefeuille projets": {
            purpose: "Ce bloc sert à synthétiser l'état des projets et les indicateurs de portefeuille.",
            guidance: "Lisez d'abord les synthèses globales puis utilisez les filtres et les exports pour détailler le portefeuille."
        },
        "notifications": {
            purpose: "Ce bloc sert à prioriser et traiter les alertes ou événements qui requièrent votre attention.",
            guidance: "Commencez par les non lues et ouvrez les éléments liés pour agir sur l'écran métier correspondant."
        }
    };

    function normalize(value) {
        return (value || "")
            .toString()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/[^a-zA-Z0-9]+/g, " ")
            .trim()
            .toLowerCase();
    }

    function escapeHtml(value) {
        return (value || "")
            .toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function getContext() {
        const params = new URLSearchParams(window.location.search);
        const body = document.body || {};
        const dataset = body.dataset || {};
        const path = window.location.pathname || "";
        const pathSegments = path.split("/").filter(Boolean);
        const inferredController = pathSegments[0] || "";
        const inferredAction = pathSegments[1] || "Index";
        const titleElement = document.querySelector(".topbar-title, .auth-premium-title, .auth-premium-copy h1, .page-hero-title, .project-hero-title, .validation-hero-title, .review-hero-title, .document-hero-title, .preview-hero-title, .text-center h2");

        return {
            controller: dataset.controller || inferredController,
            action: dataset.action || inferredAction,
            pageTitle: dataset.pageTitle || (titleElement ? titleElement.textContent.trim() : document.title.split("-")[0].trim()),
            tab: (params.get("tab") || "").toLowerCase(),
            path: path.toLowerCase()
        };
    }

    function resolveRouteHelp(context) {
        const specific = routeHelpCatalog.find((entry) => entry.match(context));
        if (specific) {
            return {
                ...specific,
                tips: specific.tips || composeBestPractices(specific.title, context, "page"),
                actors: specific.actors || composeWorkflowActors(specific.title, context, "page"),
                requiredWhen: specific.requiredWhen || composeWorkflowRequirement(specific.title, context, "page")
            };
        }

        if (context.controller === "Admin") {
            return {
                title: context.pageTitle || "Administration",
                purpose: "Cet écran sert au paramétrage du socle applicatif : utilisateurs, référentiels, délégations, rôles et règles de gestion.",
                guidance: "Lisez les libellés de section, mettez à jour uniquement les données voulues puis contrôlez l'impact fonctionnel avant de quitter la page.",
                tips: composeBestPractices(context.pageTitle || "Administration", context, "page"),
                actors: composeWorkflowActors(context.pageTitle || "Administration", context, "page"),
                requiredWhen: composeWorkflowRequirement(context.pageTitle || "Administration", context, "page")
            };
        }

        if (context.controller === "Projet") {
            return {
                title: context.pageTitle || "Projet",
                purpose: "Cet écran sert au pilotage détaillé du projet et s'inscrit dans un workflow de phase, de livrables et de validations.",
                guidance: "Complétez le bloc courant, enregistrez, puis contrôlez les alertes ou les validations attendues avant de passer à l'étape suivante.",
                tips: composeBestPractices(context.pageTitle || "Projet", context, "page"),
                actors: composeWorkflowActors(context.pageTitle || "Projet", context, "page"),
                requiredWhen: composeWorkflowRequirement(context.pageTitle || "Projet", context, "page")
            };
        }

        if (context.controller === "DemandeProjet") {
            return {
                title: context.pageTitle || "Demande projet",
                purpose: "Cet écran sert à suivre, renseigner ou valider une demande de projet dans le workflow métier et DSI.",
                guidance: "Renseignez les champs du formulaire ou utilisez les actions disponibles en contrôlant toujours la complétude du dossier avant validation.",
                tips: composeBestPractices(context.pageTitle || "Demande projet", context, "page"),
                actors: composeWorkflowActors(context.pageTitle || "Demande projet", context, "page"),
                requiredWhen: composeWorkflowRequirement(context.pageTitle || "Demande projet", context, "page")
            };
        }

        return {
            title: context.pageTitle || "Cet écran",
            purpose: `Cet écran sert à piloter ${((context.pageTitle || "ce dossier").toLowerCase())} dans l'application.`,
            guidance: "Lisez les indicateurs, renseignez les formulaires utiles, enregistrez vos changements puis vérifiez les messages de succès ou de blocage.",
            tips: composeBestPractices(context.pageTitle || "Cet écran", context, "page"),
            actors: composeWorkflowActors(context.pageTitle || "Cet écran", context, "page"),
            requiredWhen: composeWorkflowRequirement(context.pageTitle || "Cet écran", context, "page")
        };
    }

    function resolveSectionHelp(rawTitle, context) {
        const title = (rawTitle || "").replace(/\s+/g, " ").trim();
        const key = normalize(title);

        if (sectionHelpCatalog[key]) {
            return {
                title,
                purpose: sectionHelpCatalog[key].purpose,
                guidance: sectionHelpCatalog[key].guidance,
                tips: sectionHelpCatalog[key].tips || composeBestPractices(title, context, "section"),
                actors: sectionHelpCatalog[key].actors || composeWorkflowActors(title, context, "section"),
                requiredWhen: sectionHelpCatalog[key].requiredWhen || composeWorkflowRequirement(title, context, "section")
            };
        }

        if (context.controller === "Admin") {
            return {
                title,
                purpose: `Ce bloc sert au paramétrage de ${title.toLowerCase()} dans le référentiel d'administration.`,
                guidance: "Renseignez les champs attendus, vérifiez la cohérence avec l'organisation réelle puis enregistrez avant de poursuivre.",
                tips: composeBestPractices(title, context, "section"),
                actors: composeWorkflowActors(title, context, "section"),
                requiredWhen: composeWorkflowRequirement(title, context, "section")
            };
        }

        if (context.controller === "Projet") {
            return {
                title,
                purpose: `Ce bloc sert à piloter ${title.toLowerCase()} pour le projet en cours.`,
                guidance: "Complétez les informations attendues, utilisez les actions d'ajout si nécessaire puis enregistrez avant de demander une validation ou de passer à la phase suivante.",
                tips: composeBestPractices(title, context, "section"),
                actors: composeWorkflowActors(title, context, "section"),
                requiredWhen: composeWorkflowRequirement(title, context, "section")
            };
        }

        if (context.controller === "DemandeProjet") {
            return {
                title,
                purpose: `Ce bloc sert à renseigner ${title.toLowerCase()} dans la demande projet.`,
                guidance: "Saisissez des informations claires, vérifiables et cohérentes avec les pièces jointes avant de soumettre ou mettre à jour la demande.",
                tips: composeBestPractices(title, context, "section"),
                actors: composeWorkflowActors(title, context, "section"),
                requiredWhen: composeWorkflowRequirement(title, context, "section")
            };
        }

        return {
            title,
            purpose: `Ce bloc sert à traiter ${title.toLowerCase()} dans cet écran.`,
            guidance: "Complétez les champs visibles, vérifiez les valeurs obligatoires puis enregistrez pour rendre les informations exploitables.",
            tips: composeBestPractices(title, context, "section"),
            actors: composeWorkflowActors(title, context, "section"),
            requiredWhen: composeWorkflowRequirement(title, context, "section")
        };
    }

    function composeBestPractices(title, context, scope) {
        const key = normalize(title);

        if (scope === "page" && context.controller === "Projet" && context.tab === "planification") {
            return "Bonnes pratiques : construisez d'abord le planning interactif, enregistrez souvent, puis complétez RACI, communication, budget et kick-off. A éviter : demander une validation avec une checklist encore partiellement rouge.";
        }

        if (scope === "page" && context.controller === "Projet" && context.tab === "analyse") {
            return "Bonnes pratiques : déposez les documents obligatoires, générez la charte puis bouclez les signatures avant de solliciter DM et DSI. A éviter : valider la phase alors que l'alerte de blocage cite encore des pièces ou validations manquantes.";
        }

        if (scope === "page" && context.controller === "Projet" && context.tab === "execution") {
            return "Bonnes pratiques : mettez à jour les faits réels, les anomalies et les risques au fil de l'eau. A éviter : utiliser l'onglet comme un reporting théorique déconnecté des tâches et charges réelles.";
        }

        if (scope === "page" && context.controller === "Projet" && context.tab === "cloture") {
            return "Bonnes pratiques : terminez le transfert RUN et le bilan avant de lancer la clôture. A éviter : forcer une clôture sans preuves de passage au support ni validations finales.";
        }

        if (scope === "page" && context.controller === "DemandeProjet") {
            return "Bonnes pratiques : formulez un besoin concret, mesurable et rattaché au bon sponsor. A éviter : soumettre une demande avec un contexte flou, des pièces manquantes ou une gouvernance incomplète.";
        }

        if (scope === "page" && context.controller === "Admin") {
            return "Bonnes pratiques : considérez chaque modification comme un changement de référentiel ou de sécurité. A éviter : modifier rôles, délégations ou paramètres sans mesurer l'impact sur les workflows existants.";
        }

        if (key.includes("validation")) {
            return "Bonnes pratiques : vérifiez toujours les prérequis et les pièces avant de valider. A éviter : donner un feu vert de confort alors que le dossier contient encore un blocage métier ou documentaire.";
        }

        if (key.includes("risque")) {
            return "Bonnes pratiques : formulez un risque comme un événement concret avec impact et réponse associée. A éviter : créer des risques vagues, sans propriétaire ni plan d'atténuation.";
        }

        if (key.includes("budget")) {
            return "Bonnes pratiques : détaillez les postes significatifs et documentez les hypothèses de chiffrage. A éviter : saisir un montant global non expliqué ou non raccordé au périmètre réel du projet.";
        }

        if (key.includes("raci")) {
            return "Bonnes pratiques : affectez clairement un responsable et un approbateur par activité. A éviter : multiplier les rôles ambigus ou laisser une activité sans propriétaire clair.";
        }

        if (key.includes("communication") || key.includes("gouvernance")) {
            return "Bonnes pratiques : définissez un rythme d'instance réaliste et des participants utiles. A éviter : créer trop de réunions sans objectif clair ou oublier les décideurs nécessaires.";
        }

        if (key.includes("planning") || key.includes("jalon") || key.includes("wbs")) {
            return "Bonnes pratiques : partez d'un découpage simple, daté et ordonné, puis enrichissez progressivement. A éviter : créer un planning trop détaillé trop tôt ou avec des dates incohérentes.";
        }

        if (key.includes("charte")) {
            return "Bonnes pratiques : considérez la charte comme le contrat de cadrage du projet et gardez une version de référence unique. A éviter : modifier la charte après signature sans republier la bonne version et relancer les validations nécessaires.";
        }

        if (key.includes("livrable") || key.includes("document")) {
            return "Bonnes pratiques : utilisez des noms de fichier explicites et remplacez clairement les versions obsolètes. A éviter : déposer des documents mal typés, incomplets ou déconnectés du bloc où ils apparaissent.";
        }

        if (key.includes("kick")) {
            return "Bonnes pratiques : consignez les décisions, participants et actions de lancement immédiatement après la réunion. A éviter : produire un PV générique qui ne reflète ni les engagements ni les suites à donner.";
        }

        if (key.includes("notification")) {
            return "Bonnes pratiques : traitez en priorité les notifications non lues à impact métier. A éviter : tout marquer comme lu sans ouvrir les dossiers concernés.";
        }

        return "Bonnes pratiques : renseignez ce bloc avec des informations vérifiables, à jour et cohérentes avec le reste du dossier. A éviter : laisser des zones clés approximatives, dupliquées ou non relues avant enregistrement.";
    }

    function composeWorkflowActors(title, context, scope) {
        const key = normalize(title);

        if (scope === "page") {
            if (context.controller === "Projet" && context.tab === "analyse") {
                return "Chef de Projet pour préparer le dossier, puis Directeur Métier et enfin DSI ou RSIT délégué pour les validations de charte.";
            }

            if (context.controller === "Projet" && context.tab === "planification") {
                return "Chef de Projet pour renseigner et structurer le dossier, puis Directeur Métier et enfin DSI ou RSIT délégué pour les validations de planification.";
            }

            if (context.controller === "Projet" && context.tab === "execution") {
                return "Chef de Projet principalement, avec contribution éventuelle DSI, RSIT, métier et support selon les sujets d'arbitrage ou de suivi.";
            }

            if (context.controller === "Projet" && context.tab === "uat") {
                return "Chef de Projet, testeurs métier, Directeur Métier, DSI et éventuellement RSIT selon la recette, la MEP et l'hypercare.";
            }

            if (context.controller === "Projet" && context.tab === "cloture") {
                return "Chef de Projet pour préparer le dossier, puis Demandeur, Directeur Métier et enfin DSI pour la clôture finale.";
            }

            if (context.controller === "Projet" && context.action === "CharteProjet") {
                return "Chef de Projet pour produire la charte, avec intervention du Sponsor ou Directeur Métier et du DSI ou RSIT délégué selon le circuit de signature et validation.";
            }

            if (context.controller === "Projet" && context.action === "ValidationsProjet") {
                return "Directeur Métier pour la première validation, puis DSI ou RSIT délégué pour la validation finale de charte.";
            }

            if (context.controller === "Projet" && context.action === "Charges") {
                return "Chef de Projet et contributeurs du projet pour la saisie, avec validation ou contrôle par les responsables de pilotage selon l'organisation.";
            }

            if (context.controller === "DemandeProjet" && (context.action === "Create" || context.action === "Edit")) {
                return "Demandeur ou porteur métier pour la saisie initiale, puis Directeur Métier et DSI pour le circuit de validation.";
            }

            if (context.controller === "DemandeProjet" && context.action === "ListeValidationDM") {
                return "Directeur Métier concerné par la demande.";
            }

            if (context.controller === "DemandeProjet" && context.action === "ListeValidationDSI") {
                return "DSI ou RSIT délégué selon la gouvernance active.";
            }

            if (context.controller === "Notification") {
                return "L'utilisateur connecté, sur les notifications qui relèvent de son rôle et de son périmètre d'action.";
            }

            if (context.controller === "Autorisations" || context.controller === "Admin") {
                return "AdminIT, avec un usage réservé aux responsables d'administration et de sécurité de la plateforme.";
            }
        }

        if (key.includes("validation directeur metier")) {
            return "Directeur Métier sponsor du projet ou de la demande.";
        }

        if (key.includes("validation dsi")) {
            return "DSI ou RSIT délégué lorsque la délégation est active.";
        }

        if (key.includes("validation recette")) {
            return "Chef de Projet avec validation métier et décision DSI selon la règle de recette du projet.";
        }

        if (key.includes("workflow de validation")) {
            return "Chaque acteur du circuit final : Demandeur, Directeur Métier puis DSI.";
        }

        if (key.includes("charte")) {
            return "Chef de Projet pour la préparation, Sponsor ou Directeur Métier pour l'engagement métier, puis DSI ou RSIT délégué pour la validation.";
        }

        if (key.includes("planning") || key.includes("jalon") || key.includes("wbs")) {
            return "Chef de Projet principalement, avec contribution métier ou DSI si le planning doit être arbitré.";
        }

        if (key.includes("raci")) {
            return "Chef de Projet pour la construction, avec arbitrage métier ou DSI si la responsabilité n'est pas claire.";
        }

        if (key.includes("communication") || key.includes("gouvernance")) {
            return "Chef de Projet, avec validation implicite des parties prenantes qui participent aux instances projet.";
        }

        if (key.includes("budget")) {
            return "Chef de Projet pour la préparation, Sponsor ou Directeur Métier pour l'alignement métier, puis DSI pour l'arbitrage si nécessaire.";
        }

        if (key.includes("kick")) {
            return "Chef de Projet pour la formalisation, avec les participants clés du lancement comme contributeurs.";
        }

        if (key.includes("risque")) {
            return "Chef de Projet en première ligne, avec contribution des responsables métier ou techniques selon le risque.";
        }

        if (key.includes("livrable") || key.includes("document")) {
            return "L'acteur responsable de la phase, généralement le Chef de Projet, puis les validateurs si le document conditionne un feu vert.";
        }

        if (context.controller === "Projet") {
            return "L'acteur responsable de la phase projet en cours, puis le validateur suivant dans le workflow si applicable.";
        }

        if (context.controller === "DemandeProjet") {
            return "Le porteur de la demande ou le validateur du niveau courant selon l'étape du workflow.";
        }

        if (context.controller === "Admin" || context.controller === "Autorisations") {
            return "AdminIT uniquement.";
        }

        return "L'acteur responsable de cette section selon son rôle courant dans l'application.";
    }

    function composeWorkflowRequirement(title, context, scope) {
        const key = normalize(title);

        if (scope === "page") {
            if (context.controller === "Projet" && context.tab === "analyse") {
                return "Cette vue devient obligatoire dès qu'une demande validée est transformée en projet, jusqu'au passage en Planification & Validation.";
            }

            if (context.controller === "Projet" && context.tab === "planification") {
                return "Cette vue devient obligatoire dès l'entrée en phase Planification & Validation et jusqu'à la validation DSI autorisant l'Exécution.";
            }

            if (context.controller === "Projet" && context.tab === "execution") {
                return "Cette vue devient obligatoire à partir de l'entrée en Exécution et reste la référence jusqu'à l'ouverture de la phase UAT & MEP.";
            }

            if (context.controller === "Projet" && context.tab === "uat") {
                return "Cette vue devient obligatoire dès que le projet entre en recette ou en préparation de mise en production, jusqu'à la stabilisation post-MEP.";
            }

            if (context.controller === "Projet" && context.tab === "cloture") {
                return "Cette vue devient obligatoire lorsque le projet est livré et qu'il faut formaliser le bilan, le transfert RUN et la clôture finale.";
            }

            if (context.controller === "Projet" && context.action === "CharteProjet") {
                return "Cette vue devient obligatoire pendant l'analyse, dès qu'il faut générer, faire signer ou republier la charte projet.";
            }

            if (context.controller === "Projet" && context.action === "ValidationsProjet") {
                return "Cette vue devient obligatoire lorsque la charte est prête et que le projet doit franchir le contrôle DM puis DSI avant la planification.";
            }

            if (context.controller === "DemandeProjet" && (context.action === "Create" || context.action === "Edit")) {
                return "Cette vue devient obligatoire lorsqu'une nouvelle demande doit être créée, corrigée ou resoumise dans le workflow.";
            }

            if (context.controller === "DemandeProjet" && context.action === "ListeValidationDM") {
                return "Cette vue devient obligatoire dès qu'une demande entre dans le panier de validation du Directeur Métier.";
            }

            if (context.controller === "DemandeProjet" && context.action === "ListeValidationDSI") {
                return "Cette vue devient obligatoire lorsqu'une demande validée par le métier attend l'arbitrage DSI.";
            }

            if (context.controller === "Autorisations" || context.controller === "Admin") {
                return "Cette vue devient obligatoire lorsqu'un accès, un référentiel, une délégation ou une règle d'administration doit être créé, corrigé ou contrôlé.";
            }
        }

        if (key.includes("checklist livrables obligatoires")) {
            return "Ce bloc devient obligatoire avant toute validation de phase ou changement de phase conditionné par des livrables officiels.";
        }

        if (key.includes("validation directeur metier")) {
            return "Ce bloc devient obligatoire lorsque le dossier est complet et qu'un feu vert métier est requis pour poursuivre.";
        }

        if (key.includes("validation dsi")) {
            return "Ce bloc devient obligatoire après la validation métier, quand la décision DSI conditionne la poursuite du workflow.";
        }

        if (key.includes("charte")) {
            return "Ce bloc devient obligatoire pendant l'analyse dès qu'il faut obtenir une charte complète, signée et validable.";
        }

        if (key.includes("planning") || key.includes("jalon") || key.includes("wbs")) {
            return "Ce bloc devient obligatoire en phase Planification & Validation avant les validations DM et DSI puis avant le passage en Exécution.";
        }

        if (key.includes("raci")) {
            return "Ce bloc devient obligatoire quand la planification doit prouver une répartition claire des responsabilités.";
        }

        if (key.includes("communication") || key.includes("gouvernance")) {
            return "Ce bloc devient obligatoire lorsqu'un schéma de communication formel fait partie des livrables attendus de planification.";
        }

        if (key.includes("budget")) {
            return "Ce bloc devient obligatoire dès que la phase exige un budget prévisionnel ou un chiffrage consolidé.";
        }

        if (key.includes("kick")) {
            return "Ce bloc devient obligatoire lorsqu'un PV de lancement est requis dans la checklist de planification.";
        }

        if (key.includes("risque")) {
            return "Ce bloc devient obligatoire dès qu'un risque prioritaire existe ou qu'une validation dépend de la maîtrise des risques.";
        }

        if (key.includes("livrable") || key.includes("document")) {
            return "Ce bloc devient obligatoire lorsque le document conditionne une validation, un audit ou un changement de phase.";
        }

        if (key.includes("transfert run")) {
            return "Ce bloc devient obligatoire en clôture avant la validation finale DSI et la sortie définitive du projet.";
        }

        if (key.includes("workflow de validation")) {
            return "Ce bloc devient obligatoire dès qu'une demande de clôture est créée et jusqu'à la dernière validation du circuit.";
        }

        if (context.controller === "Projet") {
            return "Cette section devient obligatoire lorsqu'elle conditionne la complétude de la phase projet en cours ou l'obtention d'un feu vert.";
        }

        if (context.controller === "DemandeProjet") {
            return "Cette section devient obligatoire lorsqu'elle conditionne la soumission ou la validation de la demande.";
        }

        return "Cette section devient obligatoire lorsqu'elle conditionne la complétude du dossier ou l'étape suivante du workflow.";
    }

    function createHelpNode(info, scope = "section") {
        const wrapper = document.createElement("div");
        wrapper.className = "form-help global-help-injected";
        wrapper.dataset.helpScope = scope;
        wrapper.dataset.helpTitle = info.title;
        wrapper.dataset.helpPurpose = info.purpose;
        wrapper.dataset.helpGuidance = info.guidance;
        wrapper.dataset.helpTips = info.tips || "";
        wrapper.dataset.helpActors = info.actors || "";
        wrapper.dataset.helpRequiredWhen = info.requiredWhen || "";
        wrapper.innerHTML = `
<button type="button" class="form-help-trigger" aria-label="Aide : ${escapeHtml(info.title)}">
    <i class="bi bi-info-circle"></i>
</button>
<div class="form-help-popover" role="tooltip">
    <strong>${escapeHtml(info.title)}</strong>
    <div class="form-help-popover-section">
        <span>À quoi sert ce bloc ?</span>
        <p>${escapeHtml(info.purpose)}</p>
    </div>
    <div class="form-help-popover-section">
        <span>Comment le renseigner ?</span>
        <p>${escapeHtml(info.guidance)}</p>
    </div>
</div>`;
        return wrapper;
    }

    function shouldIgnoreHeading(element) {
        if (!element) {
            return true;
        }

        if (element.closest(".sidebar, .dropdown-menu, footer, .notif-dropdown, .layout-footer")) {
            return true;
        }

        if (element.closest(".form-help-title-row")) {
            return true;
        }

        const text = (element.textContent || "").replace(/\s+/g, " ").trim();
        if (!text || text.length < 3) {
            return true;
        }

        return false;
    }

    function wrapHeadingWithHelp(element, info) {
        if (!element || !element.parentNode) {
            return;
        }

        const parent = element.parentNode;
        const row = document.createElement("div");
        row.className = "form-help-title-row global-help-row";

        parent.insertBefore(row, element);
        row.appendChild(element);
        row.appendChild(createHelpNode(info, "section"));
    }

    function injectPageHelp(context) {
        const pageTitle = document.querySelector(".topbar-title, .auth-premium-title, .auth-premium-copy h1, .page-hero-title, .project-hero-title, .validation-hero-title, .review-hero-title, .document-hero-title, .preview-hero-title, .text-center h2");

        if (!pageTitle || shouldIgnoreHeading(pageTitle) || pageTitle.dataset.helpPageInjected === "1") {
            return;
        }

        const info = resolveRouteHelp(context);
        pageTitle.dataset.helpPageInjected = "1";
        const parent = pageTitle.parentNode;
        const row = document.createElement("div");
        row.className = "form-help-title-row global-help-row";

        parent.insertBefore(row, pageTitle);
        row.appendChild(pageTitle);
        row.appendChild(createHelpNode(info, "page"));
    }

    function injectSectionHelp(context) {
        document.querySelectorAll(titleSelectors).forEach((element) => {
            if (!element || element.classList.contains("topbar-title")) {
                return;
            }

            if (shouldIgnoreHeading(element) || element.dataset.helpInjected === "1") {
                return;
            }

            if (element.parentElement && element.parentElement.querySelector(":scope > .form-help.global-help-injected")) {
                element.dataset.helpInjected = "1";
                return;
            }

            const info = resolveSectionHelp((element.textContent || "").replace(/\s+/g, " ").trim(), context);
            element.dataset.helpInjected = "1";
            wrapHeadingWithHelp(element, info);
        });
    }

    function bootHelpEnhancements() {
        const context = getContext();
        injectPageHelp(context);
        injectSectionHelp(context);
        ensureGuideDrawer(context);
    }

    function extractHelpInfoFromWrapper(wrapper, context) {
        if (!wrapper) {
            return null;
        }

        const title = wrapper.dataset.helpTitle || wrapper.querySelector(".form-help-popover strong")?.textContent?.trim();
        const paragraphs = wrapper.querySelectorAll(".form-help-popover-section p");
        const purpose = wrapper.dataset.helpPurpose || paragraphs[0]?.textContent?.trim();
        const guidance = wrapper.dataset.helpGuidance || paragraphs[1]?.textContent?.trim();
        const tips = wrapper.dataset.helpTips;
        const actors = wrapper.dataset.helpActors;
        const requiredWhen = wrapper.dataset.helpRequiredWhen;

        if (title && purpose && guidance) {
            return {
                title,
                purpose,
                guidance,
                tips: tips || composeBestPractices(title, context, wrapper.dataset.helpScope || "section"),
                actors: actors || composeWorkflowActors(title, context, wrapper.dataset.helpScope || "section"),
                requiredWhen: requiredWhen || composeWorkflowRequirement(title, context, wrapper.dataset.helpScope || "section")
            };
        }

        const titleHost = wrapper.closest(".form-help-title-row")?.querySelector(titleSelectors);
        if (titleHost) {
            return resolveSectionHelp(titleHost.textContent.trim(), context);
        }

        return null;
    }

    function collectSectionGuides(context) {
        const seen = new Set();
        const items = [];

        document.querySelectorAll(".form-help").forEach((wrapper) => {
            if (!wrapper || wrapper.dataset.helpScope === "page") {
                return;
            }

            if (wrapper.closest(".sidebar, .dropdown-menu, footer, .notif-dropdown, .layout-footer, .screen-guide-drawer")) {
                return;
            }

            const info = extractHelpInfoFromWrapper(wrapper, context);
            if (!info) {
                return;
            }

            const key = normalize(info.title);
            if (!key || seen.has(key)) {
                return;
            }

            seen.add(key);
            items.push(info);
        });

        return items;
    }

    function ensureGuideDrawer(context) {
        let drawer = document.getElementById("screenGuideDrawer");
        if (drawer) {
            return drawer;
        }

        const routeInfo = resolveRouteHelp(context || getContext());

        const fab = document.createElement("button");
        fab.type = "button";
        fab.className = "screen-guide-fab";
        fab.id = "screenGuideFab";
        fab.setAttribute("data-screen-guide-open", "page");
        fab.setAttribute("aria-label", "Ouvrir le guide de l'écran");
        fab.innerHTML = `
<i class="bi bi-life-preserver"></i>
<span>Guide de l'écran</span>`;

        const overlay = document.createElement("div");
        overlay.className = "screen-guide-overlay";
        overlay.id = "screenGuideOverlay";
        overlay.setAttribute("aria-hidden", "true");

        drawer = document.createElement("aside");
        drawer.className = "screen-guide-drawer";
        drawer.id = "screenGuideDrawer";
        drawer.setAttribute("aria-hidden", "true");
        drawer.innerHTML = `
<div class="screen-guide-header">
    <div class="screen-guide-header-copy">
        <span class="screen-guide-kicker">Aide interactive</span>
        <h2 class="screen-guide-heading">Guide de l'écran</h2>
        <p class="screen-guide-subtitle">${escapeHtml(routeInfo.title)}</p>
    </div>
    <button type="button" class="screen-guide-close" id="screenGuideClose" aria-label="Fermer le guide">
        <i class="bi bi-x-lg"></i>
    </button>
</div>
<div class="screen-guide-body">
    <section class="screen-guide-focus-card">
        <span class="screen-guide-focus-label" id="screenGuideFocusLabel">Vue d'ensemble</span>
        <h3 class="screen-guide-focus-title" id="screenGuideFocusTitle"></h3>
        <div class="screen-guide-copy-block">
            <span>A quoi sert cet ecran ?</span>
            <p id="screenGuidePurpose"></p>
        </div>
        <div class="screen-guide-copy-block">
            <span>Comment le renseigner ?</span>
            <p id="screenGuideGuidance"></p>
        </div>
        <div class="screen-guide-copy-block">
            <span>Bonnes pratiques / erreurs a eviter</span>
            <p id="screenGuideTips"></p>
        </div>
        <div class="screen-guide-copy-block">
            <span>Qui doit agir ?</span>
            <p id="screenGuideActors"></p>
        </div>
        <div class="screen-guide-copy-block">
            <span>Quand cette section devient obligatoire ?</span>
            <p id="screenGuideRequiredWhen"></p>
        </div>
    </section>
    <section class="screen-guide-sections">
        <div class="screen-guide-sections-head">
            <h3>Blocs de l'ecran</h3>
            <span id="screenGuideSectionCount">0</span>
        </div>
        <div class="screen-guide-section-list" id="screenGuideSectionList"></div>
    </section>
</div>`;

        document.body.appendChild(fab);
        document.body.appendChild(overlay);
        document.body.appendChild(drawer);

        overlay.addEventListener("click", closeGuideDrawer);
        drawer.querySelector("#screenGuideClose")?.addEventListener("click", closeGuideDrawer);

        return drawer;
    }

    function setGuideDrawerState(open) {
        document.body.classList.toggle("screen-guide-open", open);
        const drawer = document.getElementById("screenGuideDrawer");
        const overlay = document.getElementById("screenGuideOverlay");

        if (drawer) {
            drawer.setAttribute("aria-hidden", open ? "false" : "true");
        }

        if (overlay) {
            overlay.setAttribute("aria-hidden", open ? "false" : "true");
        }
    }

    function closeGuideDrawer() {
        setGuideDrawerState(false);
    }

    function renderGuideSections(context, activeTitle) {
        const list = document.getElementById("screenGuideSectionList");
        const count = document.getElementById("screenGuideSectionCount");
        if (!list || !count) {
            return;
        }

        const sections = collectSectionGuides(context);
        count.textContent = `${sections.length} bloc(s)`;
        list.innerHTML = "";

        sections.forEach((section) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "screen-guide-section-item";
            if (activeTitle && normalize(activeTitle) === normalize(section.title)) {
                button.classList.add("is-active");
            }

            button.dataset.helpTitle = section.title;
            button.dataset.helpPurpose = section.purpose;
            button.dataset.helpGuidance = section.guidance;
            button.dataset.helpTips = section.tips || "";
            button.dataset.helpActors = section.actors || "";
            button.dataset.helpRequiredWhen = section.requiredWhen || "";
            button.innerHTML = `
<span class="screen-guide-section-item-title">${escapeHtml(section.title)}</span>
<span class="screen-guide-section-item-copy">${escapeHtml(section.purpose)}</span>`;
            list.appendChild(button);
        });
    }

    function openGuideDrawer(info, context, modeLabel) {
        ensureGuideDrawer(context);

        const routeInfo = resolveRouteHelp(context);
        const subtitle = document.querySelector(".screen-guide-subtitle");
        const focusLabel = document.getElementById("screenGuideFocusLabel");
        const focusTitle = document.getElementById("screenGuideFocusTitle");
        const purpose = document.getElementById("screenGuidePurpose");
        const guidance = document.getElementById("screenGuideGuidance");
        const tips = document.getElementById("screenGuideTips");
        const actors = document.getElementById("screenGuideActors");
        const requiredWhen = document.getElementById("screenGuideRequiredWhen");

        if (subtitle) {
            subtitle.textContent = routeInfo.title;
        }
        if (focusLabel) {
            focusLabel.textContent = modeLabel || "Vue d'ensemble";
        }
        if (focusTitle) {
            focusTitle.textContent = info.title;
        }
        if (purpose) {
            purpose.textContent = info.purpose;
        }
        if (guidance) {
            guidance.textContent = info.guidance;
        }
        if (tips) {
            tips.textContent = info.tips || composeBestPractices(info.title, context, "section");
        }
        if (actors) {
            actors.textContent = info.actors || composeWorkflowActors(info.title, context, "section");
        }
        if (requiredWhen) {
            requiredWhen.textContent = info.requiredWhen || composeWorkflowRequirement(info.title, context, "section");
        }

        renderGuideSections(context, info.title);
        setGuideDrawerState(true);
    }

    function openPageGuide() {
        const context = getContext();
        openGuideDrawer(resolveRouteHelp(context), context, "Vue d'ensemble");
    }

    function openSectionGuide(wrapper) {
        const context = getContext();
        const info = extractHelpInfoFromWrapper(wrapper, context);
        if (!info) {
            openPageGuide();
            return;
        }

        openGuideDrawer(info, context, "Bloc selectionne");
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", bootHelpEnhancements);
    } else {
        bootHelpEnhancements();
    }

    const observer = new MutationObserver((mutations) => {
        if (mutations.some((mutation) => mutation.addedNodes && mutation.addedNodes.length > 0)) {
            requestAnimationFrame(bootHelpEnhancements);
        }
    });

    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
    }

    document.addEventListener("click", (event) => {
        const guideOpen = event.target.closest("[data-screen-guide-open]");
        if (guideOpen) {
            event.preventDefault();
            openPageGuide();
            return;
        }

        const trigger = event.target.closest(".form-help-trigger");
        if (trigger) {
            event.preventDefault();
            event.stopPropagation();
            openSectionGuide(trigger.closest(".form-help"));
            return;
        }

        const sectionButton = event.target.closest(".screen-guide-section-item");
        if (sectionButton) {
            event.preventDefault();
            openGuideDrawer({
                title: sectionButton.dataset.helpTitle || "Bloc",
                purpose: sectionButton.dataset.helpPurpose || "",
                guidance: sectionButton.dataset.helpGuidance || "",
                tips: sectionButton.dataset.helpTips || "",
                actors: sectionButton.dataset.helpActors || "",
                requiredWhen: sectionButton.dataset.helpRequiredWhen || ""
            }, getContext(), "Bloc selectionne");
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && document.body.classList.contains("screen-guide-open")) {
            closeGuideDrawer();
        }
    });

    // Synonymes/variantes courantes -> terme canonique présent dans les catalogues d'aide.
    // Reste déterministe (pas de LLM) : simple table de correspondance, étendue au besoin.
    const helpSynonyms = {
        creer: "creation", cree: "creation", creation: "creation",
        valider: "validation", validee: "validation", validation: "validation", approuver: "validation",
        deposer: "depot", depose: "depot", upload: "depot", televerser: "depot",
        document: "depot", fichier: "depot", piece: "depot", livrable: "depot", rattacher: "depot", joindre: "depot",
        rejeter: "rejet", refuser: "rejet", rejet: "rejet",
        cloturer: "cloture", fermer: "cloture", terminer: "cloture", cloture: "cloture",
        echec: "echecs", echouer: "echecs", probleme: "blocage", bloque: "blocage", bloquant: "blocage",
        charte: "charte", signer: "signature", signature: "signature",
        budget: "budget", cout: "budget", couts: "budget",
        risque: "risque", risques: "risque",
        anomalie: "anomalie", bug: "anomalie", incident: "anomalie",
        delai: "planning", retard: "planning", planning: "planning", planifier: "planning",
        equipe: "membre", membre: "membre", participant: "membre",
        deleguer: "delegation", delegation: "delegation", delegataire: "delegation",
        absence: "delegation", absent: "delegation", remplacer: "delegation", remplacement: "delegation",
        supprimer: "corbeille", suppression: "corbeille", corbeille: "corbeille",
        restaurer: "corbeille", restauration: "corbeille",
        brouillon: "brouillon", cdc: "cahier",
        connexion: "connecter", connecter: "connecter", passe: "motdepasse"
    };

    // Fiches thématiques transverses : jamais liées à une route précise, mais
    // toujours proposées par la recherche de l'assistant (searchHelpCatalog).
    const topicHelpCatalog = [
        {
            title: "Délégation de rôle (absence)",
            purpose: "La délégation permet à un délégataire d'agir à la place du titulaire (Directeur Métier, DSI ou Chef de Projet) pendant une période définie, avec traçabilité complète dans l'historique.",
            guidance: "Ouvrez Administration > Délégations. Un DM/DSI peut créer sa propre délégation ; un Directeur peut déléguer pour les collaborateurs de sa direction ; le DSI désigne le délégataire d'un Chef de Projet absent. Renseignez délégant, délégataire et période de validité."
        },
        {
            title: "Remplacement par le Responsable Solution IT",
            purpose: "Le Responsable Solution IT peut agir directement sur tout projet à la place du Chef de Projet affecté, sans délégation formelle : l'historique trace l'action « en remplacement du Chef de Projet ».",
            guidance: "Aucune configuration nécessaire : connectez-vous avec le rôle ResponsableSolutionsIT et intervenez sur le projet. L'audit conserve le nom du Chef de Projet officiellement affecté."
        },
        {
            title: "Changer le chef de projet",
            purpose: "Après validation DSI, le chef de projet peut être affecté ou réaffecté à tout moment tant que le projet n'est pas clôturé. L'historique des changements est conservé.",
            guidance: "Ouvrez la fiche du projet (onglet Synthèse) puis utilisez le bouton de modification du Chef de Projet. Action réservée à la gouvernance DSI et à l'AdminIT."
        },
        {
            title: "Corbeille : supprimer ou restaurer une demande ou un projet",
            purpose: "L'AdminIT peut envoyer une demande ou un projet à la corbeille (suppression réversible) puis le restaurer. Les éléments supprimés disparaissent des listes et des files de validation.",
            guidance: "Dans la liste des demandes ou le portefeuille projets, utilisez le bouton corbeille d'une ligne. Cochez « Afficher les supprimés » dans les filtres pour voir la corbeille et restaurer. Réservé au rôle AdminIT ; chaque action est tracée dans l'audit."
        },
        {
            title: "Cahier des charges obligatoire",
            purpose: "Le cahier des charges est exigé avant la soumission d'une demande et bloque les validations DM et DSI tant qu'il n'est pas déposé.",
            guidance: "Depuis la fiche de la demande, déposez le cahier des charges (PDF ou Word) avant de soumettre. Un brouillon peut être enregistré sans CDC, mais pas soumis."
        },
        {
            title: "Enregistrer un brouillon de demande",
            purpose: "Une demande peut être enregistrée en brouillon pour être complétée plus tard : aucun champ bloquant à ce stade hormis le titre, la direction et le directeur métier.",
            guidance: "Sur le formulaire Nouvelle demande, utilisez « Enregistrer en brouillon ». Retrouvez-le dans Mes Demandes pour le modifier (y compris le Directeur métier) puis le soumettre."
        }
    ];

    function expandQueryWords(words) {
        const expanded = new Set();
        words.forEach((w) => {
            expanded.add(w);
            if (helpSynonyms[w]) {
                expanded.add(helpSynonyms[w]);
            }
        });
        return Array.from(expanded);
    }

    function haystackMatchesWord(haystack, word) {
        if (haystack.includes(word)) {
            return true;
        }
        // Tolère les variations de fin de mot (singulier/pluriel, conjugaisons courtes)
        // en comparant un préfixe suffisamment long pour rester pertinent.
        if (word.length >= 5) {
            const prefix = word.slice(0, word.length - 2);
            return haystack.split(" ").some((token) => token.startsWith(prefix));
        }
        return false;
    }

    function searchHelpCatalog(query) {
        const rawWords = normalize(query).split(" ").filter((w) => w.length > 2);
        if (rawWords.length === 0) {
            return [];
        }

        const words = expandQueryWords(rawWords);
        const candidates = [];

        Object.keys(sectionHelpCatalog).forEach((key) => {
            const entry = sectionHelpCatalog[key];
            candidates.push({
                title: key.replace(/\b\w/g, (c) => c.toUpperCase()),
                purpose: entry.purpose,
                guidance: entry.guidance,
                haystack: normalize(`${key} ${entry.purpose} ${entry.guidance}`)
            });
        });

        routeHelpCatalog.forEach((entry) => {
            candidates.push({
                title: entry.title,
                purpose: entry.purpose,
                guidance: entry.guidance,
                haystack: normalize(`${entry.title} ${entry.purpose} ${entry.guidance}`)
            });
        });

        topicHelpCatalog.forEach((entry) => {
            candidates.push({
                title: entry.title,
                purpose: entry.purpose,
                guidance: entry.guidance,
                haystack: normalize(`${entry.title} ${entry.purpose} ${entry.guidance}`)
            });
        });

        // Avec plusieurs mots significatifs, exiger au moins deux correspondances :
        // le matching par préfixe est volontairement tolérant, un seul mot ne suffit
        // pas à garantir la pertinence (évite les réponses hors sujet).
        const minScore = Math.min(2, rawWords.length);

        return candidates
            .map((candidate) => ({
                candidate,
                score: words.reduce((acc, w) => acc + (haystackMatchesWord(candidate.haystack, w) ? 1 : 0), 0)
            }))
            .filter((scored) => scored.score >= minScore)
            .sort((a, b) => b.score - a.score)
            .slice(0, 3)
            .map((scored) => scored.candidate);
    }

    window.ZeinabHelp = {
        normalize,
        getContext,
        resolveRouteHelp,
        resolveSectionHelp,
        searchHelpCatalog
    };
})();
