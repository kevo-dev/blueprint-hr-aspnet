using System.Text.Json.Serialization;
using BluePrintHr.Api.Data;
using BluePrintHr.Api.Models;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory") || string.IsNullOrWhiteSpace(connectionString);
if (useInMemory)
    builder.Services.AddDbContext<BluePrintHrDbContext>(options => options.UseInMemoryDatabase("BluePrintHrDevelopment"));
else
    builder.Services.AddDbContext<BluePrintHrDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5)));

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IRequestContext, RequestContext>();
builder.Services.AddScoped<IPayrollCalculator, KenyaPayrollCalculator>();
var configuredSameSite = builder.Configuration["Auth:CookieSameSite"];
var cookieSameSite = Enum.TryParse<SameSiteMode>(configuredSameSite, ignoreCase: true, out var parsedSameSite)
    ? parsedSameSite
    : SameSiteMode.Lax;
var requireHttpsForCookies = builder.Configuration.GetValue<bool>("Auth:RequireHttps");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "blueprint_hr_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = cookieSameSite;
    options.Cookie.SecurePolicy = requireHttpsForCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
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
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BluePrintHr.Api" }));
app.MapControllers();
await DbInitializer.InitializeAsync(app.Services, app.Configuration);
app.Run();

public partial class Program { }
