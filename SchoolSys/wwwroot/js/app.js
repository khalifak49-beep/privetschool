/* نظام إدارة المدارس — سكربت الواجهة الرئيسي */
(function () {
    'use strict';

    // ------------------ القائمة الجانبية على الشاشات الصغيرة ------------------
    const sidebar = document.getElementById('appSidebar');
    const backdrop = document.getElementById('sidebarBackdrop');
    const toggle = document.getElementById('sidebarToggle');

    function closeSidebar() {
        sidebar?.classList.remove('open');
        backdrop?.classList.remove('show');
    }

    toggle?.addEventListener('click', function () {
        sidebar?.classList.toggle('open');
        backdrop?.classList.toggle('show');
    });
    backdrop?.addEventListener('click', closeSidebar);

    // ------------------ تأكيد الحذف ------------------
    document.addEventListener('click', function (e) {
        const el = e.target.closest('[data-confirm]');
        if (!el) return;
        if (!window.confirm(el.getAttribute('data-confirm') || 'هل أنت متأكد من تنفيذ هذه العملية؟')) {
            e.preventDefault();
            e.stopPropagation();
        }
    });

    // ------------------ إرسال النموذج تلقائياً عند تغيير الفلاتر ------------------
    document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
        el.addEventListener('change', function () { el.closest('form')?.submit(); });
    });

    // ------------------ تحديد الكل في الجداول ------------------
    document.querySelectorAll('[data-check-all]').forEach(function (master) {
        const targetSel = master.getAttribute('data-check-all');
        master.addEventListener('change', function () {
            document.querySelectorAll(targetSel).forEach(function (cb) { cb.checked = master.checked; });
        });
    });

    // ------------------ التنبيهات المنبثقة ------------------
    window.showToast = function (message, tone) {
        tone = tone || 'info';
        let host = document.getElementById('toastHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'toastHost';
            host.className = 'position-fixed top-0 start-0 p-3';
            host.style.zIndex = '1080';
            document.body.appendChild(host);
        }
        const map = { success: 'text-bg-success', danger: 'text-bg-danger', warning: 'text-bg-warning', info: 'text-bg-primary' };
        const el = document.createElement('div');
        el.className = 'toast align-items-center border-0 ' + (map[tone] || map.info);
        el.setAttribute('role', 'alert');
        el.innerHTML =
            '<div class="d-flex"><div class="toast-body">' + message + '</div>' +
            '<button type="button" class="btn-close btn-close-white me-auto m-auto ms-2" data-bs-dismiss="toast"></button></div>';
        host.appendChild(el);
        const t = new bootstrap.Toast(el, { delay: 5000 });
        t.show();
        el.addEventListener('hidden.bs.toast', function () { el.remove(); });
    };

    // ------------------ الإشعارات الفورية ------------------
    const countEl = document.getElementById('notifCount');
    const listEl = document.getElementById('notifList');

    const toneIcon = {
        info: ['tone-info', 'bi-info-circle-fill'],
        success: ['tone-ok', 'bi-check-circle-fill'],
        warning: ['tone-warn', 'bi-exclamation-triangle-fill'],
        danger: ['tone-danger', 'bi-exclamation-octagon-fill']
    };

    function renderNotifications(items) {
        if (!listEl) return;
        if (!items || !items.length) {
            listEl.innerHTML = '<div class="text-center text-muted-3 py-4 fs-sm">لا توجد إشعارات</div>';
            return;
        }
        listEl.innerHTML = items.map(function (n) {
            const tone = toneIcon[(n.severity || 'info').toLowerCase()] || toneIcon.info;
            const body = n.body ? '<div class="b">' + escapeHtml(n.body) + '</div>' : '';
            const inner =
                '<div class="ico ' + tone[0] + '"><i class="bi ' + tone[1] + '"></i></div>' +
                '<div class="flex-grow-1 min-w-0">' +
                '<div class="t">' + escapeHtml(n.title) + '</div>' + body +
                '<div class="d">' + (n.createdAt || '') + '</div></div>';
            return n.link
                ? '<a href="' + n.link + '" class="notif-item text-reset' + (n.isRead ? '' : ' unread') + '">' + inner + '</a>'
                : '<div class="notif-item' + (n.isRead ? '' : ' unread') + '">' + inner + '</div>';
        }).join('');
    }

    function setCount(n) {
        if (!countEl) return;
        if (n > 0) {
            countEl.textContent = n > 99 ? '99+' : n;
            countEl.classList.remove('d-none');
        } else {
            countEl.classList.add('d-none');
        }
    }

    function escapeHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function loadNotifications() {
        if (!listEl) return;
        fetch('/Communication/Latest', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return;
                setCount(data.unread);
                renderNotifications(data.items);
            })
            .catch(function () { /* تجاهل */ });
    }

    document.getElementById('markAllRead')?.addEventListener('click', function () {
        fetch('/Communication/MarkAllRead', {
            method: 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(loadNotifications);
    });

    if (listEl) {
        loadNotifications();

        if (window.signalR) {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/notifications')
                .withAutomaticReconnect()
                .build();

            connection.on('ReceiveNotification', function (n) {
                window.showToast(escapeHtml(n.title), n.severity);
                loadNotifications();
            });

            connection.start().catch(function () { /* الاتصال غير متاح */ });
        }
    }
})();
