using System;
using System.Linq;
using System.Threading.Tasks;
using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class UsersController : Controller
    {
        private readonly CinemaDbContext _context;
        private readonly ISecurityAlertService _securityAlert;
        private readonly IMemoryCache _cache;

        public UsersController(
            CinemaDbContext context,
            ISecurityAlertService securityAlert,
            IMemoryCache cache)
        {
            _context = context;
            _securityAlert = securityAlert;
            _cache = cache;
        }

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
            targetUser.SecurityStamp = Guid.NewGuid().ToString("N");

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Ghi Audit Log vào cơ sở dữ liệu
            _context.AuditLogs.Add(new CinemaManagement.Models.AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUsername = User.Identity?.Name,
                ActorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                Action = "TOGGLE_USER_STATUS",
                EntityName = "User",
                EntityId = targetUser.UserId.ToString(),
                Details = $"Đổi trạng thái tài khoản '{targetUser.Username}' thành {targetUser.Status}",
                IpAddress = ip,
                Path = "/Admin/Users/ToggleUserStatus"
            });

            await _context.SaveChangesAsync();

            _cache.Remove($"auth_stamp_{targetUser.UserId}");
            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Medium,
                "ADMIN_USER_STATUS_CHANGED",
                $"Tài khoản '{targetUser.Username}' đã bị {(targetUser.Status == "Active" ? "mở khoá" : "khoá")}",
                $"Thực hiện bởi: {User.Identity?.Name}, Trạng thái mới: {targetUser.Status}",
                IpAddress: ip,
                Username: User.Identity?.Name,
                Path: "/Admin/Users/ToggleUserStatus"));

            TempData["Success"] = $"Đã {(targetUser.Status == "Active" ? "mở khoá" : "khoá")} người dùng '{targetUser.Username}' thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(int userId, int roleId)
        {
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
            var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (user != null && role != null)
            {
                var oldRole = user.Role?.RoleName ?? user.RoleId.ToString();
                user.RoleId = roleId;
                user.SecurityStamp = Guid.NewGuid().ToString("N");

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                _context.AuditLogs.Add(new CinemaManagement.Models.AuditLog
                {
                    TimestampUtc = DateTime.UtcNow,
                    ActorUsername = User.Identity?.Name,
                    ActorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                    Action = "CHANGE_USER_ROLE",
                    EntityName = "User",
                    EntityId = user.UserId.ToString(),
                    Details = $"Thay đổi vai trò của '{user.Username}': {oldRole} -> {role.RoleName}",
                    IpAddress = ip,
                    Path = "/Admin/Users/ChangeRole"
                });

                await _context.SaveChangesAsync();

                _cache.Remove($"auth_stamp_{user.UserId}");
                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.High,
                    "ADMIN_ROLE_CHANGED",
                    $"Thay đổi quyền hạn tài khoản '{user.Username}'",
                    $"Thực hiện bởi Admin '{User.Identity?.Name}': {oldRole} -> {role.RoleName}",
                    IpAddress: ip,
                    Username: User.Identity?.Name,
                    Path: "/Admin/Users/ChangeRole"));

                TempData["Success"] = $"Đã đổi vai trò của '{user.Username}' thành '{role.RoleName}'.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/DeleteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var targetUser = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Tickets)
                    .ThenInclude(t => t.Payment)
                .Include(u => u.PasswordResetTokens)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (targetUser == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng cần xóa.";
                return RedirectToAction(nameof(Index));
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? currentId = int.TryParse(currentUserId, out int parsedId) ? parsedId : null;
            if (currentId.HasValue && currentId.Value == targetUser.UserId)
            {
                TempData["Error"] = "Bạn không thể tự xóa tài khoản của chính mình.";
                return RedirectToAction(nameof(Index));
            }

            // Tuyệt đối không cho phép xóa bất kỳ tài khoản Quản trị viên (Admin) nào
            if (string.Equals(targetUser.Role?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Không được phép xóa tài khoản Quản trị viên (Admin). Tất cả tài khoản Quản trị viên đều được bảo vệ trong hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            int ticketCount = targetUser.Tickets.Count;

            // Xóa mềm (Soft Delete) & Ẩn danh hoá dữ liệu cá nhân theo Nghị định 13/2023/NĐ-CP
            // Giữ lại toàn bộ dữ liệu Vé & Thanh toán để bảo toàn báo cáo tài chính kế toán
            targetUser.IsDeleted = true;
            targetUser.DeletedAt = CinemaManagement.Helpers.VietnamTime.UtcNow;
            targetUser.Status = "Deleted";
            targetUser.SecurityStamp = Guid.NewGuid().ToString("N");
            targetUser.PasswordHash = null;
            targetUser.GoogleSubjectId = null;
            targetUser.FullName = "Người dùng đã xóa";
            targetUser.Email = $"deleted_{targetUser.UserId}@deleted.cinemahub.vn";
            targetUser.EmailHash = $"deleted_{targetUser.UserId}_{Guid.NewGuid():N}";

            if (targetUser.PasswordResetTokens.Any())
            {
                _context.PasswordResetTokens.RemoveRange(targetUser.PasswordResetTokens);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Ghi nhật ký kiểm toán bất biến (Audit Log) vào cơ sở dữ liệu
            _context.AuditLogs.Add(new CinemaManagement.Models.AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUsername = User.Identity?.Name,
                ActorUserId = currentId,
                ActorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                Action = "SOFT_DELETE_USER",
                EntityName = "User",
                EntityId = targetUser.UserId.ToString(),
                Details = $"Xóa mềm tài khoản '{targetUser.Username}' (Id={targetUser.UserId}). Đã ẩn danh hóa PII và bảo lưu {ticketCount} vé lịch sử giao dịch.",
                IpAddress = ip,
                Path = "/Admin/Users/DeleteUser"
            });

            await _context.SaveChangesAsync();

            _cache.Remove($"auth_stamp_{targetUser.UserId}");

            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.High,
                "ADMIN_USER_SOFT_DELETED",
                $"Tài khoản '{targetUser.Username}' đã được xóa mềm và ẩn danh hóa",
                $"Thực hiện bởi Admin '{User.Identity?.Name}'. Đã bảo lưu toàn vẹn {ticketCount} vé giao dịch phục vụ đối soát.",
                IpAddress: ip,
                Username: User.Identity?.Name,
                Path: "/Admin/Users/DeleteUser"));

            TempData["Success"] = $"Đã xóa mềm và ẩn danh hoá tài khoản '{targetUser.Username}' thành công. Lịch sử giao dịch được bảo lưu an toàn.";
            return RedirectToAction(nameof(Index));
        }
    }
}
