using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaManagement.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShowtimesApiController : ControllerBase
    {
        private readonly IShowtimeService _showtimeService;

        public ShowtimesApiController(IShowtimeService showtimeService) => _showtimeService = showtimeService;

        // GET api/ShowtimesApi/bymovie/5
        [HttpGet("bymovie/{movieId}")]
        public async Task<IActionResult> GetByMovie(int movieId) =>
            Ok(await _showtimeService.GetShowtimesByMovieAsync(movieId));

        // GET api/ShowtimesApi/bydate/2025-06-15
        [HttpGet("bydate/{date}")]
        public async Task<IActionResult> GetByDate(DateTime date) =>
            Ok(await _showtimeService.GetShowtimesByDateAsync(date));

        // GET api/ShowtimesApi/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var st = await _showtimeService.GetShowtimeByIdAsync(id);
            return st == null ? NotFound() : Ok(st);
        }

        // GET api/ShowtimesApi/5/seats
        [HttpGet("{id}/seats")]
        public async Task<IActionResult> GetSeatStatus(int id) =>
            Ok(await _showtimeService.GetSeatStatusAsync(id));

        // POST api/ShowtimesApi  [Admin, Staff]
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Create([FromBody] CreateShowtimeDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _showtimeService.CreateShowtimeAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.ShowtimeId }, created);
        }

        // DELETE api/ShowtimesApi/5  [Admin]
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _showtimeService.DeleteShowtimeAsync(id);
            return result ? NoContent() : NotFound();
        }
    }
}
