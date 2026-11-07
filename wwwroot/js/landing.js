/* ==========================================================================
   CINEMAHUB — HIGH-PERFORMANCE LANDING PAGE ENGINE (landing.js)
   Ultra-smooth 60fps/120fps, zero layout thrashing, hardware accelerated
   ========================================================================== */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        initHeroCarousel();
        initQuickBookingDock();
        initCatalogTabsAndGenres();
        initMiniSeatSimulator();
        initTrailerModal();
    });

    /* ----------------------------------------------------------------------
       1. HERO SHOWCASE CAROUSEL (Lightweight timer + CSS transition)
       ---------------------------------------------------------------------- */
    function initHeroCarousel() {
        var movies = window.CINEMAHUB_HERO_MOVIES || [];
        if (movies.length <= 1) return;

        var backdrop = document.getElementById('heroBackdrop');
        var posterImg = document.getElementById('heroPosterImg');
        var titleEl = document.getElementById('heroTitle');
        var descEl = document.getElementById('heroDesc');
        var formatEl = document.getElementById('heroFormat');
        var ageEl = document.getElementById('heroAgeBadge');
        var ratingEl = document.getElementById('heroRating');
        var durationEl = document.getElementById('heroDuration');
        var releaseEl = document.getElementById('heroRelease');
        var genresWrap = document.getElementById('heroGenres');
        var bookBtn = document.getElementById('heroBookBtn');
        var detailBtn = document.getElementById('heroDetailBtn');
        var trailerBtn = document.getElementById('heroTrailerBtn');
        var progressBar = document.getElementById('heroSlideProgress');
        var thumbBtns = document.querySelectorAll('.lp-thumb-btn');

        var currentIndex = 0;
        var intervalMs = 6500;
        var autoTimer = null;

        function startProgressCss() {
            if (!progressBar) return;
            progressBar.style.transition = 'none';
            progressBar.style.width = '0%';
            // Force browser repaint tick before starting CSS transition
            void progressBar.offsetWidth;
            progressBar.style.transition = 'width ' + (intervalMs / 1000) + 's linear';
            progressBar.style.width = '100%';
        }

        function renderSlide(idx) {
            var m = movies[idx];
            if (!m) return;
            currentIndex = idx;

            if (backdrop) backdrop.style.backgroundImage = "url('" + (m.bannerUrl || m.posterUrl) + "')";
            if (posterImg) {
                posterImg.style.opacity = '0.4';
                setTimeout(function () {
                    posterImg.src = m.posterUrl;
                    posterImg.alt = m.title;
                    posterImg.style.opacity = '1';
                }, 120);
            }

            if (titleEl) titleEl.textContent = m.title;
            if (descEl) descEl.textContent = m.description;
            if (formatEl) formatEl.innerHTML = '<i class="bi bi-badge-hd-fill"></i> ' + m.format;
            if (ageEl) {
                ageEl.className = 'lp-age-chip ' + m.ageBadgeClass;
                ageEl.textContent = m.ageBadgeText;
            }
            if (ratingEl) ratingEl.textContent = m.rating;
            if (durationEl) durationEl.textContent = m.duration + ' phút';
            if (releaseEl) releaseEl.textContent = m.releaseDate;

            if (genresWrap && Array.isArray(m.genres)) {
                genresWrap.innerHTML = m.genres.slice(0, 4).map(function (g) {
                    return '<span class="lp-genre-pill">' + g + '</span>';
                }).join('');
            }

            if (bookBtn) bookBtn.href = '/Showtimes?movieId=' + m.id;
            if (detailBtn) detailBtn.href = '/Movies/Details/' + m.id;
            if (trailerBtn) {
                trailerBtn.setAttribute('data-trailer', m.trailerUrl);
                trailerBtn.setAttribute('data-title', m.title);
            }

            thumbBtns.forEach(function (btn, bIdx) {
                btn.classList.toggle('active', bIdx === idx);
            });

            startProgressCss();
            resetTimer();
        }

        function resetTimer() {
            if (autoTimer) clearInterval(autoTimer);
            autoTimer = setInterval(function () {
                var nextIdx = (currentIndex + 1) % movies.length;
                renderSlide(nextIdx);
            }, intervalMs);
        }

        thumbBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var idx = parseInt(btn.getAttribute('data-slide-idx'), 10);
                if (!isNaN(idx)) renderSlide(idx);
            });
        });

        startProgressCss();
        resetTimer();
    }

    /* ----------------------------------------------------------------------
       2. 4-STEP SMART QUICK BOOKING DOCK
       ---------------------------------------------------------------------- */
    function initQuickBookingDock() {
        var showtimes = window.CINEMAHUB_QUICK_SHOWTIMES || [];
        var movieSel = document.getElementById('qbMovie');
        var theaterSel = document.getElementById('qbTheater');
        var dateSel = document.getElementById('qbDate');
        var timeSel = document.getElementById('qbTime');
        var submitBtn = document.getElementById('qbSubmitBtn');

        if (!movieSel || !theaterSel || !dateSel || !timeSel || !submitBtn) return;

        var uniqueMovies = [];
        showtimes.forEach(function (s) {
            if (s.movieTitle && uniqueMovies.indexOf(s.movieTitle) === -1) {
                uniqueMovies.push(s.movieTitle);
            }
        });

        uniqueMovies.forEach(function (title) {
            var opt = document.createElement('option');
            opt.value = title;
            opt.textContent = title;
            movieSel.appendChild(opt);
        });

        movieSel.addEventListener('change', function () {
            var selectedMovie = movieSel.value;
            theaterSel.innerHTML = '<option value="">-- Chọn cụm rạp --</option>';
            dateSel.innerHTML = '<option value="">-- Chọn ngày trước --</option>';
            timeSel.innerHTML = '<option value="">-- Chọn ngày trước --</option>';
            theaterSel.disabled = !selectedMovie;
            dateSel.disabled = true;
            timeSel.disabled = true;
            submitBtn.disabled = true;

            if (!selectedMovie) return;

            var theaters = [];
            showtimes.filter(function (s) { return s.movieTitle === selectedMovie; })
                     .forEach(function (s) {
                         if (theaters.indexOf(s.theaterName) === -1) theaters.push(s.theaterName);
                     });

            theaters.forEach(function (t) {
                var opt = document.createElement('option');
                opt.value = t;
                opt.textContent = t;
                theaterSel.appendChild(opt);
            });
        });

        theaterSel.addEventListener('change', function () {
            var selectedMovie = movieSel.value;
            var selectedTheater = theaterSel.value;
            dateSel.innerHTML = '<option value="">-- Chọn ngày chiếu --</option>';
            timeSel.innerHTML = '<option value="">-- Chọn ngày trước --</option>';
            dateSel.disabled = !selectedTheater;
            timeSel.disabled = true;
            submitBtn.disabled = true;

            if (!selectedTheater) return;

            var datesMap = {};
            showtimes.filter(function (s) {
                return s.movieTitle === selectedMovie && s.theaterName === selectedTheater;
            }).forEach(function (s) {
                if (!datesMap[s.dateKey]) {
                    datesMap[s.dateKey] = s.dateDisplay;
                }
            });

            Object.keys(datesMap).forEach(function (key) {
                var opt = document.createElement('option');
                opt.value = key;
                opt.textContent = datesMap[key];
                dateSel.appendChild(opt);
            });
        });

        dateSel.addEventListener('change', function () {
            var selectedMovie = movieSel.value;
            var selectedTheater = theaterSel.value;
            var selectedDate = dateSel.value;
            timeSel.innerHTML = '<option value="">-- Chọn suất chiếu --</option>';
            timeSel.disabled = !selectedDate;
            submitBtn.disabled = true;

            if (!selectedDate) return;

            var matched = showtimes.filter(function (s) {
                return s.movieTitle === selectedMovie &&
                       s.theaterName === selectedTheater &&
                       s.dateKey === selectedDate;
            });

            matched.forEach(function (s) {
                var opt = document.createElement('option');
                opt.value = s.showtimeId;
                opt.textContent = s.timeDisplay + ' (' + s.roomName + ' — ' + s.price + ')';
                timeSel.appendChild(opt);
            });
        });

        timeSel.addEventListener('change', function () {
            submitBtn.disabled = !timeSel.value;
        });

        submitBtn.addEventListener('click', function () {
            var showtimeId = timeSel.value;
            if (showtimeId) {
                window.location.href = '/Booking/SelectSeat/' + showtimeId;
            }
        });
    }

    /* ----------------------------------------------------------------------
       3. MOVIE CATALOG TABS & GENRE FILTERING
       ---------------------------------------------------------------------- */
    function initCatalogTabsAndGenres() {
        var tabBtns = document.querySelectorAll('.lp-tab-btn');
        var gridNow = document.getElementById('gridNowShowing');
        var gridSoon = document.getElementById('gridComingSoon');
        var genrePills = document.querySelectorAll('.lp-genre-filter-pill');

        if (!gridNow || !gridSoon) return;

        tabBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var tab = btn.getAttribute('data-catalog-tab');
                tabBtns.forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');

                if (tab === 'soon') {
                    gridNow.classList.add('d-none');
                    gridSoon.classList.remove('d-none');
                } else {
                    gridSoon.classList.add('d-none');
                    gridNow.classList.remove('d-none');
                }
            });
        });

        genrePills.forEach(function (pill) {
            pill.addEventListener('click', function () {
                genrePills.forEach(function (p) { p.classList.remove('active'); });
                pill.classList.add('active');

                var selectedGenre = (pill.getAttribute('data-genre') || 'ALL').toUpperCase();
                var activeGrid = !gridNow.classList.contains('d-none') ? gridNow : gridSoon;
                var cards = activeGrid.querySelectorAll('.lp-movie-card');

                cards.forEach(function (card) {
                    var genres = (card.getAttribute('data-genres') || '').toUpperCase();
                    if (selectedGenre === 'ALL' || genres.indexOf(selectedGenre) !== -1) {
                        card.style.display = '';
                    } else {
                        card.style.display = 'none';
                    }
                });
            });
        });
    }

    /* ----------------------------------------------------------------------
       4. INTERACTIVE 3D MINI SEAT SIMULATOR
       ---------------------------------------------------------------------- */
    function initMiniSeatSimulator() {
        var seats = document.querySelectorAll('.lp-mini-seat:not(:disabled)');
        var countEl = document.getElementById('simulatorSeatCount');
        var priceEl = document.getElementById('simulatorPrice');
        var bookCta = document.getElementById('simulatorBookCta');
        if (!seats.length) return;

        var basePrice = 110000;

        function updateSimulator() {
            var selectedSeats = document.querySelectorAll('.lp-mini-seat.selected');
            var count = selectedSeats.length;
            if (countEl) countEl.textContent = count + ' ghế';
            if (priceEl) priceEl.textContent = (count * basePrice).toLocaleString('vi-VN') + ' ₫';
            if (bookCta) {
                if (count > 0) {
                    bookCta.classList.remove('disabled');
                    bookCta.style.opacity = '1';
                    bookCta.style.pointerEvents = 'auto';
                } else {
                    bookCta.classList.add('disabled');
                    bookCta.style.opacity = '0.5';
                    bookCta.style.pointerEvents = 'none';
                }
            }
        }

        seats.forEach(function (seat) {
            seat.addEventListener('click', function () {
                seat.classList.toggle('selected');
                updateSimulator();
            });
        });
    }

    /* ----------------------------------------------------------------------
       5. OFFICIAL YOUTUBE TRAILER MODAL
       ---------------------------------------------------------------------- */
    function initTrailerModal() {
        var modalEl = document.getElementById('trailerModal');
        var iframe = document.getElementById('trailerIframe');
        var titleEl = document.getElementById('trailerModalTitle');
        if (!modalEl || !iframe) return;

        document.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-trailer]');
            if (!btn) return;
            var url = btn.getAttribute('data-trailer');
            var title = btn.getAttribute('data-title') || 'Trailer Phim';

            if (url) {
                if (url.indexOf('autoplay=1') === -1) {
                    url += (url.indexOf('?') === -1 ? '?' : '&') + 'autoplay=1';
                }
                iframe.src = url;
                if (titleEl) titleEl.textContent = title;
                var bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                bsModal.show();
            }
        });

        modalEl.addEventListener('hidden.bs.modal', function () {
            iframe.src = '';
        });
    }
})();
