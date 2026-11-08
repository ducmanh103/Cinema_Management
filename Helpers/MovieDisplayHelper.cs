namespace CinemaManagement.Helpers
{
    /// <summary>
    /// Helper tạo dữ liệu hiển thị "giả" (mock) cho phim khi chưa có trường tương ứng trong database.
    /// TODO: Thêm trường AgeRating, Format vào model Movie thay vì dùng fake data.
    /// </summary>
    public static class MovieDisplayHelper
    {
        /// <summary>
        /// Lấy CSS class cho badge phân loại tuổi dựa trên tên phim.
        /// </summary>
        public static string GetAgeBadgeClass(string title)
        {
            string t = title.ToLower();
            if (t.Contains("dune") || t.Contains("mai") || t.Contains("quật mộ") || t.Contains("sát thủ john wick"))
                return "badge-age-t18";
            if (t.Contains("biệt đội săn ma") || t.Contains("võ sĩ giác đấu") || t.Contains("deadpool"))
                return "badge-age-t16";
            if (t.Contains("vùng đất câm lặng") || t.Contains("fast"))
                return "badge-age-t13";
            return "badge-age-p";
        }

        /// <summary>
        /// Lấy text cho badge phân loại tuổi (T18, T16, T13, P).
        /// </summary>
        public static string GetAgeBadgeText(string title)
        {
            string t = title.ToLower();
            if (t.Contains("dune") || t.Contains("mai") || t.Contains("quật mộ") || t.Contains("sát thủ john wick"))
                return "T18";
            if (t.Contains("biệt đội săn ma") || t.Contains("võ sĩ giác đấu") || t.Contains("deadpool"))
                return "T16";
            if (t.Contains("vùng đất câm lặng") || t.Contains("fast"))
                return "T13";
            return "P";
        }

        /// <summary>
        /// Lấy mô tả cho badge phân loại tuổi.
        /// </summary>
        public static string GetAgeBadgeDesc(string title)
        {
            string t = title.ToLower();
            if (t.Contains("dune") || t.Contains("mai") || t.Contains("quật mộ") || t.Contains("sát thủ john wick"))
                return "Phim dành cho khán giả từ 18 tuổi trở lên";
            if (t.Contains("biệt đội săn ma") || t.Contains("võ sĩ giác đấu") || t.Contains("deadpool"))
                return "Phim dành cho khán giả từ 16 tuổi trở lên";
            if (t.Contains("vùng đất câm lặng") || t.Contains("fast"))
                return "Phim dành cho khán giả từ 13 tuổi trở lên";
            return "Phim dành cho mọi lứa tuổi (Phổ biến)";
        }

        /// <summary>
        /// Lấy format chiếu phim dựa trên tên phim.
        /// </summary>
        public static string GetMovieFormat(string title)
        {
            string t = title.ToLower();
            if (t.Contains("avatar") || t.Contains("godzilla") || t.Contains("dune") || t.Contains("người nhện"))
                return "IMAX 3D";
            if (t.Contains("fast") || t.Contains("john wick") || t.Contains("sonic") || t.Contains("mufasa") || t.Contains("giác đấu"))
                return "3D ATMOS";
            return "2D DIGITAL";
        }

        /// <summary>
        /// Tạo rating ổn định dựa trên MovieId (mock).
        /// </summary>
        public static double GetMovieRating(int id)
        {
            return 7.5 + (id % 13) * 0.16;
        }

        /// <summary>
        /// Chuẩn hóa URL YouTube bất kỳ (watch?v=, youtu.be/, embed/) thành URL embed chuẩn.
        /// </summary>
        public static string NormalizeYouTubeEmbedUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            url = url.Trim();

            // Nếu đã là link embed chuẩn
            var embedMatch = System.Text.RegularExpressions.Regex.Match(url, @"youtube\.com/embed/([a-zA-Z0-9_-]{11})");
            if (embedMatch.Success)
                return $"https://www.youtube.com/embed/{embedMatch.Groups[1].Value}";

            // Dạng rút gọn: youtu.be/VIDEO_ID
            var shortMatch = System.Text.RegularExpressions.Regex.Match(url, @"youtu\.be/([a-zA-Z0-9_-]{11})");
            if (shortMatch.Success)
                return $"https://www.youtube.com/embed/{shortMatch.Groups[1].Value}";

            // Dạng chuẩn: youtube.com/watch?v=VIDEO_ID
            var watchMatch = System.Text.RegularExpressions.Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})");
            if (watchMatch.Success)
                return $"https://www.youtube.com/embed/{watchMatch.Groups[1].Value}";

            return url;
        }

        /// <summary>
        /// Lấy URL YouTube Embed Trailer chuẩn xác cho từng phim theo tên hoặc URL trực tiếp.
        /// Tuyệt đối không trỏ mặc định về Dune 2 nữa.
        /// </summary>
        public static string GetTrailerEmbedUrl(string title, string? directTrailerUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(directTrailerUrl))
            {
                var normalized = NormalizeYouTubeEmbedUrl(directTrailerUrl);
                if (!string.IsNullOrWhiteSpace(normalized))
                    return normalized;
            }

            if (string.IsNullOrWhiteSpace(title))
                return "https://www.youtube.com/embed/_inKs4eeHiI"; // Mặc định Kung Fu Panda 4 thân thiện

            string t = title.ToLower().Trim();

            // 1. Kung Fu Panda 4
            if (t.Contains("kung fu panda") || t.Contains("panda 4") || t.Contains("gấu trúc"))
                return "https://www.youtube.com/embed/_inKs4eeHiI";

            // 2. Dune: Phần 2 (Chỉ đúng phim Dune mới trả về Dune)
            if (t.Contains("dune"))
                return "https://www.youtube.com/embed/Way9Dexny3w";

            // 3. Phim Mai (Trấn Thành)
            if (t == "mai" || t.StartsWith("mai ") || t.Contains("phim mai") || t.Contains("mai (2024)"))
                return "https://www.youtube.com/embed/W8VekFsJKZc";

            // 4. Quật Mộ Trùng Ma (Exhuma)
            if (t.Contains("quật mộ") || t.Contains("exhuma") || t.Contains("trùng ma"))
                return "https://www.youtube.com/embed/ag5d9Su53nA";

            // 5. Godzilla x Kong: Đế Chế Mới (The New Empire)
            if (t.Contains("godzilla") || t.Contains("kong"))
                return "https://www.youtube.com/embed/lV1OOlGwExM";

            // 6. Avatar 3: Lửa và Tro Tàn (Fire and Ash) - Kiểm tra trước Avatar 1
            if (t.Contains("avatar 3") || t.Contains("lửa và tro tàn") || t.Contains("fire and ash") || t.Contains("tro tàn"))
                return "https://www.youtube.com/embed/a8Gx8wiNbs8";

            // 7. Avatar (2009)
            if (t.Contains("avatar"))
                return "https://www.youtube.com/embed/d9MyW72ELq0";

            // 8. Deadpool 2 / Deadpool
            if (t.Contains("deadpool"))
                return "https://www.youtube.com/embed/73_1biulkYk";

            // 9. Fast & Furious 10 (Fast X)
            if (t.Contains("fast") || t.Contains("furious"))
                return "https://www.youtube.com/embed/32RAq6JzguY";

            // 10. Biệt Đội Săn Ma: Kỷ Nguyên Băng Giá (Ghostbusters: Frozen Empire)
            if (t.Contains("săn ma") || t.Contains("ghostbusters") || t.Contains("băng giá"))
                return "https://www.youtube.com/embed/AbkYj4X_9g4";

            // 11. Võ Sĩ Giác Đấu 2 (Gladiator II)
            if (t.Contains("giác đấu") || t.Contains("gladiator"))
                return "https://www.youtube.com/embed/4rgYUipGJNo";

            // 12. Sát Thủ John Wick: Phần 4 (John Wick: Chapter 4)
            if (t.Contains("john wick"))
                return "https://www.youtube.com/embed/qEVUtrk8_B4";

            // 13. Mufasa: Vua Sư Tử (Mufasa: The Lion King)
            if (t.Contains("mufasa") || t.Contains("vua sư tử") || t.Contains("lion king"))
                return "https://www.youtube.com/embed/o17MF9vnabg";

            // 14. Vùng Đất Câm Lặng: Ngày Một (A Quiet Place: Day One)
            if (t.Contains("câm lặng") || t.Contains("quiet place"))
                return "https://www.youtube.com/embed/YPY7J-flzE8";

            // 15. Sonic the Hedgehog 3
            if (t.Contains("sonic"))
                return "https://www.youtube.com/embed/qSu6i2iFMO0";

            // 16. Người Nhện: Du Hành Vũ Trụ Nhện (Spider-Man: Across the Spider-Verse)
            if (t.Contains("nhện") || t.Contains("spider"))
                return "https://www.youtube.com/embed/shW9i6k8cB0";

            // 17. Titanic
            if (t.Contains("titanic"))
                return "https://www.youtube.com/embed/I10q6fj4n3U";

            // 18. Chiến Binh Báo Đen: Wakanda Bất Diệt (Black Panther: Wakanda Forever)
            if (t.Contains("báo đen") || t.Contains("wakanda") || t.Contains("black panther"))
                return "https://www.youtube.com/embed/_Z3QKkl1WyM";

            // 19. Zootopia 2 (Phi Vụ Động Trời 2)
            if (t.Contains("zootopia") || t.Contains("phi vụ động trời"))
                return "https://www.youtube.com/embed/jWM0ct-OLsM";

            // Fallback an toàn, không bị Dune 2
            return "https://www.youtube.com/embed/_inKs4eeHiI";
        }
    }
}
