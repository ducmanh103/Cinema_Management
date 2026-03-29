using CinemaManagement.Models.ViewModels;

namespace CinemaManagement.Services
{
    public interface IMovieService
    {
        Task<List<MovieDto>> GetAllMoviesAsync();
        Task<MovieDto?> GetMovieByIdAsync(int id);
        Task<MovieDto> CreateMovieAsync(CreateMovieDto dto);
        Task<bool> UpdateMovieAsync(int id, CreateMovieDto dto);
        Task<bool> DeleteMovieAsync(int id);
    }

    public interface IShowtimeService
    {
        Task<List<ShowtimeDto>> GetShowtimesByMovieAsync(int movieId);
        Task<List<ShowtimeDto>> GetShowtimesByDateAsync(DateTime date);
        Task<ShowtimeDto?> GetShowtimeByIdAsync(int id);
        Task<List<SeatStatusDto>> GetSeatStatusAsync(int showtimeId);
        Task<ShowtimeDto> CreateShowtimeAsync(CreateShowtimeDto dto);
        Task<bool> DeleteShowtimeAsync(int id);
    }

    public interface ITicketService
    {
        Task<TicketDto> BookTicketAsync(int userId, BookTicketDto dto);
        Task<List<TicketDto>> GetUserTicketsAsync(int userId);
        Task<bool> CancelTicketAsync(int ticketId, int userId);
    }
}
