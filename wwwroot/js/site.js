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
        initMarqueeTicker();
        initInteractiveSpotlight();
        initPerspectiveTilt();
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

    /* 4. Infinite Seamless Marquee Ticker (Desktop + Mobile) */
    function initMarqueeTicker() {
        const wrappers = document.querySelectorAll('.ch-marquee-wrap');
        wrappers.forEach(function (wrap) {
            const tracks = wrap.querySelectorAll('.ch-marquee-track');
            // Nếu chỉ có 1 track, clone thêm track thứ 2 để chạy loop vô tận không bị hở trên PC
            if (tracks.length === 1) {
                const clone = tracks[0].cloneNode(true);
                clone.setAttribute('aria-hidden', 'true');
                wrap.appendChild(clone);
            }
        });
    }

    /* 5. Interactive Spotlight Effect (Tracks --mouse-x, --mouse-y for cinema-spotlight) */
    function initInteractiveSpotlight() {
        if (prefersReducedMotion || !window.matchMedia('(pointer: fine)').matches) return;

        const spotlights = document.querySelectorAll('.cinema-spotlight');
        spotlights.forEach(function (card) {
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

    /* 6. Buttery Smooth 3D Perspective Tilt on Movie Posters */
    function initPerspectiveTilt() {
        if (prefersReducedMotion || !window.matchMedia('(pointer: fine)').matches) return;

        const tiltElements = document.querySelectorAll('.cinema-tilt, .lp-poster-stage, .md-poster-frame');
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

                    // Giới hạn góc nghiêng tối đa 5 độ để chuyển động êm ái, sang trọng
                    const rotateY = (mouseX / (rect.width / 2)) * 5;
                    const rotateX = -(mouseY / (rect.height / 2)) * 5;

                    el.style.transform = 'perspective(1000px) rotateX(' + rotateX.toFixed(2) + 'deg) rotateY(' + rotateY.toFixed(2) + 'deg) scale3d(1.02, 1.02, 1.02)';
                });
            }, { passive: true });

            el.addEventListener('mouseleave', function () {
                if (rafId) cancelAnimationFrame(rafId);
                el.style.transition = 'transform 0.5s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.5s ease';
                el.style.transform = 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale3d(1, 1, 1)';
            });

            el.addEventListener('mouseenter', function () {
                el.style.transition = 'transform 0.15s ease-out, box-shadow 0.3s ease';
            });
        });
    }
})();
