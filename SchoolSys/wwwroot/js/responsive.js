/* =====================================================================
   طبقة الاستجابة — تحويل الجداول إلى بطاقات على الجوال + مساعدات لمس
   ===================================================================== */
(function () {
    'use strict';

    /* ---------------------------------------------------------------
       نسخ عناوين الأعمدة إلى الخلايا حتى تظهر عند تكديس الجدول
       --------------------------------------------------------------- */
    function labelTables(root) {
        var tables = (root || document).querySelectorAll('table.table:not(.no-stack):not([data-stacked])');

        tables.forEach(function (table) {
            var head = table.tHead;
            if (!head || !head.rows.length) return;

            var headers = Array.prototype.map.call(head.rows[0].cells, function (th) {
                return (th.textContent || '').trim();
            });

            Array.prototype.forEach.call(table.tBodies, function (body) {
                Array.prototype.forEach.call(body.rows, function (row) {
                    var i = 0;
                    Array.prototype.forEach.call(row.cells, function (cell) {
                        // خلية ممتدة (رسالة "لا توجد بيانات") — بلا عنوان
                        if (cell.hasAttribute('colspan')) {
                            i += parseInt(cell.getAttribute('colspan'), 10) || 1;
                            return;
                        }
                        cell.setAttribute('data-label', headers[i] || '');
                        i++;
                    });
                });
            });

            table.classList.add('table-stack');
            table.setAttribute('data-stacked', '1');

            // السماح للبطاقات بالظهور خارج حاوية التمرير على الجوال
            var host = table.closest('.table-responsive');
            if (host) host.classList.add('table-stack-host');
        });
    }

    labelTables();

    /* ---------------------------------------------------------------
       طيّ بطاقات الفلاتر على الجوال لتوفير المساحة
       تبقى مفتوحة تلقائياً إذا كان هناك فلتر مُفعّل
       --------------------------------------------------------------- */
    if (window.matchMedia('(max-width: 767.98px)').matches) {
        document.querySelectorAll('.card > .card-body > form[method="get"]').forEach(function (form) {
            var fields = form.querySelectorAll('input, select, textarea');

            // نماذج بسيطة (اختيار واحد) تبقى ظاهرة
            var real = Array.prototype.filter.call(fields, function (f) {
                return f.name && f.type !== 'submit' && f.type !== 'button' && f.type !== 'hidden';
            });
            if (real.length < 3) return;

            var active = 0;
            real.forEach(function (f) {
                if (f.type === 'checkbox' || f.type === 'radio') {
                    if (f.checked) active++;
                } else if (f.value && f.value.trim() !== '') {
                    active++;
                }
            });

            var body = form.parentElement;
            var card = body.parentElement;
            if (!card || !card.classList.contains('card')) return;

            var bar = document.createElement('button');
            bar.type = 'button';
            bar.className = 'filter-toggle';
            bar.setAttribute('aria-expanded', active > 0 ? 'true' : 'false');
            bar.innerHTML =
                '<span class="ft-left"><i class="bi bi-funnel"></i> الفلاتر والبحث' +
                (active > 0 ? ' <span class="chip tone-brand">' + active + '</span>' : '') +
                '</span><i class="bi bi-chevron-down ft-caret"></i>';

            body.classList.add('filter-body');
            card.insertBefore(bar, body);

            if (active === 0) body.classList.add('collapsed');
            else bar.classList.add('open');

            bar.addEventListener('click', function () {
                var nowCollapsed = body.classList.toggle('collapsed');
                bar.classList.toggle('open', !nowCollapsed);
                bar.setAttribute('aria-expanded', String(!nowCollapsed));
            });
        });
    }

    /* ---------------------------------------------------------------
       مؤشر التمرير للجداول التي تبقى أفقية
       --------------------------------------------------------------- */
    document.querySelectorAll('.table-responsive').forEach(function (el) {
        if (el.scrollWidth > el.clientWidth + 8) el.classList.add('has-scroll');
    });

    /* ---------------------------------------------------------------
       إغلاق القائمة الجانبية
       --------------------------------------------------------------- */
    var sidebar = document.getElementById('appSidebar');
    var backdrop = document.getElementById('sidebarBackdrop');

    document.getElementById('sidebarClose')?.addEventListener('click', function () {
        sidebar?.classList.remove('open');
        backdrop?.classList.remove('show');
    });

    // فتح القائمة من شريط التنقل السفلي
    document.getElementById('mobileMenuBtn')?.addEventListener('click', function () {
        sidebar?.classList.toggle('open');
        backdrop?.classList.toggle('show');
    });

    // إغلاق القائمة عند اختيار رابط منها
    sidebar?.querySelectorAll('.nav-link').forEach(function (a) {
        a.addEventListener('click', function () {
            if (window.innerWidth < 992) {
                sidebar.classList.remove('open');
                backdrop?.classList.remove('show');
            }
        });
    });

    // إغلاق بمفتاح Escape
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && sidebar?.classList.contains('open')) {
            sidebar.classList.remove('open');
            backdrop?.classList.remove('show');
        }
    });

    /* ---------------------------------------------------------------
       سحب من الحافة لفتح/إغلاق القائمة (RTL: القائمة على اليمين)
       --------------------------------------------------------------- */
    if (sidebar && window.matchMedia('(max-width: 991.98px)').matches) {
        var startX = null, startY = null, tracking = false;

        document.addEventListener('touchstart', function (e) {
            if (e.touches.length !== 1) return;
            var t = e.touches[0];
            startX = t.clientX;
            startY = t.clientY;
            // نتتبّع فقط عند البدء من حافة الشاشة اليمنى أو عندما تكون القائمة مفتوحة
            tracking = sidebar.classList.contains('open') || startX > window.innerWidth - 28;
        }, { passive: true });

        document.addEventListener('touchend', function (e) {
            if (!tracking || startX === null) return;

            var t = e.changedTouches[0];
            var dx = t.clientX - startX;
            var dy = Math.abs(t.clientY - startY);

            // حركة أفقية واضحة فقط
            if (dy < 60 && Math.abs(dx) > 65) {
                if (dx < 0 && !sidebar.classList.contains('open')) {
                    sidebar.classList.add('open');
                    backdrop?.classList.add('show');
                } else if (dx > 0 && sidebar.classList.contains('open')) {
                    sidebar.classList.remove('open');
                    backdrop?.classList.remove('show');
                }
            }

            startX = startY = null;
            tracking = false;
        }, { passive: true });
    }

    /* ---------------------------------------------------------------
       تمييز العنصر النشط في شريط التنقل السفلي
       --------------------------------------------------------------- */
    var path = window.location.pathname.toLowerCase();
    document.querySelectorAll('.mobile-nav a[data-match]').forEach(function (a) {
        var m = a.getAttribute('data-match').toLowerCase();
        if (path === '/' ? m === '/' : path.indexOf(m) === 0) a.classList.add('active');
    });

    /* ---------------------------------------------------------------
       عدّاد الإشعارات في الشريط السفلي (يتبع عدّاد الشريط العلوي)
       --------------------------------------------------------------- */
    var topCount = document.getElementById('notifCount');
    var mobBadge = document.getElementById('mobileNotifBadge');

    if (topCount && mobBadge) {
        var sync = function () {
            var hidden = topCount.classList.contains('d-none');
            mobBadge.textContent = topCount.textContent;
            mobBadge.classList.toggle('d-none', hidden);
        };
        new MutationObserver(sync).observe(topCount, {
            childList: true, characterData: true, subtree: true, attributes: true
        });
        sync();
    }

    /* ---------------------------------------------------------------
       إعادة الحساب عند تغيير الاتجاه
       --------------------------------------------------------------- */
    window.addEventListener('orientationchange', function () {
        setTimeout(function () {
            document.querySelectorAll('.table-responsive').forEach(function (el) {
                el.classList.toggle('has-scroll', el.scrollWidth > el.clientWidth + 8);
            });
        }, 250);
    });

    // إتاحة الدالة لإعادة الاستخدام بعد تحميل محتوى ديناميكي
    window.labelTables = labelTables;
})();
