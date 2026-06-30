(function () {
    'use strict';

    const cfg = window.ZeinabLayout || {};

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

    function addMessage(container, text, sender) {
        const bubble = document.createElement('div');
        bubble.className = `assistant-message assistant-message-${sender}`;
        bubble.innerHTML = text;
        container.appendChild(bubble);
        container.scrollTop = container.scrollHeight;
    }

    function addLoading(container) {
        const bubble = document.createElement('div');
        bubble.className = 'assistant-message assistant-message-assistant assistant-message-loading';
        bubble.textContent = 'Recherche en cours...';
        container.appendChild(bubble);
        container.scrollTop = container.scrollHeight;
        return bubble;
    }

    function renderProchainesEtapes(data) {
        const titre = `<strong>${escapeHtml(data.titre)}</strong> (${escapeHtml(data.codeProjet)})`;
        const phase = `Phase actuelle : <strong>${escapeHtml(data.phaseLabel)}</strong>`;

        if (data.estCloture) {
            return `${titre}<br>${phase}<br>Le projet est clôturé. 🎉`;
        }

        let html = `${titre}<br>${phase}<br>${escapeHtml(data.prochaineAction)}`;
        if (data.elementsManquants && data.elementsManquants.length > 0) {
            html += '<ul class="assistant-missing-list">' +
                data.elementsManquants.map((item) => `<li>${escapeHtml(item)}</li>`).join('') +
                '</ul>';
        }
        return html;
    }

    function loadProchainesEtapes(container) {
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
                    addMessage(container, renderProchainesEtapes(data), 'assistant');
                }
            })
            .catch(() => loadingBubble.remove());
    }

    function answerFromCatalog(query) {
        const help = window.ZeinabHelp;
        if (!help) {
            return "Je ne sais pas encore répondre à ça. Contactez la DSI si besoin.";
        }

        const results = help.searchHelpCatalog(query);
        if (results.length === 0) {
            return "Je n'ai pas trouvé de réponse précise. Essayez de reformuler, ou ouvrez le \"Guide de l'écran\" en haut de la page pour une aide contextuelle.";
        }

        return results.map((r) =>
            `<div class="assistant-answer-block">
                <strong>${escapeHtml(r.title)}</strong>
                <p>${escapeHtml(r.guidance || r.purpose)}</p>
            </div>`
        ).join('');
    }

    function initPanel() {
        const fab = byId('assistantFab');
        const panel = byId('assistantPanel');
        const closeBtn = byId('assistantPanelClose');
        const form = byId('assistantForm');
        const input = byId('assistantInput');
        const messages = byId('assistantMessages');

        if (!fab || !panel || !form || !input || !messages) {
            return;
        }

        let opened = false;

        function openPanel() {
            panel.classList.add('open');
            panel.setAttribute('aria-hidden', 'false');
            if (!opened) {
                opened = true;
                addMessage(messages,
                    "Bonjour 👋 Je peux vous aider à naviguer dans l'application ou vous dire ce qu'il manque sur le projet courant. Posez une question, ou attendez le résumé automatique si vous êtes sur une fiche projet.",
                    'assistant');
                loadProchainesEtapes(messages);
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

        form.addEventListener('submit', (event) => {
            event.preventDefault();
            const value = input.value.trim();
            if (!value) {
                return;
            }

            addMessage(messages, escapeHtml(value), 'user');
            input.value = '';

            const answer = answerFromCatalog(value);
            addMessage(messages, answer, 'assistant');
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initPanel);
    } else {
        initPanel();
    }
})();
