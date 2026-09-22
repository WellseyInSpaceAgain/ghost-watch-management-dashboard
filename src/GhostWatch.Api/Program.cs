using GhostWatch.Api.Data;
using GhostWatch.Api.Eve.Auth;
using Microsoft.AspNetCore.DataProtection;
using GhostWatch.Api.Economics.Tracks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
// Environment variables win over optional, ignored local configuration.
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddProblemDetails();
var dataDirectory = Path.GetFullPath(builder.Configuration["Storage:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(dataDirectory);
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = Path.Combine(dataDirectory, "ghost-watch.db"),
    ForeignKeys = true
}.ToString();
builder.Services.AddDbContext<GhostWatchDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDataProtection().SetApplicationName("GhostWatchManagementDashboard")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PendingLogins>();
builder.Services.AddSingleton<CharacterGate>();
builder.Services.AddScoped<CharacterAccessTokens>();
builder.Services.Configure<EveOptions>(options =>
{
    if (builder.Environment.IsDevelopment()) options.CallbackUrl = "http://localhost:4200/api/auth/eve/callback";
    builder.Configuration.GetSection("Eve").Bind(options);
});
builder.Services.AddHttpClient<SsoClient>(http => http.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddControllers();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().Database.Migrate();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { application = "Ghost Watch Management Dashboard", status = "ok" }));
app.MapTracks();
app.MapControllers();
app.MapGet("/api/eve/characters", async (GhostWatchDbContext db, CancellationToken ct) =>
    await db.EveCharacters.AsNoTracking().OrderBy(x => x.CharacterName)
        .Select(x => new { x.CharacterId, x.CharacterName, x.ConnectedAt, x.LastAuthenticatedAt }).ToListAsync(ct));
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
