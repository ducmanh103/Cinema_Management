using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersApiController : ControllerBase
    {
        private readonly CinemaDbContext _context;

        public UsersApiController(CinemaDbContext context) => _context = context;

        // GET api/UsersApi
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    Status = u.Status,
                    RoleName = u.Role.RoleName
                })
                .ToListAsync();
            return Ok(users);
        }

        // GET api/UsersApi/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    Status = u.Status,
                    RoleName = u.Role.RoleName
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();

            return Ok(user);
        }

        // PUT api/UsersApi/5/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] string status)
        {
            if (status != "Active" && status != "Inactive")
                return BadRequest(new { message = "Status phải là 'Active' hoặc 'Inactive'." });

            var affected = await _context.Users
                .Where(u => u.UserId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, status));

            if (affected == 0) return NotFound();
            return NoContent();
        }

        // DELETE api/UsersApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool hasTickets = await _context.Tickets.AnyAsync(t => t.UserId == id);
            if (hasTickets)
                return Conflict(new { message = "Không thể xoá người dùng đã có vé. Hãy khoá tài khoản thay vì xoá." });

            var affected = await _context.Users
                .Where(u => u.UserId == id)
                .ExecuteDeleteAsync();

            if (affected == 0) return NotFound();
            return NoContent();
        }
    }
}
