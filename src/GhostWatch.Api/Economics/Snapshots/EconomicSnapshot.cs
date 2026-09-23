using System.Text.Json;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Reporting;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Snapshots;
public sealed class EconomicSnapshot
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public DateTime Timestamp {get;set;}
 public string Trigger {get;set;}="Manual";
 public string? MonthKey {get;set;}
 public string? Name {get;set;}
 public string? Note {get;set;}
 public int SchemaVersion {get;set;}=2;
 public string ValuesJson {get;set;}="{}";
}
public sealed class SnapshotStore(GhostWatchDbContext db)
{
 public static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
 public async Task<EconomicSnapshot> Capture(DateTime now,bool monthly,string? name,string? note,CancellationToken ct)
 {
  now=now.ToUniversalTime();var month=monthly?now.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture):null;
  // One SQLite read/write transaction captures a consistent local state and serializes competing monthly writers.
  await using var transaction=await db.Database.BeginTransactionAsync(ct);
  if(monthly&&await db.EconomicSnapshots.AsNoTracking().SingleOrDefaultAsync(x=>x.MonthKey==month,ct) is {} existing)return existing;
  var values=await EconomicReporting.Capture(db,now,ct);
  var row=new EconomicSnapshot{Timestamp=now,Trigger=monthly?"AutomaticMonthly":"Manual",MonthKey=month,Name=string.IsNullOrWhiteSpace(name)?null:name.Trim(),Note=string.IsNullOrWhiteSpace(note)?null:note.Trim(),ValuesJson=JsonSerializer.Serialize(values,JsonOptions)};
  db.EconomicSnapshots.Add(row);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return row;
 }
 public static ProgrammeSummary Values(EconomicSnapshot snapshot)=>JsonSerializer.Deserialize<ProgrammeSummary>(snapshot.ValuesJson,JsonOptions)!;
}
public sealed class MonthlySnapshots(IServiceScopeFactory scopes,IConfiguration configuration,ILogger<MonthlySnapshots> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  if(!configuration.GetValue("Snapshots:AutomaticEnabled",true))return;
  using var timer=new PeriodicTimer(TimeSpan.FromHours(1));
  do {
   try {await using var scope=scopes.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<SnapshotStore>().Capture(DateTime.UtcNow,true,null,null,stoppingToken);}
   catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
   catch(Exception){logger.LogWarning("The monthly snapshot could not be captured. It will be retried within one hour.");}
  }while(await timer.WaitForNextTickAsync(stoppingToken));
 }
}
public sealed record SnapshotInput(string? Name,string? Note);
public static class SnapshotEndpoints
{
 public static void MapSnapshots(this WebApplication app)
 {
  var group=app.MapGroup("/api/economics/snapshots");
  group.MapGet("/",async(GhostWatchDbContext db,CancellationToken ct)=>await db.EconomicSnapshots.AsNoTracking().OrderByDescending(x=>x.Timestamp).Select(x=>new{x.Id,x.Timestamp,x.Trigger,x.MonthKey,x.Name,x.Note,x.SchemaVersion}).ToListAsync(ct));
  group.MapGet("/{id:guid}",async(Guid id,GhostWatchDbContext db,CancellationToken ct)=>await db.EconomicSnapshots.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct) is {} row?Results.Ok(new {row.Id,row.Timestamp,row.Trigger,row.Name,row.Note,row.SchemaVersion,values=SnapshotStore.Values(row)}):Results.NotFound());
  group.MapPost("/",async(SnapshotInput input,SnapshotStore store,CancellationToken ct)=>{
   if(input.Name?.Length>160||input.Note?.Length>20000)return Results.Problem(statusCode:400,title:"Snapshot names are limited to 160 characters and notes to 20,000.");
   var row=await store.Capture(DateTime.UtcNow,false,input.Name,input.Note,ct);return Results.Created($"/api/economics/snapshots/{row.Id}",new{row.Id});
  });
 }
}
