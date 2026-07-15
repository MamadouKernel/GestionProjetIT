(function () {
    'use strict';

    const cfg = window.ZeinabLayout || {};
    const HISTORY_KEY = 'zeinabAssistantHistory';
    const HISTORY_MAX = 30;

    const tabLabels = {
        synthese: 'Synthèse',
        analyse: 'Analyse',
        planification: 'Planification',
        execution: 'Exécution',
        uat: 'UAT & MEP',
        cloture: 'Clôture'
    };

    function byId(id) {
        return document.getElementById(id);
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    function getCurrentProjetId() {
        const match = window.location.pathname.match(
            /\/Projet\/(?:Details|CharteProjet|FicheProjet)\/([0-9a-fA-F-]{36})/
        );
        return match ? match[1] : null;
    }

    function getCurrentTab() {
        return new URLSearchParams(window.location.search).get('tab') || 'synthese';
    }

    function loadHistory() {
        try {
            const raw = sessionStorage.getItem(HISTORY_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            return [];
        }
    }

    function saveHistory(history) {
        try {
            sessionStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(-HISTORY_MAX)));
        } catch (e) {
            /* sessionStorage indisponible (navigation privée) : on continue sans mémoriser */
        }
    }

    function addMessage(container, html, sender, history) {
        const bubble = document.createElement('div');
        bubble.className = `assistant-message assistant-message-${sender}`;
        bubble.innerHTML = html;
        container.appendChild(bubble);
        container.scrollTop = container.scrollHeight;

        if (history) {
            history.push({ html, sender });
            saveHistory(history);
        }
    }

    function addLoading(container) {
        const bubble = document.createElement('div');
        bubble.className = 'assistant-message assistant-message-assistant assistant-message-loading';
        bubble.textContent = 'Recherche en cours...';
        container.appendChild(bubble);
        container.scrollTop = container.scrollHeight;
        return bubble;
    }

    function renderAlertes(data) {
        if (!data.alertesComplementaires || data.alertesComplementaires.length === 0) {
            return '';
        }

        return '<div class="assistant-alert-block">' +
            '<strong><i class="bi bi-exclamation-triangle"></i> À surveiller</strong>' +
            '<ul class="assistant-missing-list">' +
            data.alertesComplementaires.map((item) => `<li>${escapeHtml(item)}</li>`).join('') +
            '</ul></div>';
    }

    function renderProchainesEtapes(data) {
        const titre = `<strong>${escapeHtml(data.titre)}</strong> (${escapeHtml(data.codeProjet)})`;
        const phase = `Phase actuelle : <strong>${escapeHtml(data.phaseLabel)}</strong>`;

        if (data.estCloture) {
            return `${titre}<br>${phase}<br>Le projet est clôturé. 🎉` + renderAlertes(data);
        }

        let html = `${titre}<br>${phase}<br>${escapeHtml(data.prochaineAction)}`;
        if (data.elementsManquants && data.elementsManquants.length > 0) {
            html += '<ul class="assistant-missing-list">' +
                data.elementsManquants.map((item) => `<li>${escapeHtml(item)}</li>`).join('') +
                '</ul>';
        }

        const projetId = getCurrentProjetId();
        if (projetId && data.ongletCible && data.ongletCible !== getCurrentTab()) {
            const label = tabLabels[data.ongletCible] || data.ongletCible;
            html += `<a class="assistant-quick-link" href="/Projet/Details/${projetId}?tab=${data.ongletCible}">` +
                `Aller à l'onglet ${escapeHtml(label)} <i class="bi bi-arrow-right"></i></a>`;
        }

        html += renderAlertes(data);

        return html;
    }

    function loadProchainesEtapes(container, history) {
        const projetId = getCurrentProjetId();
        if (!projetId || !cfg.assistantProchainesEtapesUrl) {
            return;
        }

        const loadingBubble = addLoading(container);
        fetch(`${cfg.assistantProchainesEtapesUrl}?projetId=${projetId}`)
            .then((response) => (response.ok ? response.json() : null))
            .then((data) => {
                loadingBubble.remove();
                if (data) {
                    addMessage(container, renderProchainesEtapes(data), 'assistant', history);
                }
            })
            .catch(() => loadingBubble.remove());
    }

    function normalizeQuery(text) {
        if (window.ZeinabHelp && window.ZeinabHelp.normalize) {
            return window.ZeinabHelp.normalize(text);
        }
        return (text || '').toLowerCase();
    }

    function renderActions(actions) {
        if (!actions || actions.length === 0) {
            return '';
        }
        return actions.map((a) =>
            `<a class="assistant-quick-link" href="${a.href}">${escapeHtml(a.label)} <i class="bi bi-arrow-right"></i></a>`
        ).join('<br>');
    }

    function renderSuggestions(questions) {
        return '<div class="assistant-suggestions">' +
            questions.map((q) =>
                `<button type="button" class="assistant-chip" data-assistant-ask="${escapeHtml(q)}">${escapeHtml(q)}</button>`
            ).join('') +
            '</div>';
    }

    const userRoles = cfg.userRoles || [];

    function hasRole(role) {
        return userRoles.indexOf(role) !== -1;
    }

    function hasGouvernanceDsi() {
        return hasRole('DSI') || hasRole('ResponsableSolutionsIT') || hasRole('AdminIT');
    }

    // Suggestions adaptées aux rôles de l'utilisateur connecté : chaque profil
    // voit d'abord les questions qui correspondent à ses propres actions.
    function buildSuggestions() {
        const s = ['Que me manque-t-il pour avancer ?'];
        if (hasRole('DirecteurMetier')) {
            s.push('Comment valider une demande ?', 'Comment déléguer mes validations ?');
        }
        if (hasGouvernanceDsi()) {
            s.push('Comment changer le chef de projet ?', 'Comment déléguer un chef de projet absent ?');
        }
        if (hasRole('ChefDeProjet')) {
            s.push('Comment déposer un livrable ?', 'Comment ajouter un membre à l\'équipe ?');
        }
        if (hasRole('AdminIT')) {
            s.push('Comment restaurer une demande supprimée ?');
        }
        if (s.length === 1 || hasRole('Demandeur')) {
            s.push('Comment créer une demande ?', 'Comment suivre ma demande ?');
        }
        return Array.from(new Set(s)).slice(0, 5);
    }

    // Intentions conversationnelles, testées avant la recherche documentaire.
    // Chaque intention reste déterministe : motifs sur le texte normalisé (sans accents).
    const intents = [
        {
            test: (q) => /\b(bonjour|bonsoir|salut|hello|coucou)\b/.test(q),
            answer: () => ({
                html: 'Bonjour 👋 Je peux vous guider dans vos démarches. Voici quelques questions fréquentes :' +
                    renderSuggestions(buildSuggestions())
            })
        },
        {
            test: (q) => /\bmerci\b|\bparfait\b|\bsuper\b|\btop\b/.test(q),
            answer: () => ({
                html: "Avec plaisir ! N'hésitez pas si vous avez une autre question. 🙂"
            })
        },
        {
            test: (q) => /que (sais|peux)|tes capacites|aide moi|comment tu marches|qui es tu/.test(q),
            answer: () => ({
                html: "Je connais le fonctionnement de Zéïnab : demandes de projet, validations DM/DSI, phases projet, livrables, délégations, clôture... " +
                    'Sur une fiche projet, je calcule aussi ce qui vous manque pour passer à la phase suivante.' +
                    renderSuggestions(buildSuggestions())
            })
        },
        {
            test: (q) => /(manque|prochaine etape|prochaines etapes|avancer|debloqu|bloque|passer (a la|en) phase|pourquoi je ne peux pas)/.test(q),
            handler: (container, history) => {
                if (getCurrentProjetId()) {
                    loadProchainesEtapes(container, history);
                    return true;
                }
                addMessage(container,
                    "Pour vous dire ce qui manque, ouvrez d'abord la fiche d'un projet : j'y analyse automatiquement la phase, les livrables et les validations en attente." +
                    renderActions([{ label: 'Voir mes projets', href: '/Projet/Index' }]),
                    'assistant', history);
                return true;
            }
        },
        {
            test: (q) => /delegu|delegation|absence|absent|remplac/.test(q),
            answer: () => ({
                html: '<strong>Délégation de rôle</strong><p>Un DM ou un DSI peut déléguer ses validations pendant une absence ; le DSI peut désigner un délégataire pour un Chef de Projet. Renseignez le délégataire et la période : toutes ses actions seront tracées « au titre d\'une délégation ». Le Responsable Solution IT, lui, agit sans délégation (tracé « en remplacement »).</p>',
                actions: [{ label: 'Ouvrir les délégations', href: '/Admin/Delegations' }]
            })
        },
        {
            test: (q) => /(supprim|corbeille|restaur)/.test(q) && /(demande|projet)/.test(q),
            answer: () => {
                if (!hasRole('AdminIT')) {
                    return {
                        html: '<strong>Corbeille</strong><p>La suppression et la restauration des demandes et projets sont réservées au rôle AdminIT. Si un élément doit être supprimé ou récupéré, rapprochez-vous d\'un administrateur de la plateforme.</p>'
                    };
                }
                return {
                    html: '<strong>Corbeille</strong><p>Le bouton corbeille d\'une ligne envoie la demande ou le projet en suppression réversible. Cochez « Afficher les supprimés » dans les filtres pour consulter la corbeille et restaurer. Chaque action est tracée.</p>',
                    actions: [
                        { label: 'Corbeille des demandes', href: '/DemandeProjet/Index?afficherSupprimees=true' },
                        { label: 'Corbeille des projets', href: '/Projet/Index?afficherSupprimes=true' }
                    ]
                };
            }
        },
        {
            test: (q) => /(changer|modifier|affecter|assigner).*(chef de projet|chef projet)|chef de projet.*(changer|modifier|affecter)/.test(q),
            answer: () => {
                const projetId = getCurrentProjetId();
                const base = '<strong>Changer le chef de projet</strong><p>Sur la fiche du projet (onglet Synthèse), la gouvernance DSI ou l\'AdminIT peut affecter ou réaffecter le Chef de Projet tant que le projet n\'est pas clôturé. L\'historique des changements est conservé.</p>';
                if (!hasGouvernanceDsi()) {
                    return {
                        html: base + '<p>Votre profil ne permet pas cette action : adressez la demande au DSI ou au Responsable Solution IT.</p>'
                    };
                }
                return {
                    html: base,
                    actions: projetId
                        ? [{ label: 'Ouvrir la synthèse de ce projet', href: `/Projet/Details/${projetId}?tab=synthese` }]
                        : [{ label: 'Voir mes projets', href: '/Projet/Index' }]
                };
            }
        },
        {
            test: (q) => /(membre|equipe)/.test(q) && /(ajout|creer|nouveau|externe|modifi|retir|supprim|enlev|gerer|gestion)/.test(q),
            answer: () => {
                const projetId = getCurrentProjetId();
                const base = '<strong>Gérer l\'équipe projet</strong><p>Sur la fiche du projet (onglet Analyse), bloc « Équipe du projet » : bouton « Ajouter un membre ». Deux options — sélectionner un utilisateur déjà inscrit dans l\'application, ou basculer sur l\'onglet « Nouveau membre externe » pour saisir un prestataire sans compte (nom, prénom, email, direction en texte libre). Le rôle dans le projet est obligatoire dans les deux cas. Modifiez ou retirez ensuite un membre depuis les actions de sa ligne.</p>';
                if (!hasRole('ChefDeProjet') && !hasGouvernanceDsi()) {
                    return {
                        html: base + '<p>Cette action est réservée au Chef de Projet affecté et à la gouvernance DSI (DSI, Responsable Solutions IT, AdminIT).</p>'
                    };
                }
                return {
                    html: base,
                    actions: projetId
                        ? [{ label: 'Ouvrir l\'équipe de ce projet', href: `/Projet/Details/${projetId}?tab=analyse` }]
                        : [{ label: 'Voir mes projets', href: '/Projet/Index' }]
                };
            }
        },
        {
            test: (q) => /(nouvelle|creer|faire une) demande/.test(q),
            answer: () => ({
                html: '<strong>Nouvelle demande de projet</strong><p>Renseignez le contexte, les objectifs et le Directeur métier, joignez le cahier des charges (obligatoire pour soumettre), puis soumettez — ou enregistrez en brouillon pour finir plus tard.</p>',
                actions: [{ label: 'Créer une demande', href: '/DemandeProjet/Create' }]
            })
        },
        {
            test: (q) => /(suivre|suivi|statut|ou en est).*(ma |mes |une )?demande/.test(q),
            answer: () => ({
                html: '<strong>Suivre une demande</strong><p>La colonne Statut de la liste vous indique l\'étape en cours : brouillon, en attente de validation DM, en attente DSI, validée (projet créé) ou renvoyée pour correction. Ouvrez la fiche pour le détail et les commentaires des valideurs.</p>',
                actions: [{ label: 'Voir mes demandes', href: '/DemandeProjet/Index' }]
            })
        },
        {
            test: (q) => /valider.*(demande|projet)|liste.*validation/.test(q) && (hasRole('DirecteurMetier') || hasGouvernanceDsi()),
            answer: () => {
                const actions = [];
                if (hasRole('DirecteurMetier')) {
                    actions.push({ label: 'Mes validations DM', href: '/DemandeProjet/ListeValidationDM' });
                }
                if (hasGouvernanceDsi()) {
                    actions.push({ label: 'Validations DSI', href: '/DemandeProjet/ListeValidationDSI' });
                }
                return {
                    html: '<strong>Valider une demande</strong><p>Depuis votre file de validation, ouvrez la demande, vérifiez le cahier des charges (obligatoire) puis validez, rejetez ou demandez une correction. La validation DM envoie la demande à la DSI ; la validation DSI crée le projet.</p>',
                    actions: actions
                };
            }
        }
    ];

    function answerFromCatalog(query) {
        const help = window.ZeinabHelp;
        if (!help) {
            return "Je ne sais pas encore répondre à ça. Contactez la DSI si besoin.";
        }

        const results = help.searchHelpCatalog(query);
        if (results.length === 0) {
            return "Je n'ai pas trouvé de réponse précise. Essayez avec d'autres mots, ou choisissez une question ci-dessous :" +
                renderSuggestions(buildSuggestions());
        }

        return results.map((r) =>
            `<div class="assistant-answer-block">
                <strong>${escapeHtml(r.title)}</strong>
                <p>${escapeHtml(r.guidance || r.purpose)}</p>
            </div>`
        ).join('');
    }

    // Retourne true si une intention a entièrement pris en charge la question.
    function tryAnswerIntent(rawQuery, container, history) {
        const q = normalizeQuery(rawQuery);
        for (const intent of intents) {
            if (!intent.test(q)) {
                continue;
            }
            if (intent.handler) {
                return intent.handler(container, history);
            }
            const result = intent.answer();
            addMessage(container, result.html + renderActions(result.actions), 'assistant', history);
            return true;
        }
        return false;
    }

    function initPanel() {
        const fab = byId('assistantFab');
        const panel = byId('assistantPanel');
        const closeBtn = byId('assistantPanelClose');
        const form = byId('assistantForm');
        const input = byId('assistantInput');
        const messages = byId('assistantMessages');
        const clearBtn = byId('assistantClear');

        if (!fab || !panel || !form || !input || !messages) {
            return;
        }

        let opened = false;
        let history = loadHistory();

        function renderHistory() {
            messages.innerHTML = '';
            history.forEach((entry) => {
                const bubble = document.createElement('div');
                bubble.className = `assistant-message assistant-message-${entry.sender}`;
                bubble.innerHTML = entry.html;
                messages.appendChild(bubble);
            });
            messages.scrollTop = messages.scrollHeight;
        }

        function openPanel() {
            panel.classList.add('open');
            panel.setAttribute('aria-hidden', 'false');
            if (!opened) {
                opened = true;
                if (history.length > 0) {
                    renderHistory();
                } else {
                    addMessage(messages,
                        "Bonjour 👋 Je peux vous guider dans l'application ou vous dire ce qu'il manque sur le projet courant. Posez une question, ou choisissez une suggestion :" +
                        renderSuggestions(buildSuggestions()),
                        'assistant', history);
                    loadProchainesEtapes(messages, history);
                }
            }
            input.focus();
        }

        function closePanel() {
            panel.classList.remove('open');
            panel.setAttribute('aria-hidden', 'true');
        }

        fab.addEventListener('click', () => {
            if (panel.classList.contains('open')) {
                closePanel();
            } else {
                openPanel();
            }
        });

        closeBtn?.addEventListener('click', closePanel);

        clearBtn?.addEventListener('click', () => {
            history = [];
            saveHistory(history);
            messages.innerHTML = '';
            opened = false;
            openPanel();
        });

        function ask(value) {
            addMessage(messages, escapeHtml(value), 'user', history);

            if (tryAnswerIntent(value, messages, history)) {
                return;
            }

            addMessage(messages, answerFromCatalog(value), 'assistant', history);
        }

        form.addEventListener('submit', (event) => {
            event.preventDefault();
            const value = input.value.trim();
            if (!value) {
                return;
            }
            input.value = '';
            ask(value);
        });

        // Les suggestions cliquables sont recréées à chaque rendu (y compris depuis
        // l'historique) : délégation d'événement sur le conteneur, pas de handler inline.
        messages.addEventListener('click', (event) => {
            const chip = event.target.closest('[data-assistant-ask]');
            if (chip) {
                ask(chip.dataset.assistantAsk);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initPanel);
    } else {
        initPanel();
    }
})();
