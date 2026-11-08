using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

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
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;

        public AccountController(
            CinemaDbContext context,
            ILoginAttemptService loginAttemptService,
            ISecurityAlertService securityAlert,
            IEmailService emailService,
            IMemoryCache cache)
        {
            _context = context;
            _loginAttemptService = loginAttemptService;
            _securityAlert = securityAlert;
            _emailService = emailService;
            _cache = cache;
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

            // Mật khẩu chính xác -> Reset bộ đếm thử sai
            _loginAttemptService.ResetAttempts(lockoutKey);
            _loginAttemptService.ResetAttempts(globalLockoutKey);

            // ==========================================
            // KÍCH HOẠT BẢO MẬT 2 LỚP (MFA) CHO ADMIN
            // ==========================================
            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            var otpHash = ComputeSha256Hex(otpCode);
            var mfaSessionId = Guid.NewGuid().ToString("N");

            var mfaSession = new AdminMfaSession
            {
                UserId = user.UserId,
                Username = user.Username,
                RoleName = user.Role.RoleName,
                FullName = user.FullName ?? user.Username,
                SecurityStamp = user.SecurityStamp ?? string.Empty,
                Email = user.Email ?? "admin@cinemahub.vn",
                RememberMe = model.RememberMe,
                ReturnUrl = returnUrl,
                OtpHash = otpHash,
                AttemptsLeft = 5,
                CreatedAtUtc = DateTime.UtcNow,
                LastResentAtUtc = DateTime.UtcNow
            };

            // Lưu phiên xác thực tạm thời vào cache (hết hạn sau 5 phút)
            _cache.Set($"admin_mfa_{mfaSessionId}", mfaSession, TimeSpan.FromMinutes(5));
            HttpContext.Session.SetString("AdminMfaSessionId", mfaSessionId);

            // Gửi OTP qua Email
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendAdminMfaOtpEmailAsync(mfaSession.Email, mfaSession.FullName, otpCode, ip);
                }
                catch { }
            });

            // Gửi OTP tức thời qua Telegram Bot
            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Critical,
                "ADMIN_MFA_OTP_ISSUED",
                $"🔐 Mã xác thực MFA Quản trị: {otpCode}",
                $"Tài khoản '{user.Username}' vừa đăng nhập bước 1 từ IP {ip}. Mã OTP có hiệu lực trong 5 phút.",
                IpAddress: ip,
                Username: user.Username,
                Path: "/Admin/Account/VerifyMfa"));

            return RedirectToAction(nameof(VerifyMfa), new { returnUrl });
        }

        // GET: /Admin/Account/VerifyMfa
        [HttpGet]
        public IActionResult VerifyMfa(string? returnUrl = null)
        {
            var mfaSessionId = HttpContext.Session.GetString("AdminMfaSessionId");
            if (string.IsNullOrEmpty(mfaSessionId) || !_cache.TryGetValue($"admin_mfa_{mfaSessionId}", out AdminMfaSession? session) || session == null)
            {
                TempData["Error"] = "Phiên xác thực bảo mật đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction(nameof(Login));
            }

            var model = new VerifyAdminMfaViewModel
            {
                ReturnUrl = returnUrl ?? session.ReturnUrl,
                MaskedEmail = MaskEmail(session.Email)
            };

            return View(model);
        }

        // POST: /Admin/Account/VerifyMfa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyMfa(VerifyAdminMfaViewModel model)
        {
            var mfaSessionId = HttpContext.Session.GetString("AdminMfaSessionId");
            if (string.IsNullOrEmpty(mfaSessionId) || !_cache.TryGetValue($"admin_mfa_{mfaSessionId}", out AdminMfaSession? session) || session == null)
            {
                TempData["Error"] = "Phiên xác thực bảo mật đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction(nameof(Login));
            }

            model.MaskedEmail = MaskEmail(session.Email);
            if (!ModelState.IsValid) return View(model);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var inputOtpHash = ComputeSha256Hex(model.OtpCode.Trim());

            bool isValidOtp = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(inputOtpHash),
                Encoding.UTF8.GetBytes(session.OtpHash));

            if (!isValidOtp)
            {
                session.AttemptsLeft--;
                if (session.AttemptsLeft <= 0)
                {
                    _cache.Remove($"admin_mfa_{mfaSessionId}");
                    HttpContext.Session.Remove("AdminMfaSessionId");

                    _securityAlert.EnqueueAlert(new SecurityAlert(
                        SecurityAlertLevel.High,
                        "ADMIN_MFA_FAILED_LOCKOUT",
                        "Xác thực MFA thất bại quá 5 lần",
                        $"Tài khoản '{session.Username}' nhập sai mã OTP quá 5 lần từ IP {ip}.",
                        IpAddress: ip,
                        Username: session.Username,
                        Path: "/Admin/Account/VerifyMfa"));

                    TempData["Error"] = "Bạn đã nhập sai mã xác thực quá 5 lần. Phiên đăng nhập đã bị hủy để đảm bảo an toàn.";
                    return RedirectToAction(nameof(Login));
                }

                _cache.Set($"admin_mfa_{mfaSessionId}", session, TimeSpan.FromMinutes(5));
                ModelState.AddModelError("OtpCode", $"Mã OTP không chính xác. Bạn còn {session.AttemptsLeft} lần thử.");
                return View(model);
            }

            // Xác thực MFA thành công! Hủy phiên MFA tạm thời
            _cache.Remove($"admin_mfa_{mfaSessionId}");
            HttpContext.Session.Remove("AdminMfaSessionId");

            // Thiết lập Cookie phiên làm việc chính thức
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new(ClaimTypes.Name, session.Username),
                new(ClaimTypes.Role, session.RoleName),
                new("FullName", session.FullName),
                new("SecurityStamp", session.SecurityStamp)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = session.RememberMe });

            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Info,
                "ADMIN_LOGIN_SUCCESS",
                "Đăng nhập thành công vào khu vực Quản trị (MFA đã xác thực)",
                $"Quản trị viên '{session.Username}' (Role={session.RoleName}) đã vượt qua MFA từ IP {ip}.",
                IpAddress: ip,
                Username: session.Username,
                Path: "/Admin/Account/VerifyMfa"));

            var targetUrl = model.ReturnUrl ?? session.ReturnUrl;
            if (!string.IsNullOrEmpty(targetUrl) && Url.IsLocalUrl(targetUrl))
                return Redirect(targetUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        // POST: /Admin/Account/ResendMfaOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendMfaOtp()
        {
            var mfaSessionId = HttpContext.Session.GetString("AdminMfaSessionId");
            if (string.IsNullOrEmpty(mfaSessionId) || !_cache.TryGetValue($"admin_mfa_{mfaSessionId}", out AdminMfaSession? session) || session == null)
            {
                TempData["Error"] = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction(nameof(Login));
            }

            var now = DateTime.UtcNow;
            if ((now - session.LastResentAtUtc).TotalSeconds < 45)
            {
                TempData["Error"] = "Vui lòng chờ ít nhất 45 giây trước khi yêu cầu gửi lại mã.";
                return RedirectToAction(nameof(VerifyMfa));
            }

            var newOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            session.OtpHash = ComputeSha256Hex(newOtp);
            session.LastResentAtUtc = now;
            session.AttemptsLeft = 5;

            _cache.Set($"admin_mfa_{mfaSessionId}", session, TimeSpan.FromMinutes(5));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Gửi email mới
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendAdminMfaOtpEmailAsync(session.Email, session.FullName, newOtp, ip);
                }
                catch { }
            });

            // Gửi Telegram mới
            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.High,
                "ADMIN_MFA_OTP_RESENT",
                $"🔐 Mã xác thực MFA gửi lại: {newOtp}",
                $"Tài khoản '{session.Username}' yêu cầu gửi lại mã OTP từ IP {ip}.",
                IpAddress: ip,
                Username: session.Username,
                Path: "/Admin/Account/ResendMfaOtp"));

            TempData["Success"] = "Mã xác thực mới đã được gửi tới Email và Telegram Bot của bạn.";
            return RedirectToAction(nameof(VerifyMfa));
        }

        // GET & POST: /Admin/Account/Logout (Hỗ trợ cả POST và GET trực tiếp qua URL hoặc reload)
        [AcceptVerbs("GET", "POST")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return "quản trị viên";

            var parts = email.Split('@');
            var name = parts[0];
            var domain = parts[1];

            if (name.Length <= 2)
                return $"{name[0]}*@{domain}";

            return $"{name[0]}***{name[^1]}@{domain}";
        }

        private static string ComputeSha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
