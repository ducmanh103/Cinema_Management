/* ==========================================================================
   CINEMAHUB — HIGH-PERFORMANCE LANDING PAGE ENGINE (landing.js)
   60fps/120fps Hardware-Accelerated Choreography, Blueprint & Journal Reader
   ========================================================================== */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        initHeroCarousel();
        initQuickBookingDock();
        initCatalogTabsAndGenres();
        initMiniSeatSimulator();
        initTrailerModal();
        initEditorialJournalModal();
    });

    /* ----------------------------------------------------------------------
       1. HERO SHOWCASE CAROUSEL (35mm Film Gate + 60fps Live Countdown Bar)
       ---------------------------------------------------------------------- */
    function initHeroCarousel() {
        var movies = window.CINEMAHUB_HERO_MOVIES || [];
        if (movies.length <= 1) return;

        var copyWrap = document.querySelector('.lp-hero-copy');
        var backdrop = document.getElementById('heroBackdrop');
        var posterImg = document.getElementById('heroPosterImg');
        var posterBuffer = document.getElementById('heroPosterBuffer');
        var posterFlare = document.getElementById('heroPosterFlare');
        var posterShutter = document.getElementById('heroPosterShutter');
        var frameStampEl = document.getElementById('heroFrameStamp');
        var titleEl = document.getElementById('heroTitle');
        var descEl = document.getElementById('heroDesc');
        var formatEl = document.getElementById('heroFormat');
        var ageEl = document.getElementById('heroAgeBadge');
        var ratingEl = document.getElementById('heroRating');
        var durationEl = document.getElementById('heroDuration');
        var releaseEl = document.getElementById('heroRelease');
        var minPriceEl = document.getElementById('heroMinPrice');
        var quickTimesEl = document.getElementById('heroQuickTimes');
        var genresWrap = document.getElementById('heroGenres');
        var bookBtn = document.getElementById('heroBookBtn');
        var detailBtn = document.getElementById('heroDetailBtn');
        var trailerBtn = document.getElementById('heroTrailerBtn');
        var reelCounterEl = document.getElementById('heroReelCounter');
        var progressLabelEl = document.getElementById('heroProgressLabel');
        var countdownEl = document.getElementById('heroSlideCountdown');
        var timerDotEl = document.getElementById('heroTimerDot');
        var progressBar = document.getElementById('heroSlideProgress');
        var thumbBtns = document.querySelectorAll('.lp-thumb-btn');
        var prevBtn = document.getElementById('heroPrevReel');
        var nextBtn = document.getElementById('heroNextReel');
        var contactSheet = document.getElementById('heroContactSheet');

        var currentIndex = 0;
        var intervalMs = 6000;
        var rafId = null;
        var cycleStartPerf = performance.now();
        var pausedElapsed = 0;
        var isPaused = false;

        // Preload all featured posters & banners for zero-latency switching
        movies.forEach(function (m) {
            if (m.posterUrl) { var p = new Image(); p.src = m.posterUrl; }
            if (m.bannerUrl) { var b = new Image(); b.src = m.bannerUrl; }
        });

        function updateDossierContent(m, idx) {
            var numStr = '' + (idx + 1);
            var totStr = '' + movies.length;

            if (titleEl) titleEl.textContent = m.title;
            if (descEl) descEl.textContent = m.description;
            if (reelCounterEl) reelCounterEl.textContent = 'Phim tâm điểm ' + numStr + ' / ' + totStr;

            if (formatEl) formatEl.textContent = m.format;
            if (ageEl) {
                ageEl.className = 'lp-age-chip';
                ageEl.textContent = m.ageBadgeText;
            }
            if (ratingEl) ratingEl.textContent = m.rating;
            if (durationEl) durationEl.textContent = m.duration + ' phút';
            if (releaseEl) releaseEl.textContent = m.releaseDate;
            if (minPriceEl) minPriceEl.textContent = m.minPrice || '85.000đ';

            if (genresWrap && Array.isArray(m.genres)) {
                genresWrap.innerHTML = m.genres.slice(0, 4).map(function (g) {
                    return '<span class="lp-genre-pill">' + g + '</span>';
                }).join('');
            }

            if (quickTimesEl) {
                var qList = Array.isArray(m.quickTimes) ? m.quickTimes : [];
                var qHtml = qList.slice(0, 4).map(function (qt) {
                    var href = qt.showtimeId && qt.showtimeId > 0
                        ? '/Booking/SelectSeat/' + qt.showtimeId
                        : '/Showtimes?movieId=' + m.id;
                    return '<a href="' + href + '" class="lp-quicktime-chip">' + qt.time + '</a>';
                }).join('');
                quickTimesEl.innerHTML = qHtml;
            }

            if (bookBtn) bookBtn.href = '/Showtimes?movieId=' + m.id;
            if (detailBtn) detailBtn.href = '/Movies/Details/' + m.id;
            if (trailerBtn) {
                trailerBtn.setAttribute('data-trailer', m.trailerUrl);
                trailerBtn.setAttribute('data-title', m.title);
            }
        }

        function triggerFilmGatePosterTransition(m) {
            if (!posterImg) return;
            var previousSrc = posterImg.src;

            if (posterBuffer) {
                posterBuffer.src = previousSrc;
                posterBuffer.classList.remove('is-exiting');
                void posterBuffer.offsetWidth;
                posterBuffer.classList.add('is-exiting');
            }

            if (posterFlare) {
                posterFlare.classList.remove('is-sweeping');
                void posterFlare.offsetWidth;
                posterFlare.classList.add('is-sweeping');
            }

            if (posterShutter) {
                posterShutter.classList.remove('is-flashing');
                void posterShutter.offsetWidth;
                posterShutter.classList.add('is-flashing');
            }

            posterImg.classList.remove('is-entering');
            posterImg.src = m.posterUrl;
            posterImg.alt = m.title;
            void posterImg.offsetWidth;
            posterImg.classList.add('is-entering');
        }

        function renderSlide(idx) {
            var nextIdx = ((idx % movies.length) + movies.length) % movies.length;
            var m = movies[nextIdx];
            if (!m) return;
            currentIndex = nextIdx;

            // 1. Staggered Dossier Text Choreography
            if (copyWrap) {
                copyWrap.classList.add('is-switching');
                setTimeout(function () {
                    updateDossierContent(m, nextIdx);
                    requestAnimationFrame(function () {
                        copyWrap.classList.remove('is-switching');
                    });
                }, 170);
            } else {
                updateDossierContent(m, nextIdx);
            }

            // 2. 35mm Film Gate Dual-Buffer Poster + Anamorphic Light Flare
            triggerFilmGatePosterTransition(m);

            // 3. Atmospheric Blurred Poster Backdrop Crossfade
            if (backdrop) {
                var nextBg = m.posterUrl || m.bannerUrl;
                backdrop.style.transition = 'opacity 0.42s cubic-bezier(0.22, 1, 0.36, 1), transform 5.5s ease-out';
                backdrop.style.opacity = '0.28';
                backdrop.style.transform = 'scale(1.06)';
                setTimeout(function () {
                    backdrop.style.backgroundImage = "url('" + nextBg + "')";
                    requestAnimationFrame(function () {
                        backdrop.style.opacity = '1';
                        backdrop.style.transform = 'scale(1.16)';
                    });
                }, 140);
            }

            // 4. Update Thumbnail Strip Active State & Reset Thumbnail Progress Bars
            thumbBtns.forEach(function (btn, bIdx) {
                var isAct = bIdx === nextIdx;
                btn.classList.toggle('active', isAct);
                var tBar = btn.querySelector('.lp-thumb-progress');
                if (tBar) tBar.style.width = '0%';
            });

            // 5. Reset 60fps Countdown Clock
            cycleStartPerf = performance.now();
            pausedElapsed = 0;
        }

        function tickProgress(now) {
            if (!isPaused) {
                var elapsed = now - cycleStartPerf;
                var pct = Math.min((elapsed / intervalMs) * 100, 100);
                var remainingSec = Math.max((intervalMs - elapsed) / 1000, 0);

                if (progressBar) {
                    progressBar.style.width = pct.toFixed(2) + '%';
                }

                var activeThumbBar = thumbBtns[currentIndex]
                    ? thumbBtns[currentIndex].querySelector('.lp-thumb-progress')
                    : null;
                if (activeThumbBar) {
                    activeThumbBar.style.width = pct.toFixed(2) + '%';
                }

                if (countdownEl) {
                    var secFormatted = remainingSec < 10
                        ? '0' + remainingSec.toFixed(1) + 's'
                        : remainingSec.toFixed(1) + 's';
                    countdownEl.textContent = secFormatted;
                }

                if (elapsed >= intervalMs) {
                    renderSlide((currentIndex + 1) % movies.length);
                }
            }
            rafId = requestAnimationFrame(tickProgress);
        }

        thumbBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var idx = parseInt(btn.getAttribute('data-slide-idx'), 10);
                if (!isNaN(idx) && idx !== currentIndex) {
                    renderSlide(idx);
                } else {
                    cycleStartPerf = performance.now();
                }
            });
        });

        if (prevBtn) {
            prevBtn.addEventListener('click', function () {
                renderSlide(currentIndex - 1);
            });
        }
        if (nextBtn) {
            nextBtn.addEventListener('click', function () {
                renderSlide(currentIndex + 1);
            });
        }

        if (contactSheet) {
            contactSheet.addEventListener('mouseenter', function () {
                if (!isPaused) {
                    isPaused = true;
                    pausedElapsed = performance.now() - cycleStartPerf;
                    if (timerDotEl) timerDotEl.classList.add('is-paused');
                }
            });
            contactSheet.addEventListener('mouseleave', function () {
                if (isPaused) {
                    isPaused = false;
                    cycleStartPerf = performance.now() - pausedElapsed;
                    if (timerDotEl) timerDotEl.classList.remove('is-paused');
                }
            });
        }

        cycleStartPerf = performance.now();
        rafId = requestAnimationFrame(tickProgress);
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
            theaterSel.innerHTML = '<option value="">— Chọn cụm rạp —</option>';
            dateSel.innerHTML = '<option value="">— Chọn rạp trước —</option>';
            timeSel.innerHTML = '<option value="">— Chọn ngày trước —</option>';
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
            dateSel.innerHTML = '<option value="">— Chọn ngày chiếu —</option>';
            timeSel.innerHTML = '<option value="">— Chọn ngày trước —</option>';
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
            timeSel.innerHTML = '<option value="">— Chọn suất chiếu —</option>';
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
       3. MOVIE CATALOG TABS & GENRE FILTERING (Staggered Entrance Animation)
       ---------------------------------------------------------------------- */
    function initCatalogTabsAndGenres() {
        var tabBtns = document.querySelectorAll('.lp-tab-btn');
        var gridNow = document.getElementById('gridNowShowing');
        var gridSoon = document.getElementById('gridComingSoon');
        var genrePills = document.querySelectorAll('.lp-genre-filter-pill, .lp-genre-filter-btn');

        if (!gridNow || !gridSoon) return;

        function animateVisibleCards(gridEl) {
            var cards = gridEl.querySelectorAll('.lp-movie-card');
            var visibleIdx = 0;
            cards.forEach(function (card) {
                if (card.style.display !== 'none') {
                    card.classList.remove('is-filtering-in');
                    void card.offsetWidth;
                    card.style.animationDelay = (visibleIdx * 45) + 'ms';
                    card.classList.add('is-filtering-in');
                    visibleIdx++;
                }
            });
        }

        tabBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var tab = btn.getAttribute('data-catalog-tab');
                tabBtns.forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');

                if (tab === 'soon') {
                    gridNow.classList.add('d-none');
                    gridSoon.classList.remove('d-none');
                    animateVisibleCards(gridSoon);
                } else {
                    gridSoon.classList.add('d-none');
                    gridNow.classList.remove('d-none');
                    animateVisibleCards(gridNow);
                }
            });
        });

        genrePills.forEach(function (pill) {
            pill.addEventListener('click', function () {
                genrePills.forEach(function (p) { p.classList.remove('active'); });
                pill.classList.add('active');

                var selectedGenre = (pill.getAttribute('data-genre') || pill.getAttribute('data-genre-filter') || 'ALL').toUpperCase();
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
                animateVisibleCards(activeGrid);
            });
        });
    }

    /* ----------------------------------------------------------------------
       4. INTERACTIVE AUDITORIUM BLUEPRINT SEAT SIMULATOR
       ---------------------------------------------------------------------- */
    function initMiniSeatSimulator() {
        var seats = document.querySelectorAll('.lp-mini-seat:not(:disabled)');
        var rows = document.querySelectorAll('.lp-mini-seat-row');
        var countEl = document.getElementById('simulatorSeatCount');
        var textEl = document.getElementById('demoSelectedSeatsText');
        var bookCta = document.getElementById('simulatorBookCta');
        if (!seats.length) return;

        function updateSimulator() {
            var selectedSeats = document.querySelectorAll('.lp-mini-seat.selected');
            var count = selectedSeats.length;
            var codes = [];
            var totalPrice = 0;

            selectedSeats.forEach(function (s) {
                codes.push(s.getAttribute('data-seat') || s.textContent.trim());
                var p = parseInt(s.getAttribute('data-price') || '90000', 10);
                totalPrice += isNaN(p) ? 90000 : p;
            });

            rows.forEach(function (row) {
                var hasSel = row.querySelectorAll('.lp-mini-seat.selected').length > 0;
                row.classList.toggle('has-selection', hasSel);
            });

            if (countEl) countEl.textContent = count + ' ghế';
            if (textEl) {
                if (count > 0) {
                    textEl.innerHTML = codes.join(', ') + ' &bull; <span id="simulatorPrice" style="color:var(--lp-gold);">' + totalPrice.toLocaleString('vi-VN') + ' ₫</span>';
                } else {
                    textEl.innerHTML = 'Chưa chọn ghế &bull; <span id="simulatorPrice" style="color:var(--lp-gold);">0 ₫</span>';
                }
            }
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
                seat.classList.remove('is-popping');
                void seat.offsetWidth;
                seat.classList.add('is-popping');
                updateSimulator();
            });
        });

        updateSimulator();
    }

    /* ----------------------------------------------------------------------
       5. OFFICIAL YOUTUBE TRAILER MODAL
       ---------------------------------------------------------------------- */
    function initTrailerModal() {
        var modalEl = document.getElementById('landingTrailerModal') || document.getElementById('trailerModal');
        var iframe = document.getElementById('landingTrailerIframe') || document.getElementById('trailerIframe');
        var titleEl = document.getElementById('landingTrailerTitle') || document.getElementById('trailerModalTitle');
        if (!modalEl || !iframe) return;

        function openTrailer(url, title) {
            if (!url) return;
            var cleanUrl = url;
            if (cleanUrl.indexOf('autoplay=1') === -1) {
                cleanUrl += (cleanUrl.indexOf('?') === -1 ? '?' : '&') + 'autoplay=1&rel=0';
            }
            iframe.src = cleanUrl;
            if (titleEl) {
                titleEl.innerHTML = '<i class="bi bi-play-circle-fill text-warning me-2"></i>' + (title ? 'Trailer: ' + title : 'Trailer Phim Chính Thức');
            }
            if (window.bootstrap && bootstrap.Modal) {
                var bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                bsModal.show();
            }
        }

        document.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-trailer], .js-open-trailer, #heroTrailerBtn');
            if (!btn) return;
            e.preventDefault();
            var url = btn.getAttribute('data-trailer');
            var title = btn.getAttribute('data-title') || btn.getAttribute('aria-label') || '';
            openTrailer(url, title);
        });

        modalEl.addEventListener('hidden.bs.modal', function () {
            iframe.src = '';
        });
    }

    /* ----------------------------------------------------------------------
       6. MOVIE BLOG & ARTICLE READER MODAL
       ---------------------------------------------------------------------- */
    function initEditorialJournalModal() {
        var modalEl = document.getElementById('editorialArticleModal');
        if (!modalEl) return;

        var catEl = document.getElementById('articleModalCategory');
        var titleEl = document.getElementById('articleModalTitle');
        var metaEl = document.getElementById('articleModalMeta');
        var imgEl = document.getElementById('articleModalImg');
        var bodyEl = document.getElementById('articleModalBody');

        var articles = {
            'essay-imax': {
                category: 'Gợi ý phim hay cuối tuần',
                title: 'Top Phim Giải Trí Gia Đình & Bom Tấn Hành Động Không Thể Bỏ Lỡ Dịp Cuối Tuần Này',
                meta: 'Biên tập CinemaHub • 5 phút đọc • Chuyên mục: Điểm phim chiếu rạp',
                img: 'https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=1200',
                body: '<p class="mb-3">Cuối tuần luôn là khoảng thời gian tuyệt vời nhất để cả gia đình hoặc nhóm bạn cùng nhau thưởng thức một bộ phim hấp dẫn trên màn ảnh rộng. Từ những bộ phim hoạt hình vui nhộn, giàu tính nhân văn như <strong>Nhím Sonic 3, Zootopia</strong> cho đến các siêu phẩm hành động - viễn tưởng hoành tráng, lịch chiếu tuần này tại CinemaHub mang đến lựa chọn đa dạng cho mọi độ tuổi.</p>' +
                      '<blockquote class="p-3 my-3" style="border-left:3px solid #F59E0B;background:#11141D;border-radius:8px;font-weight:600;color:#F8FAFC;">"Đi xem phim tại rạp không chỉ là theo dõi một câu chuyện, mà là khoảng thời gian gắn kết trọn vẹn bên gia đình và người thân."</blockquote>' +
                      '<p class="mb-3">Đối với các gia đình có trẻ nhỏ, các suất chiếu ban ngày và đầu giờ tối (từ 18:00 – 20:00) có đầy đủ phiên bản <strong>2D Lồng tiếng Việt</strong> rõ ràng, dễ theo dõi cùng phân loại độ tuổi <strong>P (Phổ biến mọi lứa tuổi)</strong>.</p>' +
                      '<p class="mb-0">Bạn có thể chọn nhanh bộ phim yêu thích ngay tại thanh <strong>Đặt Vé Nhanh Trực Tuyến</strong> ở đầu trang để giữ trước những hàng ghế trung tâm đẹp nhất!</p>'
            },
            'essay-anamorphic': {
                category: 'Cẩm nang khán giả đi rạp',
                title: 'Đi Xem Phim Cùng Trẻ Nhỏ & Gia Đình: Nên Chọn Suất Chiếu Lồng Tiếng Hay Phụ Đề?',
                meta: 'Kinh nghiệm đi rạp • 4 phút đọc',
                img: 'https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=1200',
                body: '<p class="mb-3">Khi đặt vé xem các bộ phim hoạt hình hoặc phiêu lưu gia đình như <strong>Nhím Sonic 3</strong>, nhiều phụ huynh thường phân vân giữa suất chiếu <strong>Lồng tiếng</strong> và <strong>Phụ đề tiếng Việt</strong>.</p>' +
                      '<p class="mb-3">Nếu đi cùng các bé dưới 10 tuổi, phiên bản <strong>Lồng tiếng Việt</strong> giúp các bé dễ dàng nắm bắt trọn vẹn tình tiết hài hước mà không cần đọc chữ nhanh trên màn hình. Ngược lại, nếu bạn yêu thích giọng thoại gốc của dàn diễn viên quốc tế, các suất chiếu <strong>2D Phụ đề</strong> hoặc <strong>IMAX</strong> vào khung giờ tối sẽ là lựa chọn hoàn hảo.</p>' +
                      '<p class="mb-0">Trên trang Lịch Chiếu của CinemaHub, định dạng chiếu luôn được ghi chú rõ ràng bên cạnh từng khung giờ để bạn lựa chọn thuận tiện nhất.</p>'
            },
            'essay-sweetspot': {
                category: 'Kinh nghiệm chọn ghế đẹp',
                title: 'Bí Quyết Chọn Vị Trí Ghế "Đẹp Nhất Rạp": Vì Sao Hàng Ghế Giữa Luôn Hết Sớm?',
                meta: 'Mẹo đặt vé • 3 phút đọc',
                img: 'https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=1200',
                body: '<p class="mb-3">Nếu để ý sơ đồ chọn ghế của các suất chiếu đông khách, bạn sẽ thấy các ghế ở khu vực <strong>hàng C, D, E (số ghế từ 4 đến 7)</strong> luôn được đặt trước tiên. Đây là khu vực trung tâm cách màn hình khoảng 2/3 chiều sâu phòng chiếu.</p>' +
                      '<p class="mb-3">Ở vị trí này, tầm mắt của bạn thẳng hàng với tâm màn chiếu, giúp bao quát toàn bộ khung hình lẫn dòng phụ đề mà không phải ngước cổ hay xoay đầu. Đồng thời, đây cũng là điểm giao thoa âm thanh chuẩn nhất từ hệ thống loa vòm quanh rạp.</p>' +
                      '<p class="mb-0">Mẹo nhỏ: Hãy đặt vé trực tuyến trên CinemaHub trước giờ chiếu từ 2–4 tiếng để luôn chủ động chọn được cặp ghế trung tâm ưng ý nhất.</p>'
            },
            'essay-restoration': {
                category: 'Tiện ích đặt vé trực tuyến',
                title: 'Hướng Dẫn Đặt Vé Trực Tuyến & Sử Dụng Mã Vé Điện Tử QR Vào Rạp Không Cần Xếp Hàng',
                meta: 'Hướng dẫn khách hàng • 3 phút đọc',
                img: 'https://images.unsplash.com/photo-1440404653325-ab127d49abc1?w=1200',
                body: '<p class="mb-3">Thay vì phải đến rạp sớm để xếp hàng mua vé giấy tại quầy, bạn có thể hoàn tất việc chọn suất chiếu và vị trí ghế ngồi yêu thích ngay trên website CinemaHub chỉ trong chưa đầy 1 phút.</p>' +
                      '<p class="mb-3">Sau khi thanh toán thành công qua VNPAY-QR hoặc thẻ ngân hàng, hệ thống sẽ cấp ngay <strong>Mã vé điện tử QR</strong> trong mục <strong>Vé đã đặt</strong> và gửi bản sao về Email cá nhân của bạn.</p>' +
                      '<p class="mb-0">Khi đến rạp, bạn chỉ cần mở màn hình điện thoại có hiển thị mã QR để nhân viên quét trực tiếp tại lối vào phòng chiếu — nhanh chóng, chính xác và hoàn toàn không cần in vé giấy.</p>'
            }
        };

        document.addEventListener('click', function (e) {
            var trigger = e.target.closest('.js-read-article');
            if (!trigger) return;
            e.preventDefault();
            var id = trigger.getAttribute('data-article-id') || 'essay-imax';
            var data = articles[id] || articles['essay-imax'];

            if (catEl) catEl.textContent = data.category;
            if (titleEl) titleEl.textContent = data.title;
            if (metaEl) metaEl.textContent = data.meta;
            if (imgEl) {
                imgEl.src = data.img;
                imgEl.alt = data.title;
            }
            if (bodyEl) bodyEl.innerHTML = data.body;

            if (window.bootstrap && bootstrap.Modal) {
                var bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                bsModal.show();
            }
        });
    }
})();
