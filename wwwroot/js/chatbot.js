/* ==========================================================================
   CinemaHub ChatBot Widget JS - Premium E-Commerce Interactivity
   ========================================================================== */

document.addEventListener("DOMContentLoaded", function () {
    const triggerBtn = document.getElementById("chatbot-trigger");
    const panel = document.getElementById("chatbot-panel");
    const closeBtn = document.getElementById("chatbot-close-btn");
    const clearBtn = document.getElementById("chatbot-clear-btn");
    const sendBtn = document.getElementById("chatbot-send-btn");
    const chatInput = document.getElementById("chatbot-input");
    const chatBody = document.getElementById("chatbot-body");

    let chatHistory = [];

    // Khởi tạo Chatbot
    initChatbot();

    // 1. Quản lý Tooltip chào mừng tự động hiển thị sau 2.5 giây
    createAndShowTooltip();

    // Sự kiện mở/đóng khung chat
    triggerBtn.addEventListener("click", toggleChatPanel);
    closeBtn.addEventListener("click", toggleChatPanel);

    // Sự kiện xóa lịch sử chat
    if (clearBtn) {
        clearBtn.addEventListener("click", function () {
            clearChatHistory();
        });
    }

    // Sự kiện gửi tin nhắn
    sendBtn.addEventListener("click", handleSend);
    chatInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            handleSend();
        }
    });

    // Sự kiện click các liên kết hành động trong tin nhắn (Chat Action Links)
    chatBody.addEventListener("click", function (e) {
        const actionLink = e.target.closest(".chat-action-link");
        if (actionLink) {
            e.preventDefault();
            const queryText = actionLink.getAttribute("data-query");
            if (queryText) {
                sendUserMessage(queryText);
            }
        }
    });

    // Khởi động chatbot, load lịch sử hoặc hiển thị tin chào mừng
    function initChatbot() {
        const savedHistory = sessionStorage.getItem("cinema_chat_history");
        if (savedHistory) {
            try {
                chatHistory = JSON.parse(savedHistory);
                renderAllMessages();
            } catch (e) {
                console.error("Lỗi parse lịch sử chat:", e);
                chatHistory = [];
                showWelcomeMessage();
            }
        } else {
            showWelcomeMessage();
        }
    }

    // Hiển thị tin chào mừng mặc định
    function showWelcomeMessage() {
        const welcomeText = "Xin chào! Tôi là **CinemaHub Assistant** - trợ lý ảo trực tuyến của bạn.\n\nTôi có thể giúp bạn nhanh các thông tin sau:\n- [Xem các phim đang chiếu tại rạp](query:phim đang chiếu)\n- [Xem lịch chiếu hôm nay](query:lịch chiếu hôm nay)\n- [Xem giá vé & liên hệ hotline](query:giá vé)\n- [Xem hướng dẫn đặt vé online](query:đặt vé)\n\nBạn cần mình hỗ trợ thông tin gì thế?";
        addMessageToChat("bot", welcomeText, false);
    }

    // Tạo và quản lý Tooltip
    function createAndShowTooltip() {
        // Tạo tooltip element
        const tooltip = document.createElement("div");
        tooltip.className = "chatbot-tooltip";
        tooltip.innerHTML = "Chat với CinemaHub (Hỗ trợ 24/7)";
        document.body.appendChild(tooltip);

        // Hiển thị sau 2.5s nếu chưa mở chatbot lần nào
        const tooltipTimeout = setTimeout(() => {
            if (!panel.classList.contains("active") && !sessionStorage.getItem("cinema_chatbot_opened")) {
                tooltip.classList.add("show");
            }
        }, 2500);

        // Tự động ẩn tooltip sau 8s tiếp theo
        setTimeout(() => {
            tooltip.classList.remove("show");
            setTimeout(() => tooltip.remove(), 400);
        }, 10500);

        // Lưu vết click mở panel để xóa tooltip vĩnh viễn
        triggerBtn.addEventListener("click", function() {
            sessionStorage.setItem("cinema_chatbot_opened", "true");
            tooltip.classList.remove("show");
            tooltip.remove();
            clearTimeout(tooltipTimeout);
        }, { once: true });
    }

    // Toggle đóng mở khung chat
    function toggleChatPanel() {
        panel.classList.toggle("active");
        
        // Thay đổi chữ ở nút Trigger
        if (panel.classList.contains("active")) {
            triggerBtn.innerText = "Đóng";
            triggerBtn.classList.add("active");
            chatInput.focus();
            setTimeout(scrollToBottom, 100);
        } else {
            triggerBtn.innerText = "Hỗ trợ";
            triggerBtn.classList.remove("active");
        }
    }

    // Xóa lịch sử trò chuyện
    function clearChatHistory() {
        chatHistory = [];
        sessionStorage.removeItem("cinema_chat_history");
        chatBody.innerHTML = "";
        showWelcomeMessage();
    }

    // Xử lý gửi tin nhắn từ ô Input
    function handleSend() {
        const text = chatInput.value.trim();
        if (text === "") return;
        
        chatInput.value = "";
        sendUserMessage(text);
    }

    // Gửi tin nhắn của User
    function sendUserMessage(text) {
        addMessageToChat("user", text, true);
        showTypingIndicator();

        fetch("/api/chatbot/message", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                message: text,
                history: chatHistory
            })
        })
        .then(response => {
            if (!response.ok) {
                throw new Error("Lỗi kết nối máy chủ");
            }
            return response.json();
        })
        .then(data => {
            hideTypingIndicator();
            addMessageToChat("bot", data.response, true, data.movies, data.showtimes, data.userBookings);
        })
        .catch(error => {
            console.error("Lỗi gửi tin nhắn:", error);
            hideTypingIndicator();
            addMessageToChat("bot", "Đã xảy ra lỗi kết nối mạng. Bạn vui lòng thử lại sau giây lát nhé!", true);
        });
    }

    // Thêm tin nhắn vào khung chat (Có cấu trúc Avatar cho Bot)
    function addMessageToChat(sender, text, shouldSave = true, movies = null, showtimes = null, userBookings = null) {
        const timeString = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        
        if (shouldSave) {
            chatHistory.push({ 
                sender: sender, 
                text: text,
                movies: movies,
                showtimes: showtimes,
                userBookings: userBookings
            });
            if (chatHistory.length > 20) chatHistory.shift();
            sessionStorage.setItem("cinema_chat_history", JSON.stringify(chatHistory));
        }

        // Tạo phần tử tin nhắn cha
        const messageDiv = document.createElement("div");
        messageDiv.className = `chat-message ${sender}`;

        // Không sử dụng avatar để giao diện gọn gàng hơn

        // Tạo wrapper chứa bubble & time
        const wrapperDiv = document.createElement("div");
        wrapperDiv.className = "message-wrapper";

        const bubbleDiv = document.createElement("div");
        bubbleDiv.className = "message-bubble";
        bubbleDiv.innerHTML = formatMarkdown(text);
        wrapperDiv.appendChild(bubbleDiv);

        // 1. Render movies (Carousel)
        if (movies && movies.length > 0) {
            const carouselContainer = document.createElement("div");
            carouselContainer.className = "chatbot-carousel-container";
            
            const carousel = document.createElement("div");
            carousel.className = "chatbot-carousel";
            
            movies.forEach(movie => {
                const card = document.createElement("div");
                card.className = "chatbot-movie-card";
                
                const posterUrl = movie.posterUrl || "/img/placeholder.jpg";
                const genresText = movie.genres && movie.genres.length > 0 ? movie.genres.join(", ") : "Chưa phân loại";
                
                card.innerHTML = `
                    <img class="chatbot-movie-poster" src="${posterUrl}" alt="${movie.title}" onerror="this.src='/img/placeholder.jpg'" />
                    <div class="chatbot-movie-info">
                        <div class="chatbot-movie-title" title="${movie.title}">${movie.title}</div>
                        <div class="chatbot-movie-meta">${movie.duration}m | ${genresText}</div>
                        <a href="/Movies/Details/${movie.movieId}" class="chatbot-movie-btn">Đặt vé</a>
                    </div>
                `;
                carousel.appendChild(card);
            });
            
            carouselContainer.appendChild(carousel);
            wrapperDiv.appendChild(carouselContainer);
        }

        // 2. Render showtimes grouped by movie
        if (showtimes && showtimes.length > 0) {
            const showtimesContainer = document.createElement("div");
            showtimesContainer.className = "chatbot-showtimes-container";
            
            // Group showtimes by movie title and then by theater name
            const groups = {};
            showtimes.forEach(st => {
                if (!groups[st.movieTitle]) {
                    groups[st.movieTitle] = {};
                }
                const theater = st.theaterName || "CinemaHub";
                if (!groups[st.movieTitle][theater]) {
                    groups[st.movieTitle][theater] = [];
                }
                groups[st.movieTitle][theater].push(st);
            });
            
            Object.keys(groups).forEach(movieTitle => {
                const groupDiv = document.createElement("div");
                groupDiv.className = "chatbot-showtime-group";
                
                const titleDiv = document.createElement("div");
                titleDiv.className = "chatbot-showtime-movie-title";
                titleDiv.innerText = movieTitle;
                groupDiv.appendChild(titleDiv);
                
                const theatersObj = groups[movieTitle];
                Object.keys(theatersObj).forEach(theaterName => {
                    const theaterDiv = document.createElement("div");
                    theaterDiv.className = "chatbot-showtime-theater-group";
                    
                    const theaterHeader = document.createElement("div");
                    theaterHeader.className = "chatbot-showtime-theater-name";
                    theaterHeader.innerText = theaterName;
                    theaterDiv.appendChild(theaterHeader);
                    
                    const pillsDiv = document.createElement("div");
                    pillsDiv.className = "chatbot-showtime-pills";
                    
                    theatersObj[theaterName].forEach(st => {
                        const pill = document.createElement("a");
                        pill.className = "chatbot-showtime-pill";
                        pill.href = `/Booking/SelectSeat/${st.showtimeId}`;
                        pill.innerHTML = `
                            <span class="chatbot-showtime-time">${st.startTime}</span>
                            <span class="chatbot-showtime-room">${st.roomName}</span>
                            <span class="chatbot-showtime-price">${st.price.toLocaleString('vi-VN')}đ</span>
                        `;
                        pillsDiv.appendChild(pill);
                    });
                    
                    theaterDiv.appendChild(pillsDiv);
                    groupDiv.appendChild(theaterDiv);
                });
                
                showtimesContainer.appendChild(groupDiv);
            });
            
            wrapperDiv.appendChild(showtimesContainer);
        }

        // 3. Render userBookings (Tickets)
        if (userBookings && userBookings.length > 0) {
            const ticketsContainer = document.createElement("div");
            ticketsContainer.className = "chatbot-tickets-container";
            
            userBookings.forEach(booking => {
                const statusClass = (booking.status || "").toLowerCase() === "paid" ? "paid" : "unpaid";
                const ticketCard = document.createElement("div");
                ticketCard.className = "chatbot-ticket-card";
                
                ticketCard.innerHTML = `
                    <div class="chatbot-ticket-header">
                        <span class="chatbot-ticket-id">#${booking.ticketId}</span>
                        <span class="chatbot-ticket-status ${statusClass}">${booking.status === "Paid" ? "Đã thanh toán" : booking.status}</span>
                    </div>
                    <div class="chatbot-ticket-body">
                        <div class="chatbot-ticket-title">${booking.movieTitle}</div>
                        <div class="chatbot-ticket-row">
                            <div class="chatbot-ticket-col">
                                <span class="chatbot-ticket-label">Rạp</span>
                                <span class="chatbot-ticket-value">${booking.theaterName || "CinemaHub"}</span>
                            </div>
                            <div class="chatbot-ticket-col">
                                <span class="chatbot-ticket-label">Phòng</span>
                                <span class="chatbot-ticket-value">${booking.roomName}</span>
                            </div>
                        </div>
                        <div class="chatbot-ticket-row">
                            <div class="chatbot-ticket-col">
                                <span class="chatbot-ticket-label">Suất chiếu</span>
                                <span class="chatbot-ticket-value">${booking.startTime}</span>
                            </div>
                            <div class="chatbot-ticket-col">
                                <span class="chatbot-ticket-label">Ghế</span>
                                <span class="chatbot-ticket-value">${booking.seatName}</span>
                            </div>
                        </div>
                    </div>
                    <div class="chatbot-ticket-divider"></div>
                    <div class="chatbot-ticket-footer">
                        Đặt lúc: ${booking.bookingTime}
                    </div>
                `;
                ticketsContainer.appendChild(ticketCard);
            });
            
            wrapperDiv.appendChild(ticketsContainer);
        }

        const timeDiv = document.createElement("div");
        timeDiv.className = "message-time";
        timeDiv.innerText = timeString;
        wrapperDiv.appendChild(timeDiv);

        messageDiv.appendChild(wrapperDiv);

        chatBody.appendChild(messageDiv);
        scrollToBottom();
    }

    // Render lại toàn bộ tin nhắn từ lịch sử
    function renderAllMessages() {
        chatBody.innerHTML = "";
        chatHistory.forEach(msg => {
            addMessageToChat(msg.sender, msg.text, false, msg.movies, msg.showtimes, msg.userBookings);
        });
    }

    // Hiện biểu tượng ba chấm đang gõ
    function showTypingIndicator() {
        const indicatorDiv = document.createElement("div");
        indicatorDiv.className = "chat-message bot typing-indicator-container";
        indicatorDiv.id = "chatbot-typing-indicator";

        // Không sử dụng avatar để giao diện gọn gàng hơn

        const wrapperDiv = document.createElement("div");
        wrapperDiv.className = "message-wrapper";

        const bubbleDiv = document.createElement("div");
        bubbleDiv.className = "message-bubble";
        
        const typingDiv = document.createElement("div");
        typingDiv.className = "typing-indicator";
        typingDiv.innerHTML = `
            <div class="typing-dot"></div>
            <div class="typing-dot"></div>
            <div class="typing-dot"></div>
        `;

        bubbleDiv.appendChild(typingDiv);
        wrapperDiv.appendChild(bubbleDiv);
        indicatorDiv.appendChild(wrapperDiv);
        chatBody.appendChild(indicatorDiv);
        
        scrollToBottom();
    }

    // Ẩn biểu tượng ba chấm đang gõ
    function hideTypingIndicator() {
        const indicator = document.getElementById("chatbot-typing-indicator");
        if (indicator) {
            indicator.remove();
        }
    }

    // Tự động cuộn xuống cuối khung chat
    function scrollToBottom() {
        chatBody.scrollTop = chatBody.scrollHeight;
    }

    // Hàm chuyển đổi định dạng Markdown đơn giản sang HTML
    function formatMarkdown(text) {
        if (!text) return "";
        
        let escaped = text
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");

        // 1. Chữ đậm: **text** -> <strong>text</strong>
        escaped = escaped.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>");

        // 2. Chữ nghiêng: *text* -> <em>text</em>
        escaped = escaped.replace(/\*(.*?)\*/g, "<em>$1</em>");

        // 3. Liên kết hành động tự định nghĩa: [text](query:từ_khóa) -> <a href="#" class="chat-action-link" data-query="từ_khóa">text</a>
        escaped = escaped.replace(/\[(.*?)\]\(query:(.*?)\)/g, '<a href="#" class="chat-action-link" data-query="$2">$1</a>');

        // 4. Đường dẫn/Liên kết thông thường: [text](url) -> <a href="url" target="_blank">text</a>
        escaped = escaped.replace(/\[(.*?)\]\((.*?)\)/g, '<a href="$2" target="_blank">$1</a>');

        // Phục hồi ký tự HTML cho các thẻ a và strong đã format
        escaped = escaped
            .replace(/&lt;strong&gt;/g, "<strong>").replace(/&lt;\/strong&gt;/g, "</strong>")
            .replace(/&lt;em&gt;/g, "<em>").replace(/&lt;\/em&gt;/g, "</em>")
            .replace(/&lt;a href=(.*?)&gt;/g, '<a href=$1>').replace(/&lt;\/a&gt;/g, "</a>");

        // 4. Danh sách gạch đầu dòng và số thứ tự
        const lines = escaped.split("\n");
        let formattedText = "";
        let inList = false;
        let listType = ""; // "ul" hoặc "ol"

        for (let i = 0; i < lines.length; i++) {
            let line = lines[i].trim();
            
            if (line.startsWith("- ") || line.startsWith("* ")) {
                if (!inList || listType !== "ul") {
                    if (inList) formattedText += `</${listType}>`;
                    formattedText += "<ul>";
                    inList = true;
                    listType = "ul";
                }
                const content = line.substring(2);
                formattedText += `<li>${content}</li>`;
            } 
            else if (/^\d+\.\s/.test(line)) {
                if (!inList || listType !== "ol") {
                    if (inList) formattedText += `</${listType}>`;
                    formattedText += "<ol>";
                    inList = true;
                    listType = "ol";
                }
                const content = line.replace(/^\d+\.\s/, "");
                formattedText += `<li>${content}</li>`;
            } 
            else {
                if (inList) {
                    formattedText += `</${listType}>`;
                    inList = false;
                    listType = "";
                }
                if (line === "") {
                    formattedText += "<p></p>";
                } else {
                    formattedText += `<p>${lines[i]}</p>`;
                }
            }
        }

        if (inList) {
            formattedText += `</${listType}>`;
        }

        return formattedText;
    }
});
