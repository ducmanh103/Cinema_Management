using CinemaManagement.Data;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ================================
// 1. MVC + API + Swagger
// ================================
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Cinema Management API", Version = "v1" });
    // Swagger sẽ gửi cookie khi dùng browser session
    c.AddSecurityDefinition("cookieAuth", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
        Name = ".AspNetCore.Cookies"
    });
});

// ================================
// 2. Distributed Cache & Secure Session
// ================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".CinemaHub.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;                             // Chống XSS đọc trộm cookie
    options.Cookie.IsEssential = true;                         // Hoạt động không bị chặn chính sách Cookie
    options.Cookie.SameSite = SameSiteMode.Lax;                // Chống CSRF tấn công
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Hỗ trợ cả dev (http) và prod (https)
});

// ================================
// 3. Rate Limiting Policy (Multi-Dimensional)
// ================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            await context.HttpContext.Response.WriteAsync("{\"error\":\"Quá nhiều yêu cầu từ thiết bị của bạn. Vui lòng thử lại sau giây lát.\"}", token);
        }
        else
        {
            context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
            await context.HttpContext.Response.WriteAsync("<h2 style='text-align:center;margin-top:50px;font-family:sans-serif;color:#e50914;'>Hệ thống phát hiện thao tác quá nhanh. Vui lòng chờ vài giây rồi tải lại trang!</h2>", token);
        }
    };

    // Hàm helper tạo partition key kết hợp IP + Session ID + User ID chống rotating proxy
    string GetClientPartitionKey(HttpContext ctx, string prefix)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "ip_unknown";
        var sessionId = ctx.Session.Id;
        var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId)) return $"{prefix}_user_{userId}";
        if (!string.IsNullOrEmpty(sessionId)) return $"{prefix}_session_{sessionId}";
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

    // Policy riêng cho Auth (Login/Register): tối đa 10 requests / phút
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

// 4. Database
builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ================================
// 5. Authentication (Cookie)
// ================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
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

        // Redirect sang Admin Login nếu truy cập Admin Area mà chưa đăng nhập
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
    });

builder.Services.AddAuthorization();

// ================================
// 6. Services (Dependency Injection)
// ================================
builder.Services.AddSingleton<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IShowtimeService, ShowtimeService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();

// Background service: tự động hủy booking pending quá 15 phút
builder.Services.AddHostedService<PendingBookingCleanupService>();

// ================================
var app = builder.Build();
// ================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Swagger chỉ ở Development
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cinema API v1"));
}

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

// Gọi Seed sau khi build
DbInitializer.Seed(app);

app.Run();

