using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CinemaManagement.Areas.Admin.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketsApiController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsApiController(ITicketService ticketService) => _ticketService = ticketService;

        // POST api/TicketsApi/book
        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] BookTicketDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();

            int userId = int.Parse(userIdClaim);

            if (dto.PaymentMethod == "Cash" && !User.IsInRole("Admin") && !User.IsInRole("Staff"))
            {
                return BadRequest(new { message = "Bạn phải thanh toán vé trực tuyến bằng Ứng dụng ngân hàng hoặc Momo." });
            }

            try
            {
                var ticket = await _ticketService.BookTicketAsync(userId, dto);
                return Ok(ticket);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        // GET api/TicketsApi/my
        [HttpGet("my")]
        public async Task<IActionResult> GetMyTickets()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);
            return Ok(await _ticketService.GetUserTicketsAsync(userId));
        }

        // DELETE api/TicketsApi/5/cancel
        [HttpDelete("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);
            var result = await _ticketService.CancelTicketAsync(id, userId);
            return result ? Ok(new { message = "Đã huỷ vé thành công." }) : NotFound();
        }
    }
}
