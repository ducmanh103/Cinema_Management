/* ==========================================================================
   CINEMAHUB PRO MAX — OPTIMIZED GLOBAL MOTION & INTERACTION ENGINE (site.js)
   Hardware-accelerated, 60fps buttery smooth, zero layout thrashing
   ========================================================================== */
(function () {
    'use strict';

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    document.addEventListener('DOMContentLoaded', function () {
        initScrollAnimations();
        initLiveCounters();
        initMobileDockActions();
    });

    /* 1. Scroll Reveal Choreography (IntersectionObserver - Zero Polling) */
    function initScrollAnimations() {
        const elements = document.querySelectorAll('[data-animate]');
        if (!elements.length) return;

        if (prefersReducedMotion || !('IntersectionObserver' in window)) {
            elements.forEach(function (el) { el.classList.add('is-inview'); });
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    const el = entry.target;
                    const delay = parseInt(el.getAttribute('data-delay') || '0', 10);
                    if (delay > 0) {
                        setTimeout(function () { el.classList.add('is-inview'); }, delay);
                    } else {
                        el.classList.add('is-inview');
                    }
                    observer.unobserve(el);
                }
            });
        }, {
            threshold: 0.05,
            rootMargin: '0px 0px -20px 0px'
        });

        elements.forEach(function (el) { observer.observe(el); });
    }

    /* 2. Animated Live Counters ([data-count-to]) */
    function initLiveCounters() {
        const counters = document.querySelectorAll('[data-count-to]');
        if (!counters.length) return;

        const animateCounter = function (el) {
            const target = parseFloat(el.getAttribute('data-count-to') || '0');
            const suffix = el.getAttribute('data-count-suffix') || '';
            const prefix = el.getAttribute('data-count-prefix') || '';
            if (prefersReducedMotion || target <= 0) {
                el.textContent = prefix + target.toLocaleString('vi-VN') + suffix;
                return;
            }

            const duration = 1200;
            const start = performance.now();

            const step = function (now) {
                const progress = Math.min((now - start) / duration, 1);
                // easeOutExpo
                const eased = progress === 1 ? 1 : 1 - Math.pow(2, -10 * progress);
                const current = Math.round(target * eased);
                el.textContent = prefix + current.toLocaleString('vi-VN') + suffix;
                if (progress < 1) {
                    requestAnimationFrame(step);
                } else {
                    el.textContent = prefix + target.toLocaleString('vi-VN') + suffix;
                }
            };
            requestAnimationFrame(step);
        };

        if (!('IntersectionObserver' in window)) {
            counters.forEach(animateCounter);
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    animateCounter(entry.target);
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.15 });

        counters.forEach(function (c) { observer.observe(c); });
    }

    /* 3. Mobile Bottom Dock & AI Assistant Trigger */
    function initMobileDockActions() {
        const dockChatBtn = document.getElementById('mobileDockChatBtn');
        if (dockChatBtn) {
            dockChatBtn.addEventListener('click', function (e) {
                e.preventDefault();
                const trigger = document.getElementById('chatbot-trigger');
                if (trigger) trigger.click();
            });
        }
    }
})();
