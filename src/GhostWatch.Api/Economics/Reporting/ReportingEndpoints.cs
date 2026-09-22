using System.Text.Json;
using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Reporting;
public sealed record KpiInput(string[]? Keys,int Revision=0);
public static class ReportingEndpoints
{
 public static void MapReporting(this WebApplication app)
 {
  app.MapGet("/api/economics/overview",async(GhostWatchDbContext db,CancellationToken ct)=>{
   var summary=await EconomicReporting.Capture(db,DateTime.UtcNow,ct);var attention=await EconomicReporting.Attention(db,summary,ct);
   var runs=await db.EconomicRuns.AsNoTracking().OrderByDescending(x=>x.UpdatedAt).Take(10).ToListAsync(ct);
   var records=await db.EconomicRecords.AsNoTracking().OrderByDescending(x=>x.UpdatedAt).Take(10).ToListAsync(ct);
   var adjustments=await db.CapitalAdjustments.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(10).ToListAsync(ct);
   var activity=runs.Select(x=>new ActivityItem(x.Name,"Run","/runs/"+x.Id,x.UpdatedAt))
    .Concat(records.Select(x=>new ActivityItem(x.Title,x.RecordType,"/records?edit="+x.Id,x.UpdatedAt)))
    .Concat(adjustments.Select(x=>new ActivityItem(x.Reason,"Capital adjustment","/capital",x.CreatedAt))).OrderByDescending(x=>x.Timestamp).Take(10);
   return Results.Ok(new {summary,attention,activity,catalog=EconomicReporting.Catalog});
  });
  app.MapGet("/api/economics/tracks/{id:guid}/operations",async(Guid id,GhostWatchDbContext db,CancellationToken ct)=>{
   var summary=(await EconomicReporting.Capture(db,DateTime.UtcNow,ct)).Tracks.Find(x=>x.Id==id);if(summary is null)return Results.NotFound();
   var runs=await db.EconomicRuns.AsNoTracking().Where(x=>x.TrackId==id).OrderByDescending(x=>x.UpdatedAt).ToListAsync(ct);
   var links=await db.KnowledgeLinks.AsNoTracking().Where(x=>x.TrackId==id||db.EconomicRuns.Any(r=>r.Id==x.RunId&&r.TrackId==id)).ToListAsync(ct);
   var bookIds=links.Where(x=>x.PlaybookId!=null).Select(x=>x.PlaybookId).Concat(runs.Select(x=>x.PlaybookId)).ToArray();
   var recordIds=links.Where(x=>x.RecordId!=null).Select(x=>x.RecordId).ToArray();
   var playbooks=await db.Playbooks.AsNoTracking().Where(x=>bookIds.Contains(x.Id)).Select(x=>new {x.Id,x.Name,x.Status}).ToListAsync(ct);
   var records=await db.EconomicRecords.AsNoTracking().Where(x=>recordIds.Contains(x.Id)).OrderByDescending(x=>x.UpdatedAt).Take(20).Select(x=>new {x.Id,x.Title,x.RecordType,x.UpdatedAt}).ToListAsync(ct);
   var objectives=await db.Objectives.AsNoTracking().Where(x=>x.TrackId==id).Select(x=>new {x.Id,x.Name,x.Type,x.Status,x.ManualProgress}).ToListAsync(ct);
   return Results.Ok(new {summary,catalog=EconomicReporting.Catalog,runs=runs.Select(x=>new {x.Id,x.Name,x.Status,x.Purpose,x.UpdatedAt,financials=Runs.RunMetrics.Calculate(x)}),playbooks,records,objectives});
  });
  app.MapPut("/api/economics/tracks/{id:guid}/kpis",async(Guid id,KpiInput input,GhostWatchDbContext db,CancellationToken ct)=>{
   if(!await db.EconomyTracks.AnyAsync(x=>x.Id==id,ct))return Results.NotFound();
   if(input.Keys==null||input.Keys.Length>6||input.Keys.Distinct().Count()!=input.Keys.Length||input.Keys.Any(key=>!EconomicReporting.Catalog.Any(x=>x.Key==key)))return Results.Problem(statusCode:400,title:"Select up to six different supported metrics.");
   var row=await db.TrackKpiSelections.FindAsync([id],ct);if((row?.Revision??0)!=input.Revision)return Conflict();
   if(row==null){row=new(){TrackId=id,Revision=0};db.TrackKpiSelections.Add(row);}row.KeysJson=JsonSerializer.Serialize(input.Keys);row.Revision++;
   try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict();}catch(DbUpdateException e) when(e.InnerException is Microsoft.Data.Sqlite.SqliteException{SqliteErrorCode:19}){return Conflict();}
   return Results.Ok(new {row.Revision});
  });
 }
 private static IResult Conflict()=>Results.Problem(statusCode:409,title:"Selected metrics changed. Reload before saving.");
}
