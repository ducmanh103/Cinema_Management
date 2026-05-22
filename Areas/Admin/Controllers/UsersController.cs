using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class UsersController : Controller
    {
        private readonly CinemaDbContext _context;

        public UsersController(CinemaDbContext context) => _context = context;

        // GET: /Admin/Users
        public async Task<IActionResult> Index()
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
                    RoleName = u.Role.RoleName,
                    RoleId = u.RoleId
                })
                .ToListAsync();

            ViewBag.Roles = await _context.Roles.AsNoTracking().ToListAsync();
            return View(users);
        }

        // POST: /Admin/Users/ToggleUserStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var targetUser = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
            if (targetUser == null)
                return RedirectToAction(nameof(Index));

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != null && int.TryParse(currentUserId, out int currentId) && currentId == targetUser.UserId)
            {
                TempData["Error"] = "Bạn không thể khoá/mở khoá chính mình.";
                return RedirectToAction(nameof(Index));
            }

            if (!User.IsInRole("Admin") && targetUser.Role.RoleName != "Customer")
            {
                TempData["Error"] = "Staff chỉ có thể quản lý tài khoản khách hàng.";
                return RedirectToAction(nameof(Index));
            }

            targetUser.Status = targetUser.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã {(targetUser.Status == "Active" ? "mở khoá" : "khoá")} người dùng '{targetUser.Username}' thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(int userId, int roleId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (user != null && role != null)
            {
                user.RoleId = roleId;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã đổi vai trò của '{user.Username}' thành '{role.RoleName}'.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
