/* ==========================================================================
   CINEMAHUB MODERN LANDING PAGE — INTERACTIVE CLIENT LOGIC
   Connected directly to Backend APIs:
   - GET /api/ShowtimesApi/bymovie/{movieId}
   - GET /api/ShowtimesApi/bytheater/{theaterId}
   - POST /api/chatbot/message (via #chatbot-panel integration)
   - GET /Booking/SelectSeat/{showtimeId}
   ========================================================================== */
(function () {
    'use strict';

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const movieShowtimeCache = new Map();
    const theaterShowtimeCache = new Map();

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        const div = document.createElement('div');
        div.textContent = String(str);
        return div.innerHTML;
    }

    function toDateKey(isoStr) {
        const d = new Date(isoStr);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${y}-${m}-${day}`;
    }

    function formatTimeHHMM(isoStr) {
        const d = new Date(isoStr);
        return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    }

    function formatDateChipLabel(dateKey) {
        const parts = dateKey.split('-');
        const d = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const diffDays = Math.round((d - today) / 86400000);
        const ddmm = `${parts[2]}/${parts[1]}`;
        const weekdays = ['CN', 'Th 2', 'Th 3', 'Th 4', 'Th 5', 'Th 6', 'Th 7'];
        const top = diffDays === 0 ? 'Hôm nay' : diffDays === 1 ? 'Ngày mai' : weekdays[d.getDay()];
        return { top, ddmm, full: `${top}, ${ddmm}` };
    }

    function formatPriceVnd(amount) {
        return Number(amount || 0).toLocaleString('vi-VN') + '₫';
    }

    async function fetchMovieShowtimes(movieId) {
        const key = Number(movieId);
        if (movieShowtimeCache.has(key)) return movieShowtimeCache.get(key);
        const res = await fetch(`/api/ShowtimesApi/bymovie/${encodeURIComponent(key)}`, {
            headers: { 'Accept': 'application/json' }
        });
        if (!res.ok) throw new Error('Không thể tải lịch chiếu phim');
        const data = await res.json();
        movieShowtimeCache.set(key, Array.isArray(data) ? data : []);
        return movieShowtimeCache.get(key);
    }

    async function fetchTheaterShowtimes(theaterId) {
        const key = Number(theaterId);
        if (theaterShowtimeCache.has(key)) return theaterShowtimeCache.get(key);
        const res = await fetch(`/api/ShowtimesApi/bytheater/${encodeURIComponent(key)}`, {
            headers: { 'Accept': 'application/json' }
        });
        if (!res.ok) throw new Error('Không thể tải lịch chiếu rạp');
        const data = await res.json();
        theaterShowtimeCache.set(key, Array.isArray(data) ? data : []);
        return theaterShowtimeCache.get(key);
    }

    document.addEventListener('DOMContentLoaded', function () {
        initHeroControls();
        initSmartBookingBar();
        initMovieCatalogFilters();
        initMiniSeatPreview();
        initChatbotPromptChips();
        initQuickShowtimeModal();
        initScrollReveal();
    });

    /* ----------------------------------------------------------------------
       1. Hero Carousel Thumbnail Sync & Pause/Play Control
       ---------------------------------------------------------------------- */
    function initHeroControls() {
        const heroEl = document.getElementById('lpHeroCarousel');
        if (!heroEl || typeof bootstrap === 'undefined') return;

        const thumbs = Array.from(document.querySelectorAll('.js-hero-thumb'));
        const pauseBtn = document.getElementById('lpHeroPauseBtn');
        let isPaused = prefersReducedMotion;

        const carousel = bootstrap.Carousel.getOrCreateInstance(heroEl, {
            interval: prefersReducedMotion ? false : 6500,
            ride: prefersReducedMotion ? false : 'carousel',
            pause: 'hover'
        });

        if (prefersReducedMotion) {
            carousel.pause();
            updatePauseButtonUI(pauseBtn, true);
        }

        heroEl.addEventListener('slide.bs.carousel', function (ev) {
            thumbs.forEach((btn, idx) => {
                const active = idx === ev.to;
                btn.classList.toggle('active', active);
                btn.setAttribute('aria-current', active ? 'true' : 'false');
            });
        });

        thumbs.forEach((btn) => {
            btn.addEventListener('click', function () {
                const idx = Number(btn.getAttribute('data-slide-to') || 0);
                carousel.to(idx);
            });
        });

        if (pauseBtn) {
            pauseBtn.addEventListener('click', function () {
                isPaused = !isPaused;
                if (isPaused) {
                    carousel.pause();
                } else {
                    carousel.cycle();
                }
                updatePauseButtonUI(pauseBtn, isPaused);
            });
        }
    }

    function updatePauseButtonUI(btn, paused) {
        if (!btn) return;
        btn.setAttribute('aria-pressed', paused ? 'true' : 'false');
        btn.setAttribute('aria-label', paused ? 'Tiếp tục chuyển cảnh tự động' : 'Tạm dừng chuyển cảnh tự động');
        btn.title = paused ? 'Phát tự động' : 'Tạm dừng';
        btn.innerHTML = paused
            ? '<i class="bi bi-play-fill" aria-hidden="true"></i>'
            : '<i class="bi bi-pause-fill" aria-hidden="true"></i>';
    }

    /* ----------------------------------------------------------------------
       2. 4-Step Smart Live Booking Bar (Connected to /api/ShowtimesApi)
       ---------------------------------------------------------------------- */
    function initSmartBookingBar() {
        const form = document.getElementById('lpQuickBookingForm');
        const movieSelect = document.getElementById('lpSelectMovie');
        const theaterSelect = document.getElementById('lpSelectTheater');
        const dateSelect = document.getElementById('lpSelectDate');
        const showtimeSelect = document.getElementById('lpSelectShowtime');
        const submitBtn = document.getElementById('lpBookingSubmitBtn');
        const submitText = document.getElementById('lpBookingSubmitText');
        const statusHint = document.getElementById('lpBookingLiveHint');

        if (!form || !movieSelect || !theaterSelect || !dateSelect || !showtimeSelect) return;

        // Snapshot original theater & date options for reset when no movie is chosen
        const initialTheatersHtml = theaterSelect.innerHTML;
        const initialDatesHtml = dateSelect.innerHTML;
        let currentShowtimes = [];

        movieSelect.addEventListener('change', async function () {
            const movieId = movieSelect.value;
            showtimeSelect.innerHTML = '<option value="">-- Chọn suất chiếu --</option>';
            showtimeSelect.disabled = true;
            updateSubmitMode();

            if (!movieId) {
                currentShowtimes = [];
                theaterSelect.innerHTML = initialTheatersHtml;
                dateSelect.innerHTML = initialDatesHtml;
                if (statusHint) statusHint.textContent = 'Chọn phim để tải suất chiếu thời gian thực';
                return;
            }

            if (statusHint) statusHint.textContent = 'Đang đồng bộ suất chiếu khả dụng...';
            try {
                const list = await fetchMovieShowtimes(movieId);
                currentShowtimes = list;

                if (!list.length) {
                    theaterSelect.innerHTML = initialTheatersHtml;
                    dateSelect.innerHTML = initialDatesHtml;
                    showtimeSelect.innerHTML = '<option value="">Chưa có suất sắp tới</option>';
                    if (statusHint) statusHint.textContent = 'Phim hiện chưa có suất chiếu mới — bấm Tìm lịch chiếu để xem thêm';
                    return;
                }

                // Populate theaters that have showtimes for this movie
                const theatersMap = new Map();
                list.forEach((st) => {
                    const tKey = st.theaterId ? String(st.theaterId) : st.theaterName;
                    if (!theatersMap.has(tKey)) {
                        theatersMap.set(tKey, {
                            id: st.theaterId || '',
                            name: st.theaterName,
                            count: 0
                        });
                    }
                    theatersMap.get(tKey).count += 1;
                });

                let tHtml = '<option value="">-- Tất cả rạp có suất (' + theatersMap.size + ') --</option>';
                theatersMap.forEach((t) => {
                    tHtml += `<option value="${escapeHtml(t.id)}" data-theater-name="${escapeHtml(t.name)}">${escapeHtml(t.name)} (${t.count} suất)</option>`;
                });
                theaterSelect.innerHTML = tHtml;

                refreshDatesAndTimes();
                if (statusHint) {
                    statusHint.textContent = `Tìm thấy ${list.length} suất chiếu khả dụng — chọn giờ để vào thẳng sơ đồ ghế`;
                }
            } catch (err) {
                console.error(err);
                if (statusHint) statusHint.textContent = 'Không thể tải nhanh suất chiếu — bạn vẫn có thể bấm Tìm lịch chiếu';
            }
        });

        theaterSelect.addEventListener('change', function () {
            if (currentShowtimes.length > 0) {
                refreshDatesAndTimes();
            }
        });

        dateSelect.addEventListener('change', function () {
            if (currentShowtimes.length > 0) {
                refreshTimesOnly();
            }
        });

        showtimeSelect.addEventListener('change', updateSubmitMode);

        function refreshDatesAndTimes() {
            const selectedTheaterId = theaterSelect.value;
            const filteredByTheater = currentShowtimes.filter((st) => {
                if (!selectedTheaterId) return true;
                return String(st.theaterId) === String(selectedTheaterId);
            });

            const uniqueDates = Array.from(new Set(filteredByTheater.map((st) => toDateKey(st.startTime)))).sort();
            if (!uniqueDates.length) {
                dateSelect.innerHTML = initialDatesHtml;
                showtimeSelect.innerHTML = '<option value="">Không có suất phù hợp</option>';
                showtimeSelect.disabled = true;
                updateSubmitMode();
                return;
            }

            let dHtml = '';
            uniqueDates.forEach((dk, idx) => {
                const lbl = formatDateChipLabel(dk);
                dHtml += `<option value="${dk}" ${idx === 0 ? 'selected' : ''}>${escapeHtml(lbl.full)}</option>`;
            });
            dateSelect.innerHTML = dHtml;
            refreshTimesOnly();
        }

        function refreshTimesOnly() {
            const selectedTheaterId = theaterSelect.value;
            const selectedDate = dateSelect.value;

            const matching = currentShowtimes
                .filter((st) => {
                    const okTheater = !selectedTheaterId || String(st.theaterId) === String(selectedTheaterId);
                    const okDate = !selectedDate || toDateKey(st.startTime) === selectedDate;
                    return okTheater && okDate;
                })
                .sort((a, b) => new Date(a.startTime) - new Date(b.startTime));

            if (!matching.length) {
                showtimeSelect.innerHTML = '<option value="">Không có suất trong ngày này</option>';
                showtimeSelect.disabled = true;
                updateSubmitMode();
                return;
            }

            let sHtml = '<option value="">-- Chọn giờ chiếu (' + matching.length + ' suất) --</option>';
            matching.forEach((st) => {
                const time = formatTimeHHMM(st.startTime);
                const price = formatPriceVnd(st.price);
                const disabledAttr = st.availableSeats <= 0 ? 'disabled' : '';
                const seatInfo = st.availableSeats > 0 ? `Còn ${st.availableSeats} ghế` : 'Hết ghế';
                const prefixTheater = !selectedTheaterId ? `${st.theaterName} • ` : '';
                sHtml += `<option value="${st.showtimeId}" ${disabledAttr}>${time} — ${escapeHtml(prefixTheater + st.roomName)} • ${price} (${seatInfo})</option>`;
            });

            showtimeSelect.innerHTML = sHtml;
            showtimeSelect.disabled = false;

            // Auto-select first available showtime if only 1-2 options for super fast UX
            const firstBookable = matching.find((m) => m.availableSeats > 0);
            if (firstBookable && matching.length === 1) {
                showtimeSelect.value = String(firstBookable.showtimeId);
            }
            updateSubmitMode();
        }

        function updateSubmitMode() {
            const chosenShowtimeId = showtimeSelect.value;
            if (chosenShowtimeId) {
                submitBtn.classList.add('is-direct');
                if (submitText) submitText.textContent = 'Chọn Ghế Ngay';
            } else {
                submitBtn.classList.remove('is-direct');
                if (submitText) submitText.textContent = 'Tìm Suất Chiếu';
            }
        }

        form.addEventListener('submit', function (e) {
            const chosenShowtimeId = showtimeSelect.value;
            if (chosenShowtimeId) {
                e.preventDefault();
                window.location.href = `/Booking/SelectSeat/${encodeURIComponent(chosenShowtimeId)}`;
            }
        });
    }

    /* ----------------------------------------------------------------------
       3. Interactive Movie Catalog (Tab Switcher + Genre Pills + Search)
       ---------------------------------------------------------------------- */
    function initMovieCatalogFilters() {
        const tabs = Array.from(document.querySelectorAll('.js-movie-tab'));
        const genrePills = Array.from(document.querySelectorAll('.js-genre-pill'));
        const searchInput = document.getElementById('lpMovieSearch');
        const items = Array.from(document.querySelectorAll('.js-movie-item'));
        const emptyBox = document.getElementById('lpMovieEmptyState');
        const resetBtn = document.getElementById('lpResetMovieFilter');

        if (!items.length) return;

        let activeTab = 'now';
        let activeGenre = 'all';
        let query = '';

        function applyFilters() {
            let visibleCount = 0;

            items.forEach((el) => {
                const tabGroup = el.getAttribute('data-tab-group') || '';
                const genres = (el.getAttribute('data-genres') || '').toLowerCase();
                const title = (el.getAttribute('data-title') || '').toLowerCase();

                const matchTab = tabGroup === activeTab;
                const matchGenre = activeGenre === 'all' || genres.split('|').includes(activeGenre.toLowerCase());
                const matchQuery = !query || title.includes(query);

                const show = matchTab && matchGenre && matchQuery;
                el.classList.toggle('d-none', !show);
                if (show) visibleCount++;
            });

            if (emptyBox) {
                emptyBox.classList.toggle('d-none', visibleCount > 0);
            }
        }

        tabs.forEach((btn) => {
            btn.addEventListener('click', function () {
                activeTab = btn.getAttribute('data-tab') || 'now';
                tabs.forEach((t) => {
                    const isAct = t === btn;
                    t.classList.toggle('active', isAct);
                    t.setAttribute('aria-selected', isAct ? 'true' : 'false');
                });
                applyFilters();
            });
        });

        genrePills.forEach((pill) => {
            pill.addEventListener('click', function () {
                activeGenre = pill.getAttribute('data-genre') || 'all';
                genrePills.forEach((p) => p.classList.toggle('active', p === pill));
                applyFilters();
            });
        });

        if (searchInput) {
            searchInput.addEventListener('input', function () {
                query = searchInput.value.trim().toLowerCase();
                applyFilters();
            });
        }

        if (resetBtn) {
            resetBtn.addEventListener('click', function () {
                activeGenre = 'all';
                query = '';
                if (searchInput) searchInput.value = '';
                genrePills.forEach((p, idx) => p.classList.toggle('active', idx === 0));
                applyFilters();
            });
        }

        applyFilters();
    }

    /* ----------------------------------------------------------------------
       4. Mini Interactive 3D Seat Map Demo inside Bento Card
       ---------------------------------------------------------------------- */
    function initMiniSeatPreview() {
        const seats = Array.from(document.querySelectorAll('.js-demo-seat'));
        const summaryEl = document.getElementById('lpDemoSeatSummary');
        if (!seats.length || !summaryEl) return;

        function updateDemoSummary() {
            const selected = seats.filter((s) => s.classList.contains('is-selected'));
            if (!selected.length) {
                summaryEl.textContent = 'Chạm thử vào ghế trống để xem giá vé trực quan';
                return;
            }
            const names = selected.map((s) => s.getAttribute('data-seat'));
            const total = selected.reduce((sum, s) => sum + Number(s.getAttribute('data-price') || 90000), 0);
            summaryEl.innerHTML = `Ghế chọn: <strong class="text-white">${escapeHtml(names.join(', '))}</strong> • Tạm tính: <strong style="color: var(--lp-gold);">${formatPriceVnd(total)}</strong>`;
        }

        seats.forEach((seat) => {
            if (seat.classList.contains('is-held')) return;
            seat.addEventListener('click', function () {
                seat.classList.toggle('is-selected');
                seat.setAttribute('aria-pressed', seat.classList.contains('is-selected') ? 'true' : 'false');
                updateDemoSummary();
            });
        });

        updateDemoSummary();
    }

    /* ----------------------------------------------------------------------
       5. CinemaHub AI Assistant Integration (Opens #chatbot-panel)
       ---------------------------------------------------------------------- */
    function initChatbotPromptChips() {
        const chips = document.querySelectorAll('.js-chatbot-prompt');
        const openBtn = document.getElementById('lpOpenChatbotBtn');

        function openChatWithPrompt(promptText) {
            const panel = document.getElementById('chatbot-panel');
            const trigger = document.getElementById('chatbot-trigger');
            const input = document.getElementById('chatbot-input');
            const sendBtn = document.getElementById('chatbot-send-btn');

            if (panel && !panel.classList.contains('active') && trigger) {
                trigger.click();
            }
            if (promptText && input && sendBtn) {
                setTimeout(() => {
                    input.value = promptText;
                    sendBtn.click();
                }, 120);
            }
        }

        chips.forEach((chip) => {
            chip.addEventListener('click', function () {
                const prompt = chip.getAttribute('data-prompt') || '';
                openChatWithPrompt(prompt);
            });
        });

        if (openBtn) {
            openBtn.addEventListener('click', function () {
                openChatWithPrompt('');
            });
        }
    }

    /* ----------------------------------------------------------------------
       6. Unified Instant Showtimes Modal (Movie Mode & Theater Mode)
       ---------------------------------------------------------------------- */
    function initQuickShowtimeModal() {
        const modalEl = document.getElementById('lpQuickShowtimesModal');
        if (!modalEl || typeof bootstrap === 'undefined') return;

        const modalTitle = document.getElementById('lpModalTitle');
        const modalSub = document.getElementById('lpModalSub');
        const modalDates = document.getElementById('lpModalDates');
        const modalBody = document.getElementById('lpModalBody');
        const modalFullLink = document.getElementById('lpModalFullLink');

        let activeShowtimes = [];
        let activeDateKey = '';
        let currentMode = 'movie'; // 'movie' | 'theater'

        document.addEventListener('click', async function (e) {
            const movieTrigger = e.target.closest('.js-quick-book-movie');
            if (movieTrigger) {
                e.preventDefault();
                const movieId = movieTrigger.getAttribute('data-movie-id');
                const movieTitle = movieTrigger.getAttribute('data-movie-title') || 'Lịch chiếu phim';
                openMovieModal(movieId, movieTitle);
                return;
            }

            const theaterTrigger = e.target.closest('.js-quick-view-theater');
            if (theaterTrigger) {
                e.preventDefault();
                const theaterId = theaterTrigger.getAttribute('data-theater-id');
                const theaterName = theaterTrigger.getAttribute('data-theater-name') || 'Lịch chiếu rạp';
                const theaterAddr = theaterTrigger.getAttribute('data-theater-address') || '';
                openTheaterModal(theaterId, theaterName, theaterAddr);
            }
        });

        async function openMovieModal(movieId, title) {
            currentMode = 'movie';
            modalTitle.textContent = title;
            modalSub.innerHTML = '<i class="bi bi-lightning-charge-fill text-warning me-1" aria-hidden="true"></i>Chọn giờ chiếu để vào thẳng sơ đồ ghế 3D';
            if (modalFullLink) modalFullLink.href = `/Showtimes?movieId=${encodeURIComponent(movieId)}`;
            showLoading();
            bootstrap.Modal.getOrCreateInstance(modalEl).show();

            try {
                activeShowtimes = await fetchMovieShowtimes(movieId);
                buildDateStripAndRender();
            } catch (err) {
                renderErrorState();
            }
        }

        async function openTheaterModal(theaterId, name, address) {
            currentMode = 'theater';
            modalTitle.textContent = name;
            modalSub.innerHTML = `<i class="bi bi-geo-alt-fill text-danger me-1" aria-hidden="true"></i>${escapeHtml(address || 'Hệ thống rạp CinemaHub')}`;
            if (modalFullLink) modalFullLink.href = `/Showtimes?theaterId=${encodeURIComponent(theaterId)}`;
            showLoading();
            bootstrap.Modal.getOrCreateInstance(modalEl).show();

            try {
                activeShowtimes = await fetchTheaterShowtimes(theaterId);
                buildDateStripAndRender();
            } catch (err) {
                renderErrorState();
            }
        }

        function showLoading() {
            modalDates.innerHTML = '';
            modalBody.innerHTML = `
                <div class="text-center py-5">
                    <div class="spinner-border text-danger mb-3" role="status"></div>
                    <p class="text-muted small mb-0">Đang tải suất chiếu thời gian thực...</p>
                </div>`;
        }

        function renderErrorState() {
            modalBody.innerHTML = `
                <div class="text-center py-5">
                    <i class="bi bi-exclamation-circle text-danger fs-1" aria-hidden="true"></i>
                    <p class="text-light fw-semibold mt-2 mb-1">Không thể tải dữ liệu lịch chiếu</p>
                    <p class="text-muted small mb-0">Vui lòng thử lại hoặc mở trang Lịch chiếu đầy đủ.</p>
                </div>`;
        }

        function buildDateStripAndRender() {
            if (!activeShowtimes.length) {
                modalDates.innerHTML = '';
                modalBody.innerHTML = `
                    <div class="text-center py-5">
                        <i class="bi bi-calendar2-x text-secondary fs-1" aria-hidden="true"></i>
                        <h6 class="text-white fw-bold mt-3 mb-1">Hiện chưa có suất chiếu sắp tới</h6>
                        <p class="text-muted small mb-0">Các suất chiếu mới đang được cập nhật trên hệ thống.</p>
                    </div>`;
                return;
            }

            const dateKeys = Array.from(new Set(activeShowtimes.map((s) => toDateKey(s.startTime)))).sort();
            activeDateKey = dateKeys[0];

            modalDates.innerHTML = dateKeys.map((dk, idx) => {
                const lbl = formatDateChipLabel(dk);
                return `
                    <button type="button" class="lp-date-pill ${idx === 0 ? 'active' : ''}" data-date="${dk}">
                        <div class="small fw-semibold" style="font-size:0.7rem;">${escapeHtml(lbl.top)}</div>
                        <div class="fw-bold" style="font-size:0.95rem;">${escapeHtml(lbl.ddmm)}</div>
                    </button>`;
            }).join('');

            modalDates.querySelectorAll('.lp-date-pill').forEach((btn) => {
                btn.addEventListener('click', function () {
                    modalDates.querySelectorAll('.lp-date-pill').forEach((b) => b.classList.remove('active'));
                    btn.classList.add('active');
                    activeDateKey = btn.getAttribute('data-date') || '';
                    renderSlotsForActiveDate();
                });
            });

            renderSlotsForActiveDate();
        }

        function renderSlotsForActiveDate() {
            const list = activeShowtimes
                .filter((s) => toDateKey(s.startTime) === activeDateKey)
                .sort((a, b) => new Date(a.startTime) - new Date(b.startTime));

            if (!list.length) {
                modalBody.innerHTML = '<p class="text-center text-muted py-4 mb-0">Không có suất chiếu trong ngày đã chọn.</p>';
                return;
            }

            // Group by Theater (in movie mode) or by Movie (in theater mode)
            const groups = new Map();
            list.forEach((st) => {
                const gKey = currentMode === 'movie' ? st.theaterName : String(st.movieId);
                if (!groups.has(gKey)) {
                    groups.set(gKey, {
                        heading: currentMode === 'movie' ? st.theaterName : st.movieTitle,
                        poster: st.moviePosterUrl,
                        duration: st.movieDuration,
                        items: []
                    });
                }
                groups.get(gKey).items.push(st);
            });

            let html = '';
            groups.forEach((g) => {
                const slotsHtml = g.items.map((st) => {
                    const time = formatTimeHHMM(st.startTime);
                    const price = formatPriceVnd(st.price);
                    if (st.availableSeats > 0) {
                        return `
                            <a href="/Booking/SelectSeat/${encodeURIComponent(st.showtimeId)}" class="lp-time-slot" title="Còn ${st.availableSeats} ghế trống">
                                <span class="fw-bold fs-6">${time}</span>
                                <span style="font-size:0.7rem;color:#cbd5e1;">${escapeHtml(st.roomName)}</span>
                                <span style="font-size:0.7rem;color:var(--lp-gold);font-weight:700;">${price}</span>
                                <span style="font-size:0.65rem;color:#34d399;">Còn ${st.availableSeats} ghế</span>
                            </a>`;
                    }
                    return `
                        <div class="lp-time-slot is-full">
                            <span class="fw-bold fs-6">${time}</span>
                            <span style="font-size:0.7rem;">${escapeHtml(st.roomName)}</span>
                            <span style="font-size:0.68rem;">Hết ghế</span>
                        </div>`;
                }).join('');

                html += `
                    <div class="p-3 mb-3 rounded-4" style="background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.07);">
                        <div class="d-flex align-items-center justify-content-between flex-wrap gap-2 mb-3">
                            <div class="fw-bold text-white">
                                <i class="bi ${currentMode === 'movie' ? 'bi-building text-danger' : 'bi-film text-danger'} me-2" aria-hidden="true"></i>${escapeHtml(g.heading)}
                            </div>
                            ${g.duration ? `<span class="small text-muted"><i class="bi bi-clock me-1" aria-hidden="true"></i>${g.duration} phút</span>` : ''}
                        </div>
                        <div class="d-flex flex-wrap gap-2">
                            ${slotsHtml}
                        </div>
                    </div>`;
            });

            modalBody.innerHTML = html;
        }
    }

    /* ----------------------------------------------------------------------
       7. Scroll Reveal Observer (Respects prefers-reduced-motion)
       ---------------------------------------------------------------------- */
    function initScrollReveal() {
        const targets = document.querySelectorAll('.lp-reveal');
        if (!targets.length) return;

        if (prefersReducedMotion || !('IntersectionObserver' in window)) {
            targets.forEach((el) => el.classList.add('is-visible'));
            return;
        }

        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.08, rootMargin: '0px 0px -30px 0px' });

        targets.forEach((el) => observer.observe(el));
    }
})();

