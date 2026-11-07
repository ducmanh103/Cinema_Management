using System.Collections.Generic;

namespace CinemaManagement.Models.ViewModels
{
    /// <summary>
    /// ViewModel tổng hợp dữ liệu thời gian thực từ Backend cho trang Landing Page (Home/Index).
    /// </summary>
    public class HomeLandingViewModel
    {
        public List<MovieDto> FeaturedMovies { get; set; } = new();
        public List<MovieDto> NowShowing { get; set; } = new();
        public List<MovieDto> ComingSoon { get; set; } = new();
        public List<MovieDto> TopBoxOffice { get; set; } = new();
        public List<string> Genres { get; set; } = new();
        public List<TheaterCardDto> Theaters { get; set; } = new();
        public List<ShowtimeDto> UpcomingShowtimes { get; set; } = new();

        // Thống kê hệ thống thời gian thực từ PostgreSQL
        public int TotalTheaters { get; set; }
        public int TotalRooms { get; set; }
        public int TotalSeats { get; set; }
        public int ShowtimesToday { get; set; }
        public int NowShowingCount { get; set; }
        public int ComingSoonCount { get; set; }
    }
}

