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
    }
}
