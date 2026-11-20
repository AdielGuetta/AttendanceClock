using AttendanceClock.Api.Data;
using AttendanceClock.Api.Time;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database: SQL Server, via Entity Framework Core. The connection string comes from configuration
// (in Docker, from the ConnectionStrings__Default environment variable in docker-compose.yml).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// Trusted clock (PRD §4): external time APIs only. Registration ORDER = try order: primary first, then backup.
builder.Services.Configure<TimeProviderOptions>(builder.Configuration.GetSection(TimeProviderOptions.SectionName));
builder.Services.AddHttpClient<TimeApiIoSource>(ConfigureTimeHttpClient);
builder.Services.AddHttpClient<TimeNowSource>(ConfigureTimeHttpClient);
builder.Services.AddTransient<IExternalTimeSource>(sp => sp.GetRequiredService<TimeApiIoSource>());
builder.Services.AddTransient<IExternalTimeSource>(sp => sp.GetRequiredService<TimeNowSource>());
builder.Services.AddTransient<ITrustedClock, FallbackTrustedClock>();

// Errors as ProblemDetails (RFC 9457); "time service down" becomes a clear 503.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TimeSourceUnavailableExceptionHandler>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Health checks: "is the server alive, and can it reach the database?"
// Later we'll add a check for the external time providers.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Development convenience: create or upgrade the database tables on startup,
    // so a fresh clone works with just "docker compose up".
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static void ConfigureTimeHttpClient(HttpClient client)
{
    // The per-attempt timeout is enforced by FallbackTrustedClock; this is only a safety net.
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AttendanceClock/1.0");
}

// Lets the test project start the app in memory (WebApplicationFactory), if needed later.
public partial class Program;
