using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaManagement.Data;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPiiEncryptionService _piiService;

        public AccountController(
            CinemaDbContext context,
            ILoginAttemptService loginAttemptService,
            IEmailService emailService,
            ISecurityAlertService securityAlert,
            IConfiguration config,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory,
            IPiiEncryptionService piiService)
        {
            _context = context;
            _loginAttemptService = loginAttemptService;
            _emailService = emailService;
            _securityAlert = securityAlert;
            _config = config;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _piiService = piiService;
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

            var emailHash = _piiService.ComputeEmailHash(normalizedUsername);
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u =>
                    (u.Username.ToLower() == normalizedUsername || (u.EmailHash != null && u.EmailHash == emailHash))
                    && u.Status == "Active");

            // Nếu tài khoản tạo bằng Google (không đăng ký mật khẩu riêng)
            if (user != null && string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "Tài khoản này đăng nhập bằng Google (không sử dụng mật khẩu riêng). Vui lòng bấm 'Đăng nhập với Google' bên dưới.");
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

            // ==========================================
            // KÍCH HOẠT BẢO MẬT 2 LỚP (MFA) CHO ADMIN/STAFF
            // ==========================================
            if (user.Role != null && (user.Role.RoleName == "Admin" || user.Role.RoleName == "Staff"))
            {
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
                    $"Tài khoản '{user.Username}' vừa đăng nhập bước 1 từ IP {ip} (qua /Account/Login). Mã OTP có hiệu lực trong 5 phút.",
                    IpAddress: ip,
                    Username: user.Username,
                    Path: "/Account/Login"));

                return RedirectToAction(nameof(VerifyMfa), new { returnUrl });
            }

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

            var emailHash = _piiService.ComputeEmailHash(email);
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId || (u.EmailHash != null && u.EmailHash == emailHash));

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
                    EmailHash = emailHash,
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

            // ==========================================
            // KÍCH HOẠT MFA CHO ADMIN/STAFF KHI GOOGLE LOGIN
            // ==========================================
            if (user.Role != null && (user.Role.RoleName == "Admin" || user.Role.RoleName == "Staff"))
            {
                var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
                var otpHash = ComputeSha256Hex(otpCode);
                var mfaSessionId = Guid.NewGuid().ToString("N");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                var mfaSession = new AdminMfaSession
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    RoleName = user.Role.RoleName,
                    FullName = user.FullName ?? user.Username,
                    SecurityStamp = user.SecurityStamp ?? string.Empty,
                    Email = user.Email ?? email,
                    RememberMe = true,
                    ReturnUrl = returnUrl,
                    OtpHash = otpHash,
                    AttemptsLeft = 5,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastResentAtUtc = DateTime.UtcNow
                };

                _cache.Set($"admin_mfa_{mfaSessionId}", mfaSession, TimeSpan.FromMinutes(5));
                HttpContext.Session.SetString("AdminMfaSessionId", mfaSessionId);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendAdminMfaOtpEmailAsync(mfaSession.Email, mfaSession.FullName, otpCode, ip);
                    }
                    catch { }
                });

                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.Critical,
                    "ADMIN_MFA_OTP_ISSUED",
                    $"🔐 Mã xác thực MFA Quản trị: {otpCode}",
                    $"Tài khoản '{user.Username}' vừa đăng nhập Google từ IP {ip}. Mã OTP có hiệu lực trong 5 phút.",
                    IpAddress: ip,
                    Username: user.Username,
                    Path: "/Account/GoogleResponse"));

                return RedirectToAction(nameof(VerifyMfa), new { returnUrl });
            }

            await SignInUserAsync(user, isPersistent: true);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/VerifyMfa
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

        // POST: /Account/VerifyMfa
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
                        Path: "/Account/VerifyMfa"));

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
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == session.UserId && u.Status == "Active");

            if (user == null)
            {
                TempData["Error"] = "Tài khoản không tồn tại hoặc đã bị khóa.";
                return RedirectToAction(nameof(Login));
            }

            await SignInUserAsync(user, session.RememberMe);

            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Info,
                "ADMIN_LOGIN_SUCCESS",
                "Đăng nhập thành công vào hệ thống (MFA đã xác thực)",
                $"Quản trị viên '{session.Username}' (Role={session.RoleName}) đã vượt qua MFA từ IP {ip}.",
                IpAddress: ip,
                Username: session.Username,
                Path: "/Account/VerifyMfa"));

            var targetUrl = model.ReturnUrl ?? session.ReturnUrl;
            if (!string.IsNullOrEmpty(targetUrl) && Url.IsLocalUrl(targetUrl))
                return Redirect(targetUrl);

            if (session.RoleName == "Admin" || session.RoleName == "Staff")
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/ResendMfaOtp
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
            _cache.Set($"admin_mfa_{mfaSessionId}", session, TimeSpan.FromMinutes(5));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendAdminMfaOtpEmailAsync(session.Email, session.FullName, newOtp, ip);
                }
                catch { }
            });

            _securityAlert.EnqueueAlert(new SecurityAlert(
                SecurityAlertLevel.Critical,
                "ADMIN_MFA_OTP_RESENT",
                $"🔄 Cấp lại mã xác thực MFA: {newOtp}",
                $"Tài khoản '{session.Username}' vừa yêu cầu gửi lại OTP từ IP {ip}.",
                IpAddress: ip,
                Username: session.Username,
                Path: "/Account/ResendMfaOtp"));

            TempData["Success"] = "Mã xác thực mới đã được gửi lại qua Email & Telegram.";
            return RedirectToAction(nameof(VerifyMfa));
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.RecaptchaSiteKey = GetRecaptchaSiteKey();
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            ViewBag.RecaptchaSiteKey = GetRecaptchaSiteKey();

            var (recaptchaValid, recaptchaError) = await VerifyRecaptchaAsync();
            if (!recaptchaValid)
            {
                ModelState.AddModelError("", recaptchaError!);
                return View(model);
            }

            if (!ModelState.IsValid) return View(model);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ipOtpRateKey = $"reg_otp_ip_{ip}";
            var ipOtpCount = _cache.Get<int?>(ipOtpRateKey) ?? 0;
            if (ipOtpCount >= 5)
            {
                ModelState.AddModelError("", "Thiết bị của bạn đã yêu cầu gửi mã xác thực quá nhiều lần. Vui lòng thử lại sau 15 phút.");
                return View(model);
            }

            var usernameTrimmed = model.Username.Trim();
            var usernameLower = usernameTrimmed.ToLowerInvariant();
            var emailLower = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;

            if (string.IsNullOrEmpty(emailLower) || !emailLower.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Email", "Bắt buộc sử dụng địa chỉ Gmail hợp lệ (@gmail.com) để đăng ký tài khoản.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
                return View(model);
            }

            var emailHash = _piiService.ComputeEmailHash(emailLower);
            if (await _context.Users.AnyAsync(u => u.EmailHash != null && u.EmailHash == emailHash))
            {
                ModelState.AddModelError("Email", "Địa chỉ Gmail này đã được sử dụng.");
                return View(model);
            }

            var userReservationKey = $"pending_reg_user_{usernameLower}";
            if (_cache.TryGetValue<string>(userReservationKey, out var reservedByEmail)
                && !string.IsNullOrEmpty(reservedByEmail)
                && !string.Equals(reservedByEmail, emailLower, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập này đang chờ xác thực bởi một người dùng khác. Vui lòng chọn tên khác.");
                return View(model);
            }

            var customerRole = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleName == "Customer");
            if (customerRole == null) return BadRequest("Lỗi hệ thống: vai trò Customer chưa được khởi tạo.");

            var pendingCacheKey = $"pending_reg_email_{emailLower}";
            var nowUtc = VietnamTime.UtcNow;

            if (_cache.TryGetValue<PendingRegistrationEntry>(pendingCacheKey, out var existingPending) && existingPending != null)
            {
                if (existingPending.ResendCount >= 5 && existingPending.ExpiresAt > nowUtc)
                {
                    ModelState.AddModelError("", "Bạn đã yêu cầu gửi mã xác thực quá nhiều lần. Vui lòng thử lại sau 15 phút.");
                    return View(model);
                }
            }

            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var otpHash = ComputeSha256Hex(otpCode);
            var tokenHash = ComputeSha256Hex(rawToken);

            var verifyUrl = Url.Action(
                nameof(ConfirmRegistrationEmail),
                "Account",
                new { email = emailLower, token = rawToken },
                Request.Scheme) ?? string.Empty;

            bool emailSent = await _emailService.SendRegistrationVerificationEmailAsync(
                emailLower,
                model.FullName?.Trim() ?? usernameTrimmed,
                otpCode,
                verifyUrl);

            if (!emailSent)
            {
                ModelState.AddModelError("", "Không thể gửi email xác thực tới địa chỉ Gmail này. Vui lòng kiểm tra lại địa chỉ Gmail hoặc cấu hình SMTP của hệ thống.");
                return View(model);
            }

            var pendingEntry = new PendingRegistrationEntry
            {
                Username = usernameTrimmed,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FullName = model.FullName?.Trim(),
                Email = emailLower,
                RoleId = customerRole.RoleId,
                OtpHash = otpHash,
                TokenHash = tokenHash,
                ExpiresAt = nowUtc.AddMinutes(15),
                LastSentAt = nowUtc,
                ResendCount = (existingPending?.ResendCount ?? 0) + 1,
                FailedAttempts = 0
            };

            _cache.Set(pendingCacheKey, pendingEntry, TimeSpan.FromMinutes(15));
            _cache.Set(userReservationKey, emailLower, TimeSpan.FromMinutes(15));
            _cache.Set(ipOtpRateKey, ipOtpCount + 1, TimeSpan.FromMinutes(15));

            TempData["Success"] = $"Đã gửi mã xác thực 6 chữ số tới Gmail {emailLower}. Vui lòng xác thực để hoàn tất tạo tài khoản!";
            return RedirectToAction(nameof(VerifyRegistrationEmail), new { email = emailLower });
        }

        // GET: /Account/VerifyRegistrationEmail
        [HttpGet]
        public async Task<IActionResult> VerifyRegistrationEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(Register));

            var emailLower = email.Trim().ToLowerInvariant();

            if (await _context.Users.AsNoTracking().AnyAsync(u => u.Email != null && u.Email.ToLower() == emailLower))
            {
                TempData["Success"] = "Tài khoản Gmail này đã được xác thực và tạo thành công! Vui lòng đăng nhập.";
                return RedirectToAction(nameof(Login));
            }

            var pendingCacheKey = $"pending_reg_email_{emailLower}";
            if (!_cache.TryGetValue<PendingRegistrationEntry>(pendingCacheKey, out var pending)
                || pending == null
                || pending.ExpiresAt <= VietnamTime.UtcNow)
            {
                TempData["Error"] = "Phiên xác thực Gmail đã hết hạn (quá 15 phút) hoặc không tồn tại. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            return View(new VerifyRegistrationEmailViewModel { Email = emailLower });
        }

        // POST: /Account/VerifyRegistrationEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyRegistrationEmail(VerifyRegistrationEmailViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var emailLower = model.Email.Trim().ToLowerInvariant();
            var pendingCacheKey = $"pending_reg_email_{emailLower}";

            if (!_cache.TryGetValue<PendingRegistrationEntry>(pendingCacheKey, out var pending)
                || pending == null
                || pending.ExpiresAt <= VietnamTime.UtcNow)
            {
                TempData["Error"] = "Mã xác thực đã hết hạn (quá 15 phút). Vui lòng thực hiện đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            if (pending.FailedAttempts >= 5)
            {
                _cache.Remove(pendingCacheKey);
                _cache.Remove($"pending_reg_user_{pending.Username.ToLowerInvariant()}");
                TempData["Error"] = "Bạn đã nhập sai mã xác thực quá 5 lần. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            var inputOtpHash = ComputeSha256Hex(model.OtpCode.Trim());
            bool otpMatches = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(inputOtpHash),
                Encoding.UTF8.GetBytes(pending.OtpHash));

            if (!otpMatches)
            {
                pending.FailedAttempts++;
                _cache.Set(pendingCacheKey, pending, TimeSpan.FromMinutes(15));
                int remaining = Math.Max(0, 5 - pending.FailedAttempts);
                ModelState.AddModelError("OtpCode", $"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
                return View(model);
            }

            var (created, errorMsg) = await CompletePendingRegistrationAsync(pending);
            if (!created)
            {
                TempData["Error"] = errorMsg ?? "Không thể hoàn tất tạo tài khoản. Vui lòng thử lại.";
                return RedirectToAction(nameof(Register));
            }

            TempData["Success"] = "Xác thực Gmail thành công! Tài khoản đã được tạo, vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/ConfirmRegistrationEmail
        [HttpGet]
        public async Task<IActionResult> ConfirmRegistrationEmail(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Liên kết xác thực Gmail không hợp lệ.";
                return RedirectToAction(nameof(Register));
            }

            var emailLower = email.Trim().ToLowerInvariant();

            if (await _context.Users.AsNoTracking().AnyAsync(u => u.Email != null && u.Email.ToLower() == emailLower))
            {
                TempData["Success"] = "Tài khoản của bạn đã được xác thực và khởi tạo thành công! Vui lòng đăng nhập.";
                return RedirectToAction(nameof(Login));
            }

            var pendingCacheKey = $"pending_reg_email_{emailLower}";
            if (!_cache.TryGetValue<PendingRegistrationEntry>(pendingCacheKey, out var pending)
                || pending == null
                || pending.ExpiresAt <= VietnamTime.UtcNow)
            {
                TempData["Error"] = "Liên kết xác thực Gmail đã hết hạn (quá 15 phút) hoặc không tồn tại. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            var inputTokenHash = ComputeSha256Hex(token.Trim());
            bool tokenMatches = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(inputTokenHash),
                Encoding.UTF8.GetBytes(pending.TokenHash));

            if (!tokenMatches)
            {
                TempData["Error"] = "Liên kết xác thực Gmail không hợp lệ hoặc đã được thay thế bởi mã mới.";
                return RedirectToAction(nameof(VerifyRegistrationEmail), new { email = emailLower });
            }

            var (created, errorMsg) = await CompletePendingRegistrationAsync(pending);
            if (!created)
            {
                TempData["Error"] = errorMsg ?? "Không thể hoàn tất tạo tài khoản. Vui lòng thử lại.";
                return RedirectToAction(nameof(Register));
            }

            TempData["Success"] = "Xác thực Gmail thành công! Tài khoản đã được tạo, vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        // POST: /Account/ResendRegistrationOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegistrationOtp(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(Register));

            var emailLower = email.Trim().ToLowerInvariant();
            var pendingCacheKey = $"pending_reg_email_{emailLower}";
            var nowUtc = VietnamTime.UtcNow;

            if (!_cache.TryGetValue<PendingRegistrationEntry>(pendingCacheKey, out var pending)
                || pending == null
                || pending.ExpiresAt <= nowUtc)
            {
                TempData["Error"] = "Phiên xác thực đã hết hạn. Vui lòng điền lại thông tin đăng ký.";
                return RedirectToAction(nameof(Register));
            }

            if (pending.ResendCount >= 5)
            {
                TempData["Error"] = "Bạn đã yêu cầu gửi lại mã quá 5 lần. Vui lòng chờ 15 phút rồi thử lại.";
                return RedirectToAction(nameof(VerifyRegistrationEmail), new { email = emailLower });
            }

            var secondsSinceLast = (nowUtc - pending.LastSentAt).TotalSeconds;
            if (secondsSinceLast < 45)
            {
                int waitSec = Math.Max(1, (int)Math.Ceiling(45 - secondsSinceLast));
                TempData["Error"] = $"Vui lòng đợi {waitSec} giây trước khi yêu cầu gửi lại mã xác thực.";
                return RedirectToAction(nameof(VerifyRegistrationEmail), new { email = emailLower });
            }

            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            var verifyUrl = Url.Action(
                nameof(ConfirmRegistrationEmail),
                "Account",
                new { email = emailLower, token = rawToken },
                Request.Scheme) ?? string.Empty;

            bool emailSent = await _emailService.SendRegistrationVerificationEmailAsync(
                emailLower,
                pending.FullName ?? pending.Username,
                otpCode,
                verifyUrl);

            if (!emailSent)
            {
                TempData["Error"] = "Không thể gửi lại email xác thực lúc này. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(VerifyRegistrationEmail), new { email = emailLower });
            }

            pending.OtpHash = ComputeSha256Hex(otpCode);
            pending.TokenHash = ComputeSha256Hex(rawToken);
            pending.ExpiresAt = nowUtc.AddMinutes(15);
            pending.LastSentAt = nowUtc;
            pending.ResendCount++;
            pending.FailedAttempts = 0;

            _cache.Set(pendingCacheKey, pending, TimeSpan.FromMinutes(15));
            _cache.Set($"pending_reg_user_{pending.Username.ToLowerInvariant()}", emailLower, TimeSpan.FromMinutes(15));

            TempData["Success"] = $"Đã gửi mã xác thực mới tới Gmail {emailLower}.";
            return RedirectToAction(nameof(VerifyRegistrationEmail), new { email = emailLower });
        }

        private async Task<(bool Created, string? ErrorMessage)> CompletePendingRegistrationAsync(PendingRegistrationEntry pending)
        {
            var usernameLower = pending.Username.ToLowerInvariant();
            var emailLower = pending.Email.ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            {
                _cache.Remove($"pending_reg_email_{emailLower}");
                _cache.Remove($"pending_reg_user_{usernameLower}");
                return (false, "Tên đăng nhập đã được sử dụng. Vui lòng đăng ký lại.");
            }

            var emailHash = _piiService.ComputeEmailHash(emailLower);
            if (await _context.Users.AnyAsync(u => u.EmailHash != null && u.EmailHash == emailHash))
            {
                _cache.Remove($"pending_reg_email_{emailLower}");
                _cache.Remove($"pending_reg_user_{usernameLower}");
                return (false, "Địa chỉ Gmail này đã được kích hoạt trước đó. Vui lòng đăng nhập.");
            }

            var user = new User
            {
                Username = pending.Username,
                PasswordHash = pending.PasswordHash,
                FullName = pending.FullName,
                Email = emailLower,
                EmailHash = emailHash,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Status = "Active",
                RoleId = pending.RoleId
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _cache.Remove($"pending_reg_email_{emailLower}");
            _cache.Remove($"pending_reg_user_{usernameLower}");

            return (true, null);
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

            var emailHash = _piiService.ComputeEmailHash(emailLower);
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmailHash != null && u.EmailHash == emailHash && u.Status == "Active");

            if (user != null)
            {
                // Nếu tài khoản đăng nhập bằng Google (không đăng ký mật khẩu riêng) -> Không gửi email lấy lại mật khẩu
                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    TempData["Error"] = "Email này thuộc tài khoản đăng nhập bằng Google (không tạo tài khoản mật khẩu). Vui lòng sử dụng nút 'Đăng nhập với Google'!";
                    return RedirectToAction(nameof(Login));
                }

                var nowUtc = VietnamTime.UtcNow;
                var windowStart = nowUtc.AddMinutes(-15);
                int recentRequests = await _context.PasswordResetTokens
                    .CountAsync(t => t.UserId == user.UserId && t.UsedAt == null && t.CreatedAt >= windowStart);

                if (recentRequests < 5)
                {
                    var rawBytes = RandomNumberGenerator.GetBytes(32);
                    var rawToken = WebEncoders.Base64UrlEncode(rawBytes);
                    var tokenHash = ComputeSha256Hex(rawToken);

                    var resetEntry = new PasswordResetToken
                    {
                        UserId = user.UserId,
                        TokenHash = tokenHash,
                        CreatedAt = nowUtc,
                        ExpiresAt = nowUtc.AddMinutes(15),
                        RequestIp = ip
                    };

                    _context.PasswordResetTokens.Add(resetEntry);
                    await _context.SaveChangesAsync();

                    var resetUrl = Url.Action(
                        nameof(ResetPassword),
                        "Account",
                        new { email = user.Email, token = rawToken },
                        Request.Scheme) ?? string.Empty;

                    await _emailService.SendPasswordResetEmailAsync(
                        user.Email!, user.FullName ?? user.Username, resetUrl);
                }
            }

            // Chống dò tài khoản (Anti-Enumeration): luôn hiện cùng thông báo
            TempData["Success"] = "Nếu địa chỉ email tồn tại trong hệ thống, liên kết đặt lại mật khẩu (hiệu lực 15 phút) đã được gửi tới hộp thư của bạn.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? email, string? token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(Login));
            }

            var emailLower = email.Trim().ToLowerInvariant();
            var emailHash = _piiService.ComputeEmailHash(emailLower);
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EmailHash != null && u.EmailHash == emailHash && u.Status == "Active");

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                TempData["Error"] = user != null && string.IsNullOrEmpty(user.PasswordHash)
                    ? "Tài khoản này đăng nhập bằng Google (không sử dụng mật khẩu riêng). Vui lòng bấm 'Đăng nhập với Google'."
                    : "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(Login));
            }

            var tokenHash = ComputeSha256Hex(token.Trim());
            var tokenRecord = await _context.PasswordResetTokens
                .AsNoTracking()
                .Where(t => t.UserId == user.UserId && t.TokenHash == tokenHash)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (tokenRecord == null)
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (tokenRecord.UsedAt != null)
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu này đã được sử dụng trước đó. Vui lòng đăng nhập bằng mật khẩu mới.";
                return RedirectToAction(nameof(Login));
            }

            if (tokenRecord.ExpiresAt <= VietnamTime.Now)
            {
                TempData["Error"] = "Liên kết đặt lại mật khẩu đã hết hạn (quá 15 phút). Vui lòng yêu cầu gửi liên kết mới.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel { Email = email.Trim(), Token = token.Trim() });
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var emailLower = model.Email.Trim().ToLowerInvariant();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var emailHash = _piiService.ComputeEmailHash(emailLower);
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmailHash != null && u.EmailHash == emailHash && u.Status == "Active");

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                TempData["Error"] = user != null && string.IsNullOrEmpty(user.PasswordHash)
                    ? "Tài khoản này đăng nhập bằng Google (không sử dụng mật khẩu riêng). Vui lòng bấm 'Đăng nhập với Google'."
                    : "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction(nameof(Login));
            }

            var tokenHash = ComputeSha256Hex(model.Token.Trim());
            var nowUtc = VietnamTime.UtcNow;
            var nowVn = VietnamTime.Now;

            var tokenRecord = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.TokenHash == tokenHash)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (tokenRecord != null && tokenRecord.UsedAt != null)
            {
                // Xử lý chống bấm đúp (double-submit trên điện thoại trong vòng 15 giây)
                if (tokenRecord.UsedAt.Value >= nowVn.AddSeconds(-15))
                {
                    TempData["Success"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
                    return RedirectToAction(nameof(Login));
                }

                TempData["Error"] = "Liên kết đặt lại mật khẩu này đã được sử dụng trước đó. Vui lòng đăng nhập bằng mật khẩu mới.";
                return RedirectToAction(nameof(Login));
            }

            if (tokenRecord == null || tokenRecord.ExpiresAt <= nowVn)
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
            tokenRecord.UsedAt = nowUtc;

            // Vô hiệu hóa các token chưa dùng khác của cùng user để tránh bấm nhầm link cũ trong email
            var otherUnusedTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.Id != tokenRecord.Id && t.UsedAt == null)
                .ToListAsync();
            foreach (var oldToken in otherUnusedTokens)
            {
                oldToken.UsedAt = nowUtc;
            }

            await _context.SaveChangesAsync();
            _cache.Remove($"auth_stamp_{user.UserId}");

            // Mở khóa nếu tài khoản đang bị tạm khóa do nhập sai mật khẩu trước đó
            _loginAttemptService.ResetAttempts($"account_{user.Username.ToLowerInvariant()}");
            if (!string.IsNullOrEmpty(user.Email))
            {
                _loginAttemptService.ResetAttempts($"account_{user.Email.ToLowerInvariant()}");
            }

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

        // GET: /Account/Profile
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return RedirectToAction(nameof(Login));

            var ticketStats = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.Status == "Booked")
                .Select(t => t.Showtime.Price)
                .ToListAsync();

            var vm = new ProfileViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                RoleName = user.Role.RoleName,
                IsGoogleAccount = !string.IsNullOrEmpty(user.GoogleSubjectId),
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                TotalTickets = ticketStats.Count,
                TotalSpent = ticketStats.Sum()
            };

            return View(vm);
        }

        // POST: /Account/UpdateProfile
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin họ tên hoặc email.";
                return RedirectToAction(nameof(Profile));
            }

            user.FullName = model.FullName.Trim();

            // Chỉ cho phép đổi Email nếu không phải tài khoản Google liên kết
            if (string.IsNullOrEmpty(user.GoogleSubjectId) && !string.IsNullOrWhiteSpace(model.Email))
            {
                var newEmail = model.Email.Trim().ToLowerInvariant();
                if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var newEmailHash = _piiService.ComputeEmailHash(newEmail);
                    bool emailExists = await _context.Users.AnyAsync(u => u.UserId != userId && u.EmailHash != null && u.EmailHash == newEmailHash);
                    if (emailExists)
                    {
                        TempData["Error"] = "Địa chỉ email này đã được sử dụng bởi tài khoản khác.";
                        return RedirectToAction(nameof(Profile));
                    }
                    user.Email = newEmail;
                    user.EmailHash = newEmailHash;
                }
            }

            await _context.SaveChangesAsync();
            await SignInUserAsync(user, isPersistent: true);

            TempData["Success"] = "Cập nhật thông tin hồ sơ thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // POST: /Account/ChangePassword
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return RedirectToAction(nameof(Login));

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                TempData["Error"] = "Tài khoản đăng nhập bằng Google không sử dụng mật khẩu riêng.";
                return RedirectToAction(nameof(Profile));
            }

            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                    ?? "Thông tin mật khẩu không hợp lệ.";
                TempData["Error"] = firstError;
                return RedirectToAction(nameof(Profile));
            }

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
            {
                TempData["Error"] = "Mật khẩu hiện tại không chính xác.";
                return RedirectToAction(nameof(Profile));
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");

            await _context.SaveChangesAsync();
            _cache.Remove($"auth_stamp_{user.UserId}");
            await SignInUserAsync(user, isPersistent: true);

            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // GET & POST: /Account/Logout (Hỗ trợ cả POST và GET trực tiếp qua URL hoặc reload)
        [AcceptVerbs("GET", "POST")]
        [IgnoreAntiforgeryToken]
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

        private string? GetRecaptchaSiteKey()
        {
            var val = _config["Recaptcha:SiteKey"];
            if (string.IsNullOrWhiteSpace(val))
                val = Environment.GetEnvironmentVariable("RECAPTCHA_SITE_KEY");
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }

        private string? GetRecaptchaSecretKey()
        {
            var val = _config["Recaptcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(val))
                val = Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY");
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }

        private async Task<(bool IsValid, string? ErrorMessage)> VerifyRecaptchaAsync()
        {
            var siteKey = GetRecaptchaSiteKey();
            var secretKey = GetRecaptchaSecretKey();

            if (string.IsNullOrEmpty(siteKey) || string.IsNullOrEmpty(secretKey))
            {
                return (false, "Hệ thống chưa cấu hình Google reCAPTCHA (thiếu RECAPTCHA_SITE_KEY / RECAPTCHA_SECRET_KEY).");
            }

            var recaptchaResponse = Request.Form["g-recaptcha-response"].ToString();
            if (string.IsNullOrWhiteSpace(recaptchaResponse))
            {
                return (false, "Vui lòng tích chọn xác nhận 'Tôi không phải là người máy' (Google reCAPTCHA).");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("GoogleRecaptcha");
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secretKey,
                    ["response"] = recaptchaResponse,
                    ["remoteip"] = ip
                });

                var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", formContent);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, "Không thể kết nối tới dịch vụ xác thực Google reCAPTCHA. Vui lòng thử lại.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                if (doc.RootElement.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                {
                    return (true, null);
                }

                return (false, "Xác thực Google reCAPTCHA không hợp lệ hoặc đã hết hạn. Vui lòng tích chọn lại.");
            }
            catch
            {
                return (false, "Lỗi xác thực Google reCAPTCHA. Vui lòng thử lại.");
            }
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

        private sealed class PendingRegistrationEntry
        {
            public string Username { get; set; } = string.Empty;
            public string PasswordHash { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string Email { get; set; } = string.Empty;
            public int RoleId { get; set; }
            public string OtpHash { get; set; } = string.Empty;
            public string TokenHash { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public DateTime LastSentAt { get; set; }
            public int ResendCount { get; set; }
            public int FailedAttempts { get; set; }
        }
    }
}
