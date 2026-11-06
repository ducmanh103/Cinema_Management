using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CinemaManagement.Data;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CinemaManagement.Controllers
{
    [EnableRateLimiting("AuthLimit")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class AccountController : Controller
    {
        // Dummy hash dùng để cân bằng thời gian thực thi BCrypt khi username không tồn tại (chống Timing Attack)
        private const string DummyBcryptHash = "$2a$11$V.Co51b1VTU8zRBPdCcMUeYFNuCBgiTGoyngz5kPfWe3iyFC1AcKq";

        private readonly CinemaDbContext _context;
        private readonly ILoginAttemptService _loginAttemptService;
        private readonly IEmailService _emailService;
        private readonly ISecurityAlertService _securityAlert;
        private readonly IConfiguration _config;

        public AccountController(
            CinemaDbContext context,
            ILoginAttemptService loginAttemptService,
            IEmailService emailService,
            ISecurityAlertService securityAlert,
            IConfiguration config)
        {
            _context = context;
            _loginAttemptService = loginAttemptService;
            _emailService = emailService;
            _securityAlert = securityAlert;
            _config = config;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var normalizedUsername = model.Username.Trim().ToLowerInvariant();
            var lockoutKey = $"user_{normalizedUsername}_{ip}";
            var accountLockoutKey = $"account_{normalizedUsername}";

            if (_loginAttemptService.IsLockedOut(lockoutKey, out var remainingTime)
                || _loginAttemptService.IsLockedOut(accountLockoutKey, out remainingTime))
            {
                var minutes = Math.Max(1, (int)Math.Ceiling(remainingTime.TotalMinutes));
                ModelState.AddModelError("", $"Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau {minutes} phút.");
                return View(model);
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername && u.Status == "Active");

            // Jika tài khoản tạo bằng Google và chưa đặt mật khẩu nội bộ
            if (user != null && string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "Tài khoản này đăng nhập bằng Google. Vui lòng bấm 'Đăng nhập với Google' hoặc chọn 'Quên mật khẩu' để thiết lập mật khẩu.");
                return View(model);
            }

            string hashToVerify = user?.PasswordHash ?? DummyBcryptHash;
            bool passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, hashToVerify) && user != null;

            if (!passwordValid || user == null)
            {
                _loginAttemptService.RecordFailedAttempt(lockoutKey);
                _loginAttemptService.RecordFailedAttempt(accountLockoutKey);

                if (_loginAttemptService.IsLockedOut(lockoutKey, out _))
                {
                    _securityAlert.EnqueueAlert(new SecurityAlert(
                        SecurityAlertLevel.High,
                        "USER_BRUTE_FORCE_LOCKOUT",
                        "Tài khoản người dùng bị tạm khoá do đăng nhập sai quá 5 lần",
                        $"Username='{model.Username}' đã bị khoá tạm thời 15 phút.",
                        IpAddress: ip,
                        Username: model.Username,
                        Path: "/Account/Login"));
                }

                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }

            _loginAttemptService.ResetAttempts(lockoutKey);
            _loginAttemptService.ResetAttempts(accountLockoutKey);

            await SignInUserAsync(user, model.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/GoogleLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var clientId = _config["Authentication:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
                clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");

            if (string.IsNullOrWhiteSpace(clientId) || clientId.StartsWith("unconfigured-", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Đăng nhập Google chưa được cấu hình (thiếu GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET).";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var redirectUrl = Url.Action(nameof(GoogleResponse), "Account", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // GET: /Account/GoogleResponse
        [HttpGet]
        public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync("ExternalTempCookie");
            if (!result.Succeeded || result.Principal == null)
            {
                TempData["Error"] = "Xác thực với Google không thành công hoặc đã bị huỷ.";
                return RedirectToAction(nameof(Login));
            }

            var googleSubjectId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value?.Trim().ToLowerInvariant();
            var fullName = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            await HttpContext.SignOutAsync("ExternalTempCookie");

            if (string.IsNullOrEmpty(googleSubjectId) || string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Không lấy được thông tin Email từ tài khoản Google.";
                return RedirectToAction(nameof(Login));
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId || (u.Email != null && u.Email.ToLower() == email));

            if (user != null)
            {
                if (user.Status != "Active")
                {
                    TempData["Error"] = "Tài khoản của bạn hiện đang bị khoá.";
                    return RedirectToAction(nameof(Login));
                }

                bool updated = false;
                if (string.IsNullOrEmpty(user.GoogleSubjectId))
                {
                    user.GoogleSubjectId = googleSubjectId;
                    updated = true;
                }
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    updated = true;
                }
                if (updated) await _context.SaveChangesAsync();
            }
            else
            {
                var customerRole = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleName == "Customer");
                if (customerRole == null)
                {
                    TempData["Error"] = "Lỗi hệ thống: vai trò Customer chưa được khởi tạo.";
                    return RedirectToAction(nameof(Login));
                }

                string baseUsername = email.Split('@')[0];
                baseUsername = new string(baseUsername.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.').ToArray());
                if (baseUsername.Length < 3) baseUsername = "user_" + baseUsername;
                if (baseUsername.Length > 40) baseUsername = baseUsername[..40];

                string candidateUsername = baseUsername;
                int suffix = 1;
                while (await _context.Users.AnyAsync(u => u.Username.ToLower() == candidateUsername.ToLower()))
                {
                    candidateUsername = $"{baseUsername}{suffix++}";
                }

                user = new User
                {
                    Username = candidateUsername,
                    PasswordHash = null,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? candidateUsername : fullName,
                    Email = email,
                    GoogleSubjectId = googleSubjectId,
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    Status = "Active",
                    RoleId = customerRole.RoleId
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                user.Role = customerRole;
            }

            await SignInUserAsync(user, isPersistent: true);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register() => View();

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var usernameLower = model.Username.Trim().ToLowerInvariant();
            var emailLower = model.Email?.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
                return View(model);
            }

            if (!string.IsNullOrEmpty(emailLower) && await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == emailLower))
            {
                ModelState.AddModelError("Email", "Email đã được sử dụng.");
                return View(model);
            }

            var customerRole = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleName == "Customer");
            if (customerRole == null) return BadRequest("Lỗi hệ thống: vai trò Customer chưa được khởi tạo.");

            var user = new User
            {
                Username = model.Username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FullName = model.FullName?.Trim(),
                Email = emailLower,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Status = "Active",
                RoleId = customerRole.RoleId
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var emailLower = model.Email.Trim().ToLowerInvariant();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == emailLower && u.Status == "Active");

            if (user != null)
            {
                var windowStart = VietnamTime.Now.AddMinutes(-15);
                int recentRequests = await _context.PasswordResetTokens
                    .CountAsync(t => t.UserId == user.UserId && t.CreatedAt >= windowStart);

                if (recentRequests < 3)
                {
                    var rawBytes = RandomNumberGenerator.GetBytes(32);
                    var rawToken = WebEncoders.Base64UrlEncode(rawBytes);
                    var tokenHash = ComputeSha256Hex(rawToken);

                    var resetEntry = new PasswordResetToken
                    {
                        UserId = user.UserId,
                        TokenHash = tokenHash,
                        CreatedAt = VietnamTime.UtcNow,
                        ExpiresAt = VietnamTime.UtcNow.AddMinutes(15),
                        RequestIp = ip
                    };

                    _context.PasswordResetTokens.Add(resetEntry);
                    await _context.SaveChangesAsync();

                    var resetUrl = Url.Action(
                        nameof(ResetPassword),
                        "Account",
                        new { email = user.Email, token = rawToken },
                        Request.Scheme) ?? string.Empty;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendPasswordResetEmailAsync(
                                user.Email!, user.FullName ?? user.Username, resetUrl);
                        }
                        catch
                        {
                            // Đã được log bên trong EmailService
                        }
                    });
                }
            }

            // Chống dò tài khoản (Anti-Enumeration): luôn hiện cùng thông báo
            TempData["Success"] = "Nếu địa chỉ email tồn tại trong hệ thống, liên kết đặt lại mật khẩu (hiệu lực 15 phút) đã được gửi tới hộp thư của bạn.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var emailLower = model.Email.Trim().ToLowerInvariant();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == emailLower && u.Status == "Active");

            if (user == null)
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var tokenHash = ComputeSha256Hex(model.Token.Trim());
            var now = VietnamTime.Now;

            var validToken = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > now)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (validToken == null)
            {
                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.Medium,
                    "PASSWORD_RESET_INVALID_TOKEN",
                    "Sử dụng token đặt lại mật khẩu không hợp lệ hoặc đã hết hạn",
                    $"Email='{emailLower}'",
                    IpAddress: ip,
                    Username: user.Username,
                    Path: "/Account/ResetPassword"));

                TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn (quá 15 phút).";
                return RedirectToAction(nameof(ForgotPassword));
            }

            // Cập nhật mật khẩu + xoay SecurityStamp để thu hồi mọi phiên đăng nhập cũ
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            validToken.UsedAt = VietnamTime.UtcNow;

            await _context.SaveChangesAsync();

            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Info,
                "PASSWORD_RESET_SUCCESS",
                "Người dùng đã đặt lại mật khẩu thành công",
                $"Tài khoản '{user.Username}' ({user.Email}) vừa hoàn tất đặt lại mật khẩu.",
                IpAddress: ip,
                Username: user.Username,
                Path: "/Account/ResetPassword"));

            TempData["Success"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            return RedirectToAction(nameof(Login));
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        public IActionResult AccessDenied() => View();

        private async Task SignInUserAsync(User user, bool isPersistent)
        {
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
                new AuthenticationProperties { IsPersistent = isPersistent });
        }

        private static string ComputeSha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
