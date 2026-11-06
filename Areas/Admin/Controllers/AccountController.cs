using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [EnableRateLimiting("AuthLimit")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class AccountController : Controller
    {
        private const string DummyBcryptHash = "$2a$11$V.Co51b1VTU8zRBPdCcMUeYFNuCBgiTGoyngz5kPfWe3iyFC1AcKq";

        private readonly CinemaDbContext _context;
        private readonly ILoginAttemptService _loginAttemptService;
        private readonly ISecurityAlertService _securityAlert;

        public AccountController(
            CinemaDbContext context,
            ILoginAttemptService loginAttemptService,
            ISecurityAlertService securityAlert)
        {
            _context = context;
            _loginAttemptService = loginAttemptService;
            _securityAlert = securityAlert;
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
            var normalizedUsername = model.Username.Trim().ToLowerInvariant();
            var lockoutKey = $"admin_{normalizedUsername}_{ip}";
            var globalLockoutKey = $"admin_account_{normalizedUsername}";

            if (_loginAttemptService.IsLockedOut(lockoutKey, out var remainingTime)
                || _loginAttemptService.IsLockedOut(globalLockoutKey, out remainingTime))
            {
                var minutes = Math.Max(1, (int)Math.Ceiling(remainingTime.TotalMinutes));
                ModelState.AddModelError("", $"Khu vực quản trị bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {minutes} phút.");
                return View(model);
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername && u.Status == "Active");

            string hashToVerify = !string.IsNullOrEmpty(user?.PasswordHash) ? user!.PasswordHash! : DummyBcryptHash;
            bool validPassword = BCrypt.Net.BCrypt.Verify(model.Password, hashToVerify) && user != null && !string.IsNullOrEmpty(user.PasswordHash);

            if (!validPassword || user == null)
            {
                _loginAttemptService.RecordFailedAttempt(lockoutKey);
                _loginAttemptService.RecordFailedAttempt(globalLockoutKey);

                if (_loginAttemptService.IsLockedOut(lockoutKey, out _))
                {
                    _securityAlert.EnqueueAlert(new SecurityAlert(
                        SecurityAlertLevel.High,
                        "ADMIN_BRUTE_FORCE_LOCKOUT",
                        "Cổng Admin bị khoá tạm thời do đăng nhập sai quá 5 lần",
                        $"Tài khoản thử nghiệm: '{model.Username}'",
                        IpAddress: ip,
                        Username: model.Username,
                        Path: "/Admin/Account/Login"));
                }

                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }

            if (user.Role.RoleName != "Admin" && user.Role.RoleName != "Staff")
            {
                _loginAttemptService.RecordFailedAttempt(lockoutKey);
                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.Medium,
                    "ADMIN_UNAUTHORIZED_LOGIN_ATTEMPT",
                    "Tài khoản Customer cố gắng đăng nhập vào cổng Quản trị",
                    $"Tài khoản '{user.Username}' (Role={user.Role.RoleName}) vừa thử đăng nhập Admin.",
                    IpAddress: ip,
                    Username: user.Username,
                    Path: "/Admin/Account/Login"));

                ModelState.AddModelError("", "Bạn không có quyền truy cập trang quản trị.");
                return View(model);
            }

            _loginAttemptService.ResetAttempts(lockoutKey);
            _loginAttemptService.ResetAttempts(globalLockoutKey);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role.RoleName),
                new("FullName", user.FullName ?? user.Username),
                new("SecurityStamp", user.SecurityStamp ?? string.Empty)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Info,
                "ADMIN_LOGIN_SUCCESS",
                "Đăng nhập thành công vào khu vực Quản trị (Admin Area)",
                $"Quản trị viên '{user.Username}' (Role={user.Role.RoleName}) đã đăng nhập.",
                IpAddress: ip,
                Username: user.Username,
                Path: "/Admin/Account/Login"));

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
