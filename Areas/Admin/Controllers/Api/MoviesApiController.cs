using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaManagement.Areas.Admin.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesApiController : ControllerBase
    {
        private readonly IMovieService _movieService;

        public MoviesApiController(IMovieService movieService) => _movieService = movieService;

        // GET api/MoviesApi
        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _movieService.GetAllMoviesAsync());

        // GET api/MoviesApi/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);
            return movie == null ? NotFound() : Ok(movie);
        }

        // POST api/MoviesApi  [Admin]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateMovieDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _movieService.CreateMovieAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.MovieId }, created);
        }

        // PUT api/MoviesApi/5  [Admin]
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateMovieDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _movieService.UpdateMovieAsync(id, dto);
            return result ? NoContent() : NotFound();
        }

        // DELETE api/MoviesApi/5  [Admin]
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _movieService.DeleteMovieAsync(id);
            return result ? NoContent() : NotFound();
        }
    }
}
