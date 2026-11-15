/* ==========================================================================
   CINEMAHUB — 35MM ARCHIVAL EDITORIAL MOTION & INTERACTION ENGINE (site.js)
   Hardware-accelerated 60fps choreography, staggered reveals & 3D tilt
   ========================================================================== */
(function () {
    'use strict';

    const prefersReducedMotion = false;

    document.addEventListener('DOMContentLoaded', function () {
        initStaggerGroups();
        initScrollAnimations();
        initLiveCounters();
        initMobileDockActions();
        initMarqueeTicker();
        initInteractiveSpotlight();
        initPerspectiveTilt();
    });

    /* 0. Auto-assign Stagger Delays to Grid Children for Smooth Cascade */
    function initStaggerGroups() {
        if (prefersReducedMotion) return;
        const grids = document.querySelectorAll('.lp-movie-grid, .lp-theater-grid, .lp-rank-list, .lp-journal-stack');
        grids.forEach(function (grid) {
            const items = Array.from(grid.children);
            items.forEach(function (item, idx) {
                if (!item.hasAttribute('data-animate')) {
                    item.setAttribute('data-animate', 'fade-up');
                }
                if (!item.hasAttribute('data-delay')) {
                    item.setAttribute('data-delay', String(Math.min(idx * 65, 360)));
                }
            });
        });
    }

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
            threshold: 0.06,
            rootMargin: '0px 0px -24px 0px'
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

    /* 3. Mobile Dock & Curator AI Trigger Delegation */
    function initMobileDockActions() {
        document.addEventListener('click', function (e) {
            const curatorBtn = e.target.closest('[data-open-curator]');
            if (!curatorBtn) return;
            e.preventDefault();
            const promptText = curatorBtn.getAttribute('data-curator-prompt') || '';
            if (window.CinemaCurator && typeof window.CinemaCurator.open === 'function') {
                window.CinemaCurator.open(promptText);
            } else if (typeof window.toggleCinemaChat === 'function') {
                window.toggleCinemaChat();
            }
        });
    }

    /* 4. Infinite Seamless Marquee Ticker (Desktop + Mobile) */
    function initMarqueeTicker() {
        const wrappers = document.querySelectorAll('.ch-marquee-wrap');
        wrappers.forEach(function (wrap) {
            const tracks = wrap.querySelectorAll('.ch-marquee-track');
            if (tracks.length === 1) {
                const clone = tracks[0].cloneNode(true);
                clone.setAttribute('aria-hidden', 'true');
                wrap.appendChild(clone);
            }
        });
    }

    /* 5. Interactive Warm Amber Spotlight Effect */
    function initInteractiveSpotlight() {
        if (prefersReducedMotion || !window.matchMedia('(pointer: fine)').matches) return;

        const targets = document.querySelectorAll(
            '.cinema-spotlight, .lp-movie-card, .lp-bento-panel, .lp-theater-card, .lp-journal-lead, .lp-journal-card'
        );
        targets.forEach(function (card) {
            card.classList.add('cinema-spotlight');
            let ticking = false;
            card.addEventListener('mousemove', function (e) {
                if (!ticking) {
                    window.requestAnimationFrame(function () {
                        const rect = card.getBoundingClientRect();
                        const x = e.clientX - rect.left;
                        const y = e.clientY - rect.top;
                        card.style.setProperty('--mouse-x', x + 'px');
                        card.style.setProperty('--mouse-y', y + 'px');
                        ticking = false;
                    });
                    ticking = true;
                }
            }, { passive: true });
        });
    }

    /* 6. Buttery Smooth 3D Perspective Tilt on Movie Contact Sheets & Posters */
    function initPerspectiveTilt() {
        if (prefersReducedMotion || !window.matchMedia('(pointer: fine)').matches) return;

        const tiltElements = document.querySelectorAll('.cinema-tilt, .lp-contact-sheet, .md-poster-frame');
        tiltElements.forEach(function (el) {
            let rafId = null;

            el.addEventListener('mousemove', function (e) {
                if (rafId) cancelAnimationFrame(rafId);
                rafId = requestAnimationFrame(function () {
                    const rect = el.getBoundingClientRect();
                    const centerX = rect.left + rect.width / 2;
                    const centerY = rect.top + rect.height / 2;
                    const mouseX = e.clientX - centerX;
                    const mouseY = e.clientY - centerY;

                    const rotateY = (mouseX / (rect.width / 2)) * 4.5;
                    const rotateX = -(mouseY / (rect.height / 2)) * 4.5;

                    el.style.transform = 'perspective(1100px) rotateX(' + rotateX.toFixed(2) + 'deg) rotateY(' + rotateY.toFixed(2) + 'deg) translate3d(0, -4px, 0)';
                });
            }, { passive: true });

            el.addEventListener('mouseleave', function () {
                if (rafId) cancelAnimationFrame(rafId);
                el.style.transition = 'transform 0.55s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.55s ease';
                el.style.transform = 'perspective(1100px) rotateX(0deg) rotateY(0deg) translate3d(0, 0, 0)';
            });

            el.addEventListener('mouseenter', function () {
                el.style.transition = 'transform 0.12s ease-out, box-shadow 0.3s ease';
            });
        });
    }
})();
