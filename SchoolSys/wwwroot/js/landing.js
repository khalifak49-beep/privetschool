/* الصفحة التعريفية — تفاعلات خفيفة */
(function () {
    'use strict';

    var reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ---------- الشريط العلوي عند التمرير ----------
    var nav = document.getElementById('siteNav');
    function onScroll() {
        if (!nav) return;
        nav.classList.toggle('solid', window.scrollY > 40);
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();

    // ---------- قائمة الجوال ----------
    var toggle = document.getElementById('navToggle');
    var links = document.getElementById('navLinks');
    toggle?.addEventListener('click', function () { links?.classList.toggle('open'); });
    links?.querySelectorAll('a').forEach(function (a) {
        a.addEventListener('click', function () { links.classList.remove('open'); });
    });

    // ---------- الظهور عند التمرير ----------
    var reveals = document.querySelectorAll('.reveal');
    if (reduced || !('IntersectionObserver' in window)) {
        reveals.forEach(function (el) { el.classList.add('in'); });
    } else {
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (e) {
                if (e.isIntersecting) {
                    e.target.classList.add('in');
                    io.unobserve(e.target);
                }
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -60px 0px' });

        reveals.forEach(function (el) { io.observe(el); });
    }

    // ---------- عدّادات الأرقام ----------
    var counters = document.querySelectorAll('[data-count]');

    function runCounter(el) {
        var target = parseFloat(el.getAttribute('data-count')) || 0;
        var suffix = el.getAttribute('data-suffix') || '';

        if (reduced) {
            el.textContent = target.toLocaleString('en-US') + suffix;
            return;
        }

        var duration = 1500;
        var start = null;

        function step(ts) {
            if (!start) start = ts;
            var p = Math.min((ts - start) / duration, 1);
            // تسارع ثم تباطؤ
            var eased = p < 0.5 ? 2 * p * p : 1 - Math.pow(-2 * p + 2, 2) / 2;
            el.textContent = Math.round(target * eased).toLocaleString('en-US') + suffix;
            if (p < 1) requestAnimationFrame(step);
        }

        requestAnimationFrame(step);
    }

    if (!('IntersectionObserver' in window)) {
        counters.forEach(runCounter);
    } else {
        var cio = new IntersectionObserver(function (entries) {
            entries.forEach(function (e) {
                if (e.isIntersecting) {
                    runCounter(e.target);
                    cio.unobserve(e.target);
                }
            });
        }, { threshold: 0.4 });

        counters.forEach(function (el) { cio.observe(el); });
    }
})();
