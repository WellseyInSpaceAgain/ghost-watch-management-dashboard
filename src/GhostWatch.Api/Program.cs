using GhostWatch.Api.Data;
using GhostWatch.Api.Management;
using GhostWatch.Api.Economics.Capital;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Planning;
using GhostWatch.Api.Eve.Inventory;
using GhostWatch.Api.Eve.Esi;
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
builder.Services.AddScoped<ICharacterAccessTokens>(services => services.GetRequiredService<CharacterAccessTokens>());
builder.Services.AddSingleton<EsiThrottle>();
builder.Services.AddScoped<CharacterRefresh>();
builder.Services.AddScoped<InventoryMetadata>();
builder.Services.AddScoped<LocationNames>();
builder.Services.AddScoped<ExtraFacts>();
builder.Services.AddSingleton<RefreshQueue>();
builder.Services.AddHostedService(services => services.GetRequiredService<RefreshQueue>());
builder.Services.AddHttpClient<EsiClient>(http =>
{
    http.BaseAddress = new Uri("https://esi.evetech.net/");
    http.Timeout = TimeSpan.FromSeconds(45);
    http.DefaultRequestHeaders.Add("X-Compatibility-Date", "2026-09-22");
    http.DefaultRequestHeaders.Add("User-Agent", "GhostWatchManagementDashboard/0.1");
    http.DefaultRequestHeaders.Add("Accept-Language", "en");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
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
app.MapCapital();
app.MapRuns();
app.MapPlanning();
app.MapCharacterManagement();
app.MapEveData();
app.MapControllers();
app.MapGet("/api/eve/characters", async (GhostWatchDbContext db, RefreshQueue queue, CancellationToken ct) =>
{
    var characters = await db.EveCharacters.AsNoTracking().OrderBy(x => x.CharacterName)
        .Select(x => new { x.CharacterId, x.CharacterName, x.ConnectedAt, x.LastAuthenticatedAt, x.GrantedScopesJson }).ToListAsync(ct);
    var plans = await db.CharacterPlans.AsNoTracking().Include(x => x.Account).ToDictionaryAsync(x => x.CharacterId, ct);
    return Results.Ok(characters.Select(x => new { x.CharacterId, x.CharacterName, x.ConnectedAt, x.LastAuthenticatedAt,
        accountName = plans.GetValueOrDefault(x.CharacterId)?.Account?.Name, subscription = plans.GetValueOrDefault(x.CharacterId)?.Account?.Subscription ?? "Unknown", assignment = plans.GetValueOrDefault(x.CharacterId)?.Assignment,
        permissions = EveScopes.Permissions(x.GrantedScopesJson), progress = queue.Status(x.CharacterId) }).ToArray());
});
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
