using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using StuRoom.Authorization;
using StuRoom.Data;
using StuRoom.Models;
using Microsoft.AspNetCore.Identity;
using StuRoom.Services;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── Identity ───────────────────────────────────────────────────────────
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Cookie: redirect về trang AccessDenied tùy chỉnh
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccountStatus/AccessDenied";
    options.LoginPath = "/Identity/Account/Login";
});

// ── Authorization ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthorizationHandler, LandlordApprovedHandler>();

builder.Services.AddAuthorization(options =>
{
    // Chỉ Admin
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Landlord đã được Admin duyệt
    options.AddPolicy("LandlordOnly", policy =>
    {
        policy.RequireRole("Landlord");
        policy.AddRequirements(new LandlordApprovedRequirement());
    });

    // Tenant (chỉ cần role, không cần duyệt)
    options.AddPolicy("TenantOnly", policy =>
        policy.RequireRole("Tenant"));

    // Landlord hoặc Admin — dùng cho các màn hình quản lý chung
    options.AddPolicy("LandlordOrAdmin", policy =>
        policy.RequireRole("Landlord", "Admin"));
});

// ── Email ──────────────────────────────────────────────────────────────
// Overrides the default no-op IEmailSender registered by AddDefaultIdentity
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// ── Cloudinary ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// ── Seed database ──────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

// ── Pipeline ───────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

var supportedCultures = new[] { "vi" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("vi")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
