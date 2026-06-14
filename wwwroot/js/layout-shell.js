(function () {
    'use strict';

    const cfg = window.ZeinabLayout || {};

    function byId(id) {
        return document.getElementById(id);
    }

    function removeSessionModal() {
        document.querySelector('.session-warning-modal')?.remove();
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    function toggleSidebar() {
        const sidebar = byId('sidebar');
        const overlay = byId('sidebarOverlay');
        if (!sidebar || !overlay) {
            return;
        }

        const open = sidebar.classList.toggle('open');
        overlay.classList.toggle('active', open);
        document.body.style.overflow = open ? 'hidden' : '';
    }

    function toggleSidebarDesktop() {
        const sidebar = byId('sidebar');
        if (!sidebar) {
            return;
        }

        const collapsed = sidebar.classList.toggle('collapsed');
        document.body.classList.toggle('sidebar-collapsed', collapsed);
        localStorage.setItem('sidebarCollapsed', collapsed ? '1' : '0');

        const btn = byId('sidebarCollapseBtn');
        if (btn) {
            btn.title = collapsed ? 'Developper le menu' : 'Reduire le menu';
        }
    }

    function restoreSidebarState() {
        if (window.innerWidth >= 769 && localStorage.getItem('sidebarCollapsed') === '1') {
            byId('sidebar')?.classList.add('collapsed');
            document.body.classList.add('sidebar-collapsed');
        }
    }

    function initSidebar() {
        restoreSidebarState();
        byId('sidebarOverlay')?.addEventListener('click', toggleSidebar);
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && byId('sidebar')?.classList.contains('open')) {
                toggleSidebar();
            }
        });
    }

    function initUserDropdown() {
        const userDropdown = byId('userDropdown');
        if (!userDropdown) {
            return;
        }

        if (typeof bootstrap !== 'undefined' && !bootstrap.Dropdown.getInstance(userDropdown)) {
            new bootstrap.Dropdown(userDropdown);
        }

        userDropdown.addEventListener('show.bs.dropdown', () => {
            const chevron = userDropdown.querySelector('.bi-chevron-down');
            if (chevron) {
                chevron.style.transform = 'rotate(180deg)';
            }
        });

        userDropdown.addEventListener('hide.bs.dropdown', () => {
            const chevron = userDropdown.querySelector('.bi-chevron-down');
            if (chevron) {
                chevron.style.transform = 'rotate(0deg)';
            }
        });
    }

    function showSessionWarning(resetTimer) {
        if (document.querySelector('.session-warning-modal')) {
            return;
        }

        const warningModal = document.createElement('div');
        warningModal.className = 'modal fade show session-warning-modal';
        warningModal.style.display = 'block';
        warningModal.style.backgroundColor = 'rgba(0,0,0,0.5)';
        warningModal.innerHTML =
            '<div class="modal-dialog modal-dialog-centered">' +
                '<div class="modal-content" style="border-radius: var(--radius-lg);">' +
                    '<div class="modal-header" style="background: var(--warning); color: white; border-radius: var(--radius-lg) var(--radius-lg) 0 0;">' +
                        '<h5 class="modal-title">' +
                            '<i class="bi bi-exclamation-triangle-fill"></i> Session sur le point d&apos;expirer' +
                        '</h5>' +
                    '</div>' +
                    '<div class="modal-body" style="padding: var(--spacing-lg);">' +
                        '<p>Votre session expirera dans <strong id="countdown">5:00</strong> minutes en raison de l&apos;inactivite.</p>' +
                        '<p class="mb-0">Souhaitez-vous continuer votre session ?</p>' +
                    '</div>' +
                    '<div class="modal-footer">' +
                        '<button type="button" class="btn-modern btn-modern-primary" data-session-action="extend">' +
                            '<i class="bi bi-check-circle"></i> Continuer' +
                        '</button>' +
                        '<button type="button" class="btn-modern btn-modern-secondary" data-session-action="logout">' +
                            '<i class="bi bi-box-arrow-right"></i> Se deconnecter' +
                        '</button>' +
                    '</div>' +
                '</div>' +
            '</div>';

        warningModal.querySelector('[data-session-action="extend"]')?.addEventListener('click', () => extendSession(resetTimer));
        warningModal.querySelector('[data-session-action="logout"]')?.addEventListener('click', logoutNow);
        document.body.appendChild(warningModal);

        let seconds = 300;
        const countdownEl = warningModal.querySelector('#countdown');
        const countdownInterval = setInterval(() => {
            if (!document.body.contains(warningModal)) {
                clearInterval(countdownInterval);
                return;
            }

            seconds -= 1;
            const mins = Math.floor(seconds / 60);
            const secs = seconds % 60;
            if (countdownEl) {
                countdownEl.textContent = `${mins}:${secs.toString().padStart(2, '0')}`;
            }

            if (seconds <= 0) {
                clearInterval(countdownInterval);
                logoutNow();
            }
        }, 1000);
    }

    function extendSession(resetTimer) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const formData = new FormData();
        if (token) {
            formData.append('__RequestVerificationToken', token);
        }

        fetch(cfg.keepAliveUrl, { method: 'POST', body: formData })
            .finally(() => {
                removeSessionModal();
                resetTimer();
            });
    }

    function logoutNow() {
        const reasonInput = byId('logoutReason');
        if (reasonInput) {
            reasonInput.value = 'inactivity';
        }

        byId('logoutForm')?.submit();
    }

    function initInactivityTimer() {
        if (!cfg.isAuthenticated) {
            return;
        }

        let inactivityTimer;
        let warningTimer;
        const inactivityTimeout = 60 * 60 * 1000;
        const warningTime = 5 * 60 * 1000;

        function resetTimer() {
            clearTimeout(inactivityTimer);
            clearTimeout(warningTimer);
            removeSessionModal();

            inactivityTimer = setTimeout(logoutNow, inactivityTimeout);
            warningTimer = setTimeout(() => showSessionWarning(resetTimer), inactivityTimeout - warningTime);
        }

        ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click']
            .forEach((eventName) => document.addEventListener(eventName, resetTimer, true));

        resetTimer();
    }

    function renderNotifications(items) {
        const list = byId('notificationsList');
        if (!list) {
            return;
        }

        list.setAttribute('aria-busy', 'false');
        if (!items.length) {
            list.innerHTML =
                '<div class="notif-state notif-empty">' +
                    '<span class="notif-state-icon"><i class="bi bi-check2-circle"></i></span>' +
                    '<span>Aucune notification non lue</span>' +
                '</div>';
            return;
        }

        list.innerHTML = items.map((notification) => {
            const openUrl = notification.ouvrirUrl || cfg.notificationFallbackUrl;
            return `
                <a href="${escapeHtml(openUrl)}" class="notif-item">
                    <div class="notif-item-content">
                        <div class="notif-item-title">${escapeHtml(notification.titre)}</div>
                        <div class="notif-item-message">${escapeHtml(notification.message)}</div>
                        <div class="notif-item-date">${escapeHtml(notification.dateCreationFormatted)}</div>
                    </div>
                    <span class="notif-item-action" aria-hidden="true">
                        <i class="bi bi-arrow-right"></i>
                    </span>
                </a>`;
        }).join('');
    }

    function renderNotificationsError() {
        const list = byId('notificationsList');
        if (!list) {
            return;
        }

        list.setAttribute('aria-busy', 'false');
        list.innerHTML =
            '<div class="notif-state notif-error">' +
                '<span class="notif-state-icon"><i class="bi bi-wifi-off"></i></span>' +
                '<span>Notifications indisponibles pour le moment</span>' +
            '</div>';
    }

    function loadNotifications() {
        if (!cfg.isAuthenticated) {
            return;
        }

        fetch(cfg.notificationCountUrl)
            .then((response) => response.json())
            .then((data) => {
                const badge = byId('notificationBadge');
                if (!badge) {
                    return;
                }

                const count = Number(data.count || 0);
                if (count > 0) {
                    badge.textContent = count > 99 ? '99+' : count;
                    badge.hidden = false;
                } else {
                    badge.hidden = true;
                }
            })
            .catch((err) => console.error('Erreur chargement notifications:', err));

        fetch(cfg.notificationsUrl)
            .then((response) => response.json())
            .then((data) => renderNotifications(Array.isArray(data) ? data : []))
            .catch((err) => {
                console.error('Erreur chargement liste notifications:', err);
                renderNotificationsError();
            });
    }

    function initNotifications() {
        if (!cfg.isAuthenticated) {
            return;
        }

        loadNotifications();
        setInterval(loadNotifications, 30000);
    }

    function init() {
        initSidebar();
        initUserDropdown();
        initInactivityTimer();
        initNotifications();
    }

    window.toggleSidebar = toggleSidebar;
    window.toggleSidebarDesktop = toggleSidebarDesktop;
    window.extendSession = () => extendSession(() => {});
    window.logoutNow = logoutNow;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
