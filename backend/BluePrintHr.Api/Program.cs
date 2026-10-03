using System.Text.Json.Serialization;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Middleware;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory");

if (useInMemory)
{
    builder.Services.AddDbContext<BluePrintHrDbContext>(options =>
        options.UseInMemoryDatabase("BluePrintHrDevelopment"));
}
else
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Database:DefaultConnection is required when Database:UseInMemory is false.");

    builder.Services.AddDbContext<BluePrintHrDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5)).UseSnakeCaseNamingConvention());
}

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IRequestContext, RequestContext>();
builder.Services.AddScoped<IPayrollCalculator, KenyaPayrollCalculator>();

var configuredSameSite = builder.Configuration["Auth:CookieSameSite"];
var cookieSameSite = Enum.TryParse<SameSiteMode>(configuredSameSite, ignoreCase: true, out var parsedSameSite)
    ? parsedSameSite
    : SameSiteMode.Lax;
var requireHttpsForCookies = builder.Configuration.GetValue<bool>("Auth:RequireHttps");
var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
if (!string.IsNullOrWhiteSpace(keyDirectory))
{
    Directory.CreateDirectory(keyDirectory);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyDirectory)).SetApplicationName("BluePrintHR");
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "blueprint_hr_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = cookieSameSite;
    options.Cookie.SecurePolicy = requireHttpsForCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        var stamp = context.Principal?.FindFirstValue("security_stamp");
        var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId) || string.IsNullOrWhiteSpace(stamp)) { context.RejectPrincipal(); return; }
        var db = context.HttpContext.RequestServices.GetRequiredService<BluePrintHrDbContext>();
        var user = await db.Users.AsNoTracking().Include(x => x.Tenant).SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null || !user.Active || user.Tenant.Status == TenantStatus.Suspended || !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal)) context.RejectPrincipal();
    };
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageEmployees", policy => policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.HrManager)));
    options.AddPolicy("CanManagePayroll", policy => policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.PayrollManager)));
    options.AddPolicy("CanApprove", policy => policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.HrManager), nameof(UserRole.PayrollManager)));
    options.AddPolicy("CanViewAudit", policy => policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.HrManager)));
    options.AddPolicy("CanViewReports", policy => policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.CompanyAdmin), nameof(UserRole.HrManager), nameof(UserRole.PayrollManager)));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.Use(async (http, next) =>
{
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    http.Response.Headers["X-Frame-Options"] = "DENY";
    http.Response.Headers["Referrer-Policy"] = "no-referrer";
    http.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (http.Request.IsHttps) http.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});
app.UseMiddleware<CsrfOriginMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BluePrintHr.Api" }));
app.MapGet("/health/ready", async (BluePrintHrDbContext db) =>
{
    try
    {
        var connected = await db.Database.CanConnectAsync();
        return connected ? Results.Ok(new { status = "ready", database = "ok" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
});
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services, app.Configuration);
app.Run();

public partial class Program { }
