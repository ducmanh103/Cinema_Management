using CinemaManagement.Data;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

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


// 2. Database

builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ================================
// 3. Authentication (Cookie)
// ================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
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
// 4. Services (Dependency Injection)
// ================================
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

// ⇒ Auth phải nằm GIỮA Routing và Authorization
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
