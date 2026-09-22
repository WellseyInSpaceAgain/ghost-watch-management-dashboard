using GhostWatch.Api.Data;
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
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<GhostWatchDbContext>().Database.Migrate();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { application = "Ghost Watch Management Dashboard", status = "ok" }));
app.MapTracks();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
