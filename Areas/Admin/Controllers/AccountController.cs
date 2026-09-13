using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [EnableRateLimiting("AuthLimit")]
    public class AccountController : Controller
    {
        private readonly CinemaDbContext _context;
        private readonly ILoginAttemptService _loginAttemptService;

        public AccountController(CinemaDbContext context, ILoginAttemptService loginAttemptService)
        {
            _context = context;
            _loginAttemptService = loginAttemptService;
        }

        // GET: /Admin/Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true
                && (User.IsInRole("Admin") || User.IsInRole("Staff")))
                return RedirectToAction("Index", "Dashboard");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Admin/Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var lockoutKey = $"admin_{model.Username}_{ip}";

            if (_loginAttemptService.IsLockedOut(lockoutKey, out var remainingTime))
            {
                var minutes = Math.Max(1, (int)Math.Ceiling(remainingTime.TotalMinutes));
                ModelState.AddModelError("", $"Khu vực quản trị bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {minutes} phút.");
                return View(model);
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == model.Username && u.Status == "Active");

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                _loginAttemptService.RecordFailedAttempt(lockoutKey);
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }

            // Chỉ cho phép Admin hoặc Staff đăng nhập vào trang Admin
            if (user.Role.RoleName != "Admin" && user.Role.RoleName != "Staff")
            {
                _loginAttemptService.RecordFailedAttempt(lockoutKey);
                ModelState.AddModelError("", "Bạn không có quyền truy cập trang quản trị.");
                return View(model);
            }

            // Đăng nhập thành công -> Xóa lockout
            _loginAttemptService.ResetAttempts(lockoutKey);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role.RoleName),
                new("FullName", user.FullName ?? user.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        // POST: /Admin/Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
