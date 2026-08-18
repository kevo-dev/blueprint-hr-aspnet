using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BluePrintHr.Api.Data;

public sealed class BluePrintHrDbContextFactory : IDesignTimeDbContextFactory<BluePrintHrDbContext>
{
    public BluePrintHrDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var projectDirectory = ResolveProjectDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured before running EF Core migrations.");

        var options = new DbContextOptionsBuilder<BluePrintHrDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5))
            .Options;

        return new BluePrintHrDbContext(options);
    }

    private static string ResolveProjectDirectory()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "BluePrintHr.Api"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."))
        };

        var projectDirectory = candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json")));
        return projectDirectory ?? throw new DirectoryNotFoundException("Could not locate the BluePrintHr.Api project directory containing appsettings.json.");
    }
}
