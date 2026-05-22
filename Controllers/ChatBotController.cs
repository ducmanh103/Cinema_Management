using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace CinemaManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatBotController : ControllerBase
    {
        private readonly CinemaDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ChatBotController(CinemaDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        // DTOs cho request/response của frontend
        public class ChatRequest
        {
            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("history")]
            public List<ChatMessageDto> History { get; set; } = new();
        }

        public class ChatMessageDto
        {
            [JsonPropertyName("sender")]
            public string Sender { get; set; } = string.Empty; // "user" hoặc "bot"

            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }

        public class ChatResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;

            [JsonPropertyName("mode")]
            public string Mode { get; set; } = string.Empty;

            [JsonPropertyName("movies")]
            public List<MovieAttachmentDto>? Movies { get; set; }

            [JsonPropertyName("showtimes")]
            public List<ShowtimeAttachmentDto>? Showtimes { get; set; }

            [JsonPropertyName("userBookings")]
            public List<BookingAttachmentDto>? UserBookings { get; set; }
        }

        public class MovieAttachmentDto
        {
            [JsonPropertyName("movieId")]
            public int MovieId { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("duration")]
            public int Duration { get; set; }

            [JsonPropertyName("posterUrl")]
            public string? PosterUrl { get; set; }

            [JsonPropertyName("genres")]
            public List<string> Genres { get; set; } = new();

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
        }

        public class ShowtimeAttachmentDto
        {
            [JsonPropertyName("showtimeId")]
            public int ShowtimeId { get; set; }

            [JsonPropertyName("movieId")]
            public int MovieId { get; set; }

            [JsonPropertyName("movieTitle")]
            public string MovieTitle { get; set; } = string.Empty;

            [JsonPropertyName("startTime")]
            public string StartTime { get; set; } = string.Empty;

            [JsonPropertyName("roomName")]
            public string RoomName { get; set; } = string.Empty;

            [JsonPropertyName("price")]
            public decimal Price { get; set; }
        }

        public class BookingAttachmentDto
        {
            [JsonPropertyName("ticketId")]
            public int TicketId { get; set; }

            [JsonPropertyName("movieTitle")]
            public string MovieTitle { get; set; } = string.Empty;

            [JsonPropertyName("startTime")]
            public string StartTime { get; set; } = string.Empty;

            [JsonPropertyName("roomName")]
            public string RoomName { get; set; } = string.Empty;

            [JsonPropertyName("seatName")]
            public string SeatName { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("bookingTime")]
            public string BookingTime { get; set; } = string.Empty;
        }

        [HttpPost("message")]
        public async Task<IActionResult> ProcessMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { response = "Tin nhắn không được để trống." });
            }

            var apiKey = _configuration["Gemini:ApiKey"];

            try
            {
                // Lấy thông tin thực tế từ database để làm ngữ cảnh
                var contextData = await GetCinemaContextAsync();
                
                // Tra cứu thông tin người dùng đang đăng nhập
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string userFullName = "Khách";
                string userTicketsContext = "Không có thông tin vé đã mua.";
                bool isLoggedIn = false;
                int userId = 0;

                if (userIdClaim != null && int.TryParse(userIdClaim, out userId))
                {
                    isLoggedIn = true;
                    userFullName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Khách";
                    
                    var tickets = await _dbContext.Tickets
                        .Include(t => t.Showtime).ThenInclude(s => s.Movie)
                        .Include(t => t.Showtime).ThenInclude(s => s.Room)
                        .Include(t => t.Seat)
                        .Where(t => t.UserId == userId)
                        .OrderByDescending(t => t.BookingTime)
                        .Take(5)
                        .ToListAsync();
                        
                    if (tickets.Any())
                    {
                        var ticketsSb = new StringBuilder();
                        ticketsSb.AppendLine($"Người dùng này đã đăng nhập tên là {userFullName}. Dưới đây là danh sách vé họ đã đặt:");
                        foreach (var t in tickets)
                        {
                            ticketsSb.AppendLine($"- Vé mã #{t.TicketId}: Phim: {t.Showtime.Movie.Title} | Phòng: {t.Showtime.Room.RoomName} | Suất chiếu: {t.Showtime.StartTime:dd/MM/yyyy HH:mm} | Ghế: {t.Seat.SeatNumber} | Trạng thái: {t.Status}");
                        }
                        userTicketsContext = ticketsSb.ToString();
                    }
                    else
                    {
                        userTicketsContext = $"Người dùng này đã đăng nhập tên là {userFullName} nhưng chưa đặt bất kỳ vé nào.";
                    }
                }

                // Phân loại ý định của câu hỏi để đính kèm dữ liệu (Rich Attachments) cho frontend
                var normalizedMessage = RemoveDiacritics(request.Message.ToLower());
                List<MovieAttachmentDto>? movies = null;
                List<ShowtimeAttachmentDto>? showtimes = null;
                List<BookingAttachmentDto>? userBookings = null;

                if (normalizedMessage.Contains("phim dang chieu") || normalizedMessage.Contains("phim hot") || normalizedMessage.Contains("dang chieu") || normalizedMessage.Contains("phim moi"))
                {
                    movies = await _dbContext.Movies
                        .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                        .Where(m => m.Status == "Now Showing")
                        .Select(m => new MovieAttachmentDto {
                            MovieId = m.MovieId,
                            Title = m.Title,
                            Duration = m.Duration,
                            PosterUrl = m.PosterUrl,
                            Genres = m.MovieGenres.Select(mg => mg.Genre.GenreName).ToList(),
                            Status = m.Status
                        }).ToListAsync();
                }
                else if (normalizedMessage.Contains("phim sap chieu") || normalizedMessage.Contains("sap chieu") || normalizedMessage.Contains("sap ra mat"))
                {
                    movies = await _dbContext.Movies
                        .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                        .Where(m => m.Status == "Coming Soon")
                        .Select(m => new MovieAttachmentDto {
                            MovieId = m.MovieId,
                            Title = m.Title,
                            Duration = m.Duration,
                            PosterUrl = m.PosterUrl,
                            Genres = m.MovieGenres.Select(mg => mg.Genre.GenreName).ToList(),
                            Status = m.Status
                        }).ToListAsync();
                }
                else if (normalizedMessage.Contains("lich chieu") || normalizedMessage.Contains("suat chieu") || normalizedMessage.Contains("gio chieu"))
                {
                    var today = DateTime.Today;
                    var tomorrow = today.AddDays(1);
                    showtimes = await _dbContext.Showtimes
                        .Include(s => s.Movie)
                        .Include(s => s.Room)
                        .Where(s => s.StartTime >= today && s.StartTime < tomorrow)
                        .OrderBy(s => s.StartTime)
                        .Select(s => new ShowtimeAttachmentDto {
                            ShowtimeId = s.ShowtimeId,
                            MovieId = s.MovieId,
                            MovieTitle = s.Movie.Title,
                            StartTime = s.StartTime.ToString("HH:mm"),
                            RoomName = s.Room.RoomName,
                            Price = s.Price
                        }).ToListAsync();
                }
                else if (normalizedMessage.Contains("ve cua toi") || normalizedMessage.Contains("ve da dat") || normalizedMessage.Contains("da dat") || normalizedMessage.Contains("lich su") || normalizedMessage.Contains("ve cua minh") || normalizedMessage.Contains("ve da mua") || normalizedMessage.Contains("lich su dat ve"))
                {
                    if (isLoggedIn)
                    {
                        userBookings = await _dbContext.Tickets
                            .Include(t => t.Showtime).ThenInclude(s => s.Movie)
                            .Include(t => t.Showtime).ThenInclude(s => s.Room)
                            .Include(t => t.Seat)
                            .Where(t => t.UserId == userId)
                            .OrderByDescending(t => t.BookingTime)
                            .Take(5)
                            .Select(t => new BookingAttachmentDto {
                                TicketId = t.TicketId,
                                MovieTitle = t.Showtime.Movie.Title,
                                StartTime = t.Showtime.StartTime.ToString("dd/MM/yyyy HH:mm"),
                                RoomName = t.Showtime.Room.RoomName,
                                SeatName = t.Seat.SeatNumber,
                                Status = t.Status,
                                BookingTime = t.BookingTime.ToString("dd/MM/yyyy HH:mm")
                            }).ToListAsync();
                    }
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Chế độ Online: Sử dụng Gemini API
                    var aiResponse = await CallGeminiApiAsync(request.Message, request.History, contextData, apiKey, userFullName, userTicketsContext, isLoggedIn);
                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        return Ok(new ChatResponse { 
                            Response = aiResponse, 
                            Mode = "online",
                            Movies = movies,
                            Showtimes = showtimes,
                            UserBookings = userBookings
                        });
                    }
                }

                // Chế độ Offline (Fallback): Xử lý bằng từ khóa & truy vấn Db cục bộ
                var fallbackResponse = ProcessOfflineQuery(request.Message, contextData, userFullName, isLoggedIn);
                return Ok(new ChatResponse { 
                    Response = fallbackResponse, 
                    Mode = "offline",
                    Movies = movies,
                    Showtimes = showtimes,
                    UserBookings = userBookings
                });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu cần và trả về fallback
                Console.WriteLine($"ChatBot Error: {ex.Message}");
                var contextData = await GetCinemaContextAsync();
                
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string userFullName = "Khách";
                bool isLoggedIn = userIdClaim != null;
                
                var fallbackResponse = ProcessOfflineQuery(request.Message, contextData, userFullName, isLoggedIn);
                return Ok(new ChatResponse { 
                    Response = "Đã xảy ra lỗi kết nối. Dưới đây là phản hồi offline của hệ thống: \n\n" + fallbackResponse, 
                    Mode = "offline-error",
                    Movies = null,
                    Showtimes = null,
                    UserBookings = null
                });
            }
        }

        private async Task<CinemaContextData> GetCinemaContextAsync()
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 1. Lấy phim đang chiếu
            var nowShowing = await _dbContext.Movies
                .Where(m => m.Status == "Now Showing")
                .Select(m => new MovieContextInfo { Title = m.Title, Duration = m.Duration, Description = m.Description })
                .ToListAsync();

            // 2. Lấy phim sắp chiếu
            var comingSoon = await _dbContext.Movies
                .Where(m => m.Status == "Coming Soon")
                .Select(m => new MovieContextInfo { Title = m.Title, Duration = m.Duration, Description = m.Description })
                .ToListAsync();

            // 3. Lấy suất chiếu hôm nay
            var todayShowtimes = await _dbContext.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .Where(s => s.StartTime >= today && s.StartTime < tomorrow)
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeContextInfo
                {
                    MovieTitle = s.Movie.Title,
                    RoomName = s.Room.RoomName,
                    StartTime = s.StartTime.ToString("HH:mm"),
                    Price = s.Price
                })
                .ToListAsync();

            return new CinemaContextData
            {
                CurrentTime = now.ToString("dd/MM/yyyy HH:mm"),
                NowShowing = nowShowing,
                ComingSoon = comingSoon,
                TodayShowtimes = todayShowtimes
            };
        }

        private async Task<string> CallGeminiApiAsync(string message, List<ChatMessageDto> history, CinemaContextData context, string apiKey, string userFullName, string userTicketsContext, bool isLoggedIn)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={apiKey}";

            // Xây dựng System Instruction chứa ngữ cảnh rạp phim và người dùng
            var systemInstruction = new StringBuilder();
            systemInstruction.AppendLine("Bạn là Trợ lý ảo thông minh CinemaHub Assistant hỗ trợ khách hàng đặt vé xem phim trực tuyến.");
            systemInstruction.AppendLine("Hãy trả lời thân thiện, nhiệt tình, lịch sự và ngắn gọn bằng Tiếng Việt.");
            systemInstruction.AppendLine("Luôn dùng markdown để định dạng văn bản (in đậm, danh sách gạch đầu dòng, xuống dòng) cho dễ đọc.");
            systemInstruction.AppendLine("TUYỆT ĐỐI KHÔNG sử dụng bất kỳ biểu tượng cảm xúc (emoji) hoặc biểu tượng đồ họa (icon) nào trong câu trả lời.");
            systemInstruction.AppendLine("Khi gợi ý các hành động tiếp theo cho người dùng, hãy luôn dùng định dạng [Tên hành động](query:từ khóa truy vấn) để người dùng có thể click trực tiếp vào dòng chat thay vì dùng các nút bấm bên ngoài. Ví dụ: '[Xem phim đang chiếu](query:phim đang chiếu)' hoặc '[Xem lịch chiếu hôm nay](query:lịch chiếu hôm nay)'.");
            systemInstruction.AppendLine($"Thời gian hiện tại của hệ thống: {context.CurrentTime}");
            
            if (isLoggedIn)
            {
                systemInstruction.AppendLine($"\nThông tin khách hàng đang chat: {userFullName}");
                systemInstruction.AppendLine($"{userTicketsContext}");
                systemInstruction.AppendLine("Hãy chào khách hàng bằng tên của họ một cách tự nhiên và hỗ trợ tra cứu hoặc nhắc về vé đã đặt của họ nếu được hỏi.");
            }
            else
            {
                systemInstruction.AppendLine("\nKhách hàng hiện tại chưa đăng nhập tài khoản. Nếu họ hỏi về vé đã mua của họ, hãy bảo họ đăng nhập trước.");
            }

            systemInstruction.AppendLine("\nDưới đây là thông tin thực tế từ cơ sở dữ liệu rạp phim CinemaHub của chúng tôi:");
            
            systemInstruction.AppendLine("\n1. Phim đang chiếu tại rạp:");
            foreach (var movie in context.NowShowing)
            {
                systemInstruction.AppendLine($"- {movie.Title} (Thời lượng: {movie.Duration} phút) - Mô tả: {movie.Description}");
            }

            systemInstruction.AppendLine("\n2. Phim sắp khởi chiếu:");
            foreach (var movie in context.ComingSoon)
            {
                systemInstruction.AppendLine($"- {movie.Title} (Thời lượng: {movie.Duration} phút)");
            }

            systemInstruction.AppendLine("\n3. Lịch chiếu ngày hôm nay:");
            if (context.TodayShowtimes.Any())
            {
                foreach (var st in context.TodayShowtimes)
                {
                    systemInstruction.AppendLine($"- Phim: {st.MovieTitle} | Suất: {st.StartTime} | Phòng: {st.RoomName} | Giá vé: {st.Price:N0} VNĐ");
                }
            }
            else
            {
                systemInstruction.AppendLine("- Hôm nay hiện tại chưa xếp lịch chiếu.");
            }

            systemInstruction.AppendLine("\n4. Thông tin liên hệ và chính sách:");
            systemInstruction.AppendLine("- Địa chỉ rạp: 235 Hoàng Quốc Việt, phường Nghĩa Đô, quận Bắc Từ Liêm, Hà Nội.");
            systemInstruction.AppendLine("- Hotline hỗ trợ: 0344 596 643");
            systemInstruction.AppendLine("- Email hỗ trợ: ducmanh2005vt@gmail.com");
            systemInstruction.AppendLine("- Giá vé: Vé thường dao động từ 60.000đ - 75.000đ, vé VIP khoảng 85.000đ - 90.000đ tùy thuộc vào khung giờ và phòng chiếu.");
            systemInstruction.AppendLine("- Hình thức thanh toán trực tuyến: VNPay.");
            systemInstruction.AppendLine("- Chính sách đặt vé: Khi đặt vé online thành công, mã vé sẽ được lưu trong mục 'Lịch sử đặt vé'. Vé tạm giữ sẽ tự động hủy sau 15 phút nếu chưa thanh toán.");
            systemInstruction.AppendLine("- Hướng dẫn đặt vé: Chọn Lịch chiếu hoặc Phim -> Chọn suất -> Chọn ghế -> Thanh toán VNPay -> Kiểm tra vé.");
            systemInstruction.AppendLine("\nLưu ý: Chỉ trả lời dựa vào thông tin của rạp CinemaHub đã cung cấp. Nếu người dùng hỏi điều gì không liên quan đến rạp phim hoặc điện ảnh, hãy từ chối trả lời khéo léo và hướng người dùng hỏi về phim ảnh/vé xem phim.");

            // Chuẩn bị payload body cho Gemini API
            var contentsList = new List<object>();

            // Gộp lịch sử chat và tin nhắn hiện tại
            var allMessages = new List<ChatMessageDto>(history);
            
            // Nếu tin nhắn cuối trong lịch sử trùng với tin nhắn hiện tại (cả về sender và nội dung),
            // ta không cần thêm tin nhắn hiện tại vào nữa để tránh bị trùng lặp.
            bool alreadyContainsCurrent = allMessages.Count > 0 && 
                allMessages[^1].Sender.Equals("user", StringComparison.OrdinalIgnoreCase) && 
                allMessages[^1].Text == message;

            if (!alreadyContainsCurrent)
            {
                allMessages.Add(new ChatMessageDto { Sender = "user", Text = message });
            }

            // Đảm bảo các lượt hội thoại xen kẽ (user - model - user - model) theo yêu cầu của Gemini API
            var alternatingMessages = new List<ChatMessageDto>();
            foreach (var msg in allMessages)
            {
                var role = msg.Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user" : "model";
                if (alternatingMessages.Count > 0 && alternatingMessages[^1].Sender == role)
                {
                    // Nếu trùng role liên tiếp, gộp nội dung lại bằng dấu xuống dòng
                    alternatingMessages[^1].Text += "\n" + msg.Text;
                }
                else
                {
                    alternatingMessages.Add(new ChatMessageDto { Sender = role, Text = msg.Text });
                }
            }

            // Lấy tối đa 6 lượt hội thoại gần nhất để tiết kiệm token và đảm bảo tốc độ
            var recentHistory = alternatingMessages.Skip(Math.Max(0, alternatingMessages.Count - 6)).ToList();
            foreach (var msg in recentHistory)
            {
                contentsList.Add(new
                {
                    role = msg.Sender,
                    parts = new[] { new { text = msg.Text } }
                });
            }

            var requestBody = new
            {
                contents = contentsList,
                systemInstruction = new
                {
                    parts = new[] { new { text = systemInstruction.ToString() } }
                },
                generationConfig = new
                {
                    temperature = 0.5,
                    maxOutputTokens = 2048
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var candidates) && 
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentNode) &&
                    contentNode.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? string.Empty;
                }
                
                throw new Exception("Gemini API response was successful, but could not parse the response JSON structure.");
            }
            else
            {
                var responseError = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API call failed with status code {response.StatusCode}. Error details: {responseError}");
            }
        }

        private string ProcessOfflineQuery(string message, CinemaContextData context, string userFullName, bool isLoggedIn)
        {
            var text = RemoveDiacritics(message.ToLower().Trim());

            // 1. Phim đang chiếu
            if (text.Contains("phim dang chieu") || text.Contains("phim hot") || text.Contains("dang chieu") || text.Contains("phim moi"))
            {
                if (!context.NowShowing.Any())
                {
                    return "Hiện tại rạp đang cập nhật danh sách phim mới. Bạn vui lòng quay lại sau nhé!";
                }

                var sb = new StringBuilder();
                sb.AppendLine(isLoggedIn ? $"Chào **{userFullName}**! Dưới đây là danh sách phim đang chiếu tại CinemaHub:" : "**Các phim đang chiếu tại CinemaHub:**\n");
                sb.AppendLine("Bạn có thể vuốt xem các phim đang chiếu và đặt vé trực tiếp trên thẻ phim dưới đây:");
                return sb.ToString();
            }

            // 2. Phim sắp chiếu
            if (text.Contains("phim sap chieu") || text.Contains("sap chieu") || text.Contains("sap ra mat"))
            {
                if (!context.ComingSoon.Any())
                {
                    return "Hiện chưa có phim sắp chiếu mới được lên lịch. Hãy theo dõi website thường xuyên nhé!";
                }

                var sb = new StringBuilder();
                sb.AppendLine(isLoggedIn ? $"Chào **{userFullName}**! Dưới đây là danh sách phim sắp chiếu tại CinemaHub:" : "**Các phim sắp khởi chiếu:**\n");
                sb.AppendLine("Vuốt ngang thanh trượt phim bên dưới để xem thêm các tác phẩm sắp ra mắt:");
                return sb.ToString();
            }

            // 3. Lịch chiếu hôm nay
            if (text.Contains("lich chieu") || text.Contains("suat chieu") || text.Contains("gio chieu") || text.Contains("lich chieu hom nay"))
            {
                if (!context.TodayShowtimes.Any())
                {
                    return "Hôm nay rạp chưa lên lịch chiếu phim nào, hoặc các suất chiếu hôm nay đã kết thúc. Bạn vui lòng xem lịch chiếu ngày mai trên website nhé!";
                }

                var sb = new StringBuilder();
                sb.AppendLine(isLoggedIn ? $"Chào **{userFullName}**! Dưới đây là lịch chiếu hôm nay ({DateTime.Today:dd/MM/yyyy}):" : $"**Lịch chiếu hôm nay ({DateTime.Today:dd/MM/yyyy}):**\n");
                sb.AppendLine("Chọn suất chiếu phù hợp bên dưới để vào trang đặt ghế ngồi:");
                return sb.ToString();
            }

            // 4. Giá vé
            if (text.Contains("gia ve") || text.Contains("ve bao nhieu") || text.Contains("tien ve") || text.Contains("bang gia"))
            {
                return "**Bảng giá vé tại rạp CinemaHub:**\n\n" +
                       "- **Ghế Thường (2D):**\n" +
                       "  * Ngày thường (T2 - T5): **60.000 VNĐ**\n" +
                       "  * Cuối tuần (T6 - CN, Lễ): **75.000 VNĐ**\n\n" +
                       "- **Ghế VIP / Suất chiếu đặc biệt:**\n" +
                       "  * Ngày thường: **85.000 VNĐ**\n" +
                       "  * Cuối tuần: **90.000 VNĐ**\n\n" +
                       "*(Giá vé có thể thay đổi tùy thuộc vào định dạng phòng chiếu VIP/3D hoặc các chương trình khuyến mãi hiện hành)*";
            }

            // 5. Liên hệ & Địa chỉ
            if (text.Contains("dia chi") || text.Contains("rap o dau") || text.Contains("lien he") || text.Contains("hotline") || text.Contains("email") || text.Contains("sdt"))
            {
                return "**Thông tin liên hệ rạp chiếu phim CinemaHub:**\n\n" +
                       "- **Địa chỉ:** 235 Hoàng Quốc Việt, phường Nghĩa Đô, quận Bắc Từ Liêm, TP. Hà Nội\n" +
                       "- **Hotline:** 0344 596 643\n" +
                       "- **Email hỗ trợ:** ducmanh2005vt@gmail.com\n" +
                       "- **Thời gian mở cửa:** 8:00 - 23:30 hàng ngày (kể cả Lễ, Tết)\n\n" +
                       "Rất hân hạnh được phục vụ quý khách!";
            }

            // 6. Hướng dẫn đặt vé
            if (text.Contains("dat ve") || text.Contains("huong dan") || text.Contains("mua ve") || text.Contains("dat nhu nao"))
            {
                return "**Hướng dẫn các bước đặt vé xem phim online tại CinemaHub:**\n\n" +
                       "1. **Bước 1:** Chọn phim và suất chiếu thích hợp trong mục **Lịch chiếu**.\n" +
                       "2. **Bước 2:** Chọn vị trí ghế ngồi mong muốn (Ghế trống màu xám/xanh, ghế đã đặt có màu đỏ).\n" +
                       "3. **Bước 3:** Bấm **Thanh toán**, hệ thống sẽ chuyển hướng sang cổng thanh toán **VNPay**.\n" +
                       "4. **Bước 4:** Thực hiện thanh toán. Sau khi thành công, vé của bạn sẽ lập tức hiển thị trong mục **Lịch sử đặt vé** (click vào tên của bạn ở góc trên bên phải).\n\n" +
                       "*Lưu ý: Vé tạm giữ sẽ bị hủy tự động sau 15 phút nếu bạn không hoàn tất thanh toán.*";
            }

            // 7. Vé của tôi
            if (text.Contains("ve cua toi") || text.Contains("ve da dat") || text.Contains("da dat") || text.Contains("lich su") || text.Contains("ve cua minh") || text.Contains("ve da mua") || text.Contains("lich su dat ve"))
            {
                if (!isLoggedIn)
                {
                    return "Bạn vui lòng đăng nhập tài khoản để tra cứu lịch sử đặt vé xem phim của mình nhé!";
                }
                return $"Chào **{userFullName}**! Dưới đây là các vé bạn đã đặt gần đây được hiển thị chi tiết bên dưới:";
            }

            // 8. Chào hỏi
            var words = text.Split(new[] { ' ', '.', ',', '!', '?', ';', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (text.Contains("xin chao") || text.Contains("chao bot") || text.Contains("hello") || 
                words.Contains("hi") || words.Contains("chao") || words.Contains("ad"))
            {
                var greeting = isLoggedIn ? $"Xin chào **{userFullName}**! Tôi là Trợ lý ảo CinemaHub Assistant.\n\n" : "Xin chào! Tôi là Trợ lý ảo CinemaHub Assistant.\n\n";
                return greeting +
                       "Hiện tại hệ thống AI đang ở chế độ rà soát ngoại tuyến. Tôi có thể hỗ trợ bạn nhanh các thông tin sau:\n" +
                       "- [Xem danh sách phim đang chiếu hoặc phim sắp chiếu](query:phim đang chiếu)\n" +
                       "- [Xem lịch chiếu hôm nay](query:lịch chiếu hôm nay)\n" +
                       "- [Thông tin giá vé, địa chỉ, hotline liên hệ](query:giá vé)\n" +
                       "- [Hướng dẫn đặt vé online](query:đặt vé)\n\n" +
                       "Bạn muốn tìm hiểu thông tin nào?";
            }

            // Mặc định
            return "Cảm ơn câu hỏi của bạn. Tôi là Trợ lý ảo CinemaHub. Ở chế độ ngoại tuyến, tôi chưa hiểu được câu hỏi này.\n\n" +
                   "Bạn có thể thử nhập câu hỏi trực tiếp hoặc nhấn vào các hành động gợi ý dưới đây:\n" +
                   "- [Phim đang chiếu](query:phim đang chiếu)\n" +
                   "- [Lịch chiếu hôm nay](query:lịch chiếu hôm nay)\n" +
                   "- [Giá vé & Liên hệ](query:giá vé)\n" +
                   "- [Đặt vé online](query:đặt vé)";
        }

        // Loại bỏ dấu tiếng Việt để so khớp keyword chính xác
        private string RemoveDiacritics(string text)
        {
            string[] arr1 = new string[] { "á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ",
                                            "đ",
                                            "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
                                            "í","ì","ỉ","ĩ","ị",
                                            "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
                                            "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
                                            "ý","ỳ","ỷ","ỹ","ỵ",};
            string[] arr2 = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
                                            "d",
                                            "e","e","e","e","e","e","e","e","e","e","e",
                                            "i","i","i","i","i",
                                            "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
                                            "u","u","u","u","u","u","u","u","u","u","u",
                                            "y","y","y","y","y",};
            for (int i = 0; i < arr1.Length; i++)
            {
                text = text.Replace(arr1[i], arr2[i]);
                text = text.Replace(arr1[i].ToUpper(), arr2[i].ToUpper());
            }
            return text;
        }

        // Lớp chứa dữ liệu ngữ cảnh
        private class CinemaContextData
        {
            public string CurrentTime { get; set; } = string.Empty;
            public List<MovieContextInfo> NowShowing { get; set; } = new();
            public List<MovieContextInfo> ComingSoon { get; set; } = new();
            public List<ShowtimeContextInfo> TodayShowtimes { get; set; } = new();
        }

        private class MovieContextInfo
        {
            public string Title { get; set; } = string.Empty;
            public int Duration { get; set; }
            public string? Description { get; set; }
        }

        private class ShowtimeContextInfo
        {
            public string MovieTitle { get; set; } = string.Empty;
            public string RoomName { get; set; } = string.Empty;
            public string StartTime { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }
    }
}
