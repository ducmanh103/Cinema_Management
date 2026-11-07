using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CinemaManagement.Areas.Admin.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesApiController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly IFileStorageService _fileStorage;

        public MoviesApiController(
            IMovieService movieService,
            IFileStorageService fileStorage)
        {
            _movieService = movieService;
            _fileStorage = fileStorage;
        }

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

        // POST api/MoviesApi  [Admin] - Thêm phim qua JSON (hỗ trợ tự động tải ảnh từ xa về server)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateMovieDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Nếu cờ DownloadRemoteImages bật, tự động tải các URL từ xa về lưu trên server
            if (dto.DownloadRemoteImages)
            {
                if (!string.IsNullOrWhiteSpace(dto.PosterUrl) &&
                    (dto.PosterUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || dto.PosterUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var savedPoster = await _fileStorage.DownloadAndSaveRemoteImageAsync(dto.PosterUrl, "movies");
                    if (!string.IsNullOrEmpty(savedPoster)) dto.PosterUrl = savedPoster;
                }

                if (!string.IsNullOrWhiteSpace(dto.BannerUrl) &&
                    (dto.BannerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || dto.BannerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var savedBanner = await _fileStorage.DownloadAndSaveRemoteImageAsync(dto.BannerUrl, "movies");
                    if (!string.IsNullOrEmpty(savedBanner)) dto.BannerUrl = savedBanner;
                }
            }

            var created = await _movieService.CreateMovieAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.MovieId }, created);
        }

        // POST api/MoviesApi/create-form  [Admin] - Thêm phim từ xa hỗ trợ trực tiếp multipart/form-data upload file poster/banner
        [HttpPost("create-form")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateWithFiles([FromForm] CreateMovieFormDto form)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            string? finalPosterUrl = form.PosterUrl;
            string? finalBannerUrl = form.BannerUrl;

            // Xử lý Poster File hoặc Download
            if (form.PosterFile != null && form.PosterFile.Length > 0)
            {
                finalPosterUrl = await _fileStorage.SaveFileAsync(form.PosterFile, "movies");
            }
            else if (!string.IsNullOrWhiteSpace(form.PosterUrl) && form.DownloadRemotePoster)
            {
                var downloaded = await _fileStorage.DownloadAndSaveRemoteImageAsync(form.PosterUrl, "movies");
                if (!string.IsNullOrEmpty(downloaded)) finalPosterUrl = downloaded;
            }

            // Xử lý Banner File hoặc Download
            if (form.BannerFile != null && form.BannerFile.Length > 0)
            {
                finalBannerUrl = await _fileStorage.SaveFileAsync(form.BannerFile, "movies");
            }
            else if (!string.IsNullOrWhiteSpace(form.BannerUrl) && form.DownloadRemoteBanner)
            {
                var downloaded = await _fileStorage.DownloadAndSaveRemoteImageAsync(form.BannerUrl, "movies");
                if (!string.IsNullOrEmpty(downloaded)) finalBannerUrl = downloaded;
            }

            var dto = new CreateMovieDto
            {
                Title = form.Title,
                Duration = form.Duration,
                Description = form.Description,
                ReleaseDate = form.ReleaseDate,
                Status = form.Status,
                GenreIds = form.GenreIds,
                PosterUrl = finalPosterUrl,
                BannerUrl = finalBannerUrl
            };

            var created = await _movieService.CreateMovieAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.MovieId }, created);
        }

        // POST api/MoviesApi/upload  [Admin] - Upload file ảnh đơn lẻ trả về đường dẫn URL trên server
        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không tìm thấy tệp tải lên." });

            try
            {
                var path = await _fileStorage.SaveFileAsync(file, "movies");
                if (string.IsNullOrEmpty(path))
                    return BadRequest(new { error = "Không thể lưu tệp." });

                return Ok(new
                {
                    url = path,
                    fileName = file.FileName,
                    size = file.Length
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        public record RemoteImageRequest(string ImageUrl);

        // POST api/MoviesApi/download-remote  [Admin] - Tải ảnh từ URL bên ngoài về lưu trữ nội bộ trên server
        [HttpPost("download-remote")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadRemoteImage([FromBody] RemoteImageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.ImageUrl))
                return BadRequest(new { error = "ImageUrl không được để trống." });

            var savedPath = await _fileStorage.DownloadAndSaveRemoteImageAsync(request.ImageUrl.Trim(), "movies");
            if (string.IsNullOrEmpty(savedPath))
                return BadRequest(new { error = "Không thể tải ảnh từ URL cung cấp hoặc định dạng không hợp lệ." });

            return Ok(new { url = savedPath });
        }

        // PUT api/MoviesApi/5  [Admin]
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateMovieDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (dto.DownloadRemoteImages)
            {
                if (!string.IsNullOrWhiteSpace(dto.PosterUrl) &&
                    (dto.PosterUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || dto.PosterUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var savedPoster = await _fileStorage.DownloadAndSaveRemoteImageAsync(dto.PosterUrl, "movies");
                    if (!string.IsNullOrEmpty(savedPoster)) dto.PosterUrl = savedPoster;
                }

                if (!string.IsNullOrWhiteSpace(dto.BannerUrl) &&
                    (dto.BannerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || dto.BannerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var savedBanner = await _fileStorage.DownloadAndSaveRemoteImageAsync(dto.BannerUrl, "movies");
                    if (!string.IsNullOrEmpty(savedBanner)) dto.BannerUrl = savedBanner;
                }
            }

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
