using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CinemaManagement.Data;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

// ================================
// Serilog Bootstrap Logger
// ================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/cinemahub-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        fileSizeLimitBytes: 25_000_000)
    .CreateLogger();

try
{
    Log.Information("🎬 CinemaHub starting up (PostgreSQL TIMESTAMPTZ UTC mode)...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ================================
    // 1. MVC + API + Swagger + Antiforgery
    // ================================
    builder.Services.AddControllersWithViews();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Cinema Management API", Version = "v1" });
        c.AddSecurityDefinition("cookieAuth", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
            Name = ".CinemaHub.Auth"
        });
    });

    // Hardening Antiforgery Cookie (tự động bật cờ Secure khi truy cập qua HTTPS, vẫn hoạt động qua HTTP IP VPS)
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = ".CinemaHub.Antiforgery";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.HeaderName = "RequestVerificationToken";
    });

    // ================================
    // 2. MemoryCache, Distributed Cache & Secure Session
    // ================================
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.Cookie.Name = ".CinemaHub.Session";
        options.IdleTimeout = TimeSpan.FromMinutes(20);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    // ================================
    // 3. HttpClientFactory (tránh cạn kiệt socket trên VPS 2GB)
    // ================================
    builder.Services.AddHttpClient("GeminiApi", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(25);
    });
    builder.Services.AddHttpClient("TelegramBot", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddHttpClient("GoogleRecaptcha", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // ================================
    // 4. Rate Limiting Policy (IP-first cho Anonymous, chống lách qua cookieless bot)
    // ================================
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
        {
            var httpContext = context.HttpContext;
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "ip_unknown";
            var username = httpContext.User.Identity?.Name;
            var path = httpContext.Request.Path.Value ?? "/";

            httpContext.RequestServices
                .GetService<ISecurityAlertService>()?
                .RecordRateLimitRejection(path, ip, username);

            if (httpContext.Request.Path.StartsWithSegments("/api"))
            {
                httpContext.Response.ContentType = "application/json; charset=utf-8";
                await httpContext.Response.WriteAsync(
                    "{\"error\":\"Quá nhiều yêu cầu từ thiết bị của bạn. Vui lòng thử lại sau giây lát.\"}", token);
            }
            else
            {
                httpContext.Response.ContentType = "text/html; charset=utf-8";
                await httpContext.Response.WriteAsync(
                    "<h2 style='text-align:center;margin-top:50px;font-family:sans-serif;color:#e50914;'>Hệ thống phát hiện thao tác quá nhanh. Vui lòng chờ vài giây rồi tải lại trang!</h2>", token);
            }
        };

        // Ưu tiên UserId nếu đã đăng nhập; nếu ẩn danh thì bắt buộc dùng IP thật (không dùng Session.Id để tránh bot không giữ cookie)
        static string GetClientPartitionKey(HttpContext ctx, string prefix)
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "ip_unknown";
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId)) return $"{prefix}_user_{userId}_{ip}";
            return $"{prefix}_ip_{ip}";
        }

        // Toàn cục (Global): 120 requests / 10s
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var key = GetClientPartitionKey(httpContext, "global");
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: key,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromSeconds(10),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        // Policy riêng cho Auth (Login/Register/ForgotPassword): tối đa 10 requests / phút
        options.AddPolicy("AuthLimit", httpContext =>
        {
            var key = GetClientPartitionKey(httpContext, "auth");
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: key,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        // Policy riêng cho Chatbot Gemini API: 15 requests / phút
        options.AddPolicy("ChatBotLimit", httpContext =>
        {
            var key = GetClientPartitionKey(httpContext, "chat");
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: key,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 15,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        // Policy riêng cho Booking/Thanh toán: 10 requests / 30s
        options.AddPolicy("BookingLimit", httpContext =>
        {
            var key = GetClientPartitionKey(httpContext, "booking");
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: key,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromSeconds(30),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });
    });

    // ================================
    // 5. Database (PostgreSQL + DbContextPool tối ưu RAM + DataProtection Keys)
    // ================================
    var rawConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Chưa cấu hình DefaultConnection.");

    // Tối ưu Connection Pool cho VPS 2 vCPU / 2 GB RAM
    if (!rawConnStr.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
    {
        rawConnStr = rawConnStr.TrimEnd(';') + ";Maximum Pool Size=20;Minimum Pool Size=2;Connection Idle Lifetime=60;";
    }

    builder.Services.AddDbContextPool<CinemaDbContext>(options =>
        options.UseNpgsql(rawConnStr), poolSize: 64);

    var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "dp_keys");
    Directory.CreateDirectory(keysFolder);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
        .SetApplicationName("CinemaHub");

    // ================================
    // 6. Authentication (Cookie + SecurityStamp Validation + Google OAuth 2.0)
    // ================================
    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = ".CinemaHub.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                var returnUrl = context.Request.Path + context.Request.QueryString;
                context.RedirectUri = "/Admin/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl);
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        // Kiểm tra lại trạng thái tài khoản & SecurityStamp định kỳ (nhờ IMemoryCache không gây tải DB)
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var cacheKey = $"auth_stamp_{userId}";

            if (!cache.TryGetValue(cacheKey, out (string Status, string Stamp, string RoleName) dbState))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<CinemaDbContext>();
                var userInfo = await db.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == userId)
                    .Select(u => new { u.Status, u.SecurityStamp, RoleName = u.Role.RoleName })
                    .FirstOrDefaultAsync();

                if (userInfo == null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                dbState = (userInfo.Status, userInfo.SecurityStamp ?? string.Empty, userInfo.RoleName);
                cache.Set(cacheKey, dbState, TimeSpan.FromMinutes(2));
            }

            var claimStamp = context.Principal?.FindFirst("SecurityStamp")?.Value;
            var claimRole = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;

            if (dbState.Status != "Active"
                || (!string.IsNullOrEmpty(claimStamp) && !string.Equals(claimStamp, dbState.Stamp, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(claimRole) && !string.Equals(claimRole, dbState.RoleName, StringComparison.Ordinal)))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    })
    .AddCookie("ExternalTempCookie", options =>
    {
        options.Cookie.Name = ".CinemaHub.External";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

    var rawGoogleClientId = builder.Configuration["Authentication:Google:ClientId"];
    if (string.IsNullOrWhiteSpace(rawGoogleClientId))
        rawGoogleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
    var googleClientId = string.IsNullOrWhiteSpace(rawGoogleClientId)
        ? "unconfigured-google-client-id"
        : rawGoogleClientId.Trim();

    var rawGoogleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    if (string.IsNullOrWhiteSpace(rawGoogleClientSecret))
        rawGoogleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
    var googleClientSecret = string.IsNullOrWhiteSpace(rawGoogleClientSecret)
        ? "unconfigured-google-client-secret"
        : rawGoogleClientSecret.Trim();

    authBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = "ExternalTempCookie";
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });

    builder.Services.AddAuthorization();

    // ================================
    // 7. Services (Dependency Injection)
    // ================================
    builder.Services.AddSingleton<ILoginAttemptService, LoginAttemptService>();
    builder.Services.AddSingleton<TelegramSecurityAlertService>();
    builder.Services.AddSingleton<ISecurityAlertService>(sp => sp.GetRequiredService<TelegramSecurityAlertService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramSecurityAlertService>());

    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IMovieService, MovieService>();
    builder.Services.AddScoped<IShowtimeService, ShowtimeService>();
    builder.Services.AddScoped<ITicketService, TicketService>();
    builder.Services.AddScoped<IVnPayService, VnPayService>();

    // Background service: tự động hủy booking pending quá 15 phút
    builder.Services.AddHostedService<PendingBookingCleanupService>();

    // ================================
    // 8. Health Checks
    // ================================
    builder.Services.AddHealthChecks()
        .AddNpgSql(rawConnStr,
                   name: "postgresql",
                   tags: new[] { "db", "ready" });

    // ================================
    var app = builder.Build();
    // ================================

    // Cấu hình ForwardedHeaders tin cậy Reverse Proxy Nginx + CDN trong mạng Docker
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = null
    };
    forwardedOptions.KnownNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);

    // Đảm bảo khi truy cập qua tên miền (ví dụ cinemahubvn.io.vn đi qua CDN về cổng 80), Scheme luôn là https cho Google OAuth & Cookie
    app.Use((context, next) =>
    {
        var host = context.Request.Host.Host;
        if (!string.IsNullOrEmpty(host)
            && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !IPAddress.TryParse(host, out _))
        {
            context.Request.Scheme = "https";
        }
        return next();
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cinema API v1"));
    }

    // ================================
    // Security Headers Middleware (Xử lý triệt để báo cáo OWASP ZAP: CSP, Anti-Clickjacking, nosniff)
    // ================================
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' https://cdn.jsdelivr.net https://www.google.com https://www.gstatic.com 'unsafe-inline'; " +
            "style-src 'self' https://cdn.jsdelivr.net https://fonts.googleapis.com 'unsafe-inline'; " +
            "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com data:; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' https://www.google.com; " +
            "frame-src 'self' https://www.google.com https://recaptcha.google.com; " +
            "frame-ancestors 'self'; " +
            "form-action 'self' https://sandbox.vnpayment.vn https://accounts.google.com;";

        await next();
    });

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    // Middleware Order: Session -> RateLimiter -> Auth
    app.UseSession();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // Health Check Endpoints
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    // Khởi tạo / tự động nâng cấp schema PostgreSQL (TIMESTAMPTZ, Auth, DataProtection)
    DbInitializer.Seed(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ CinemaHub terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
