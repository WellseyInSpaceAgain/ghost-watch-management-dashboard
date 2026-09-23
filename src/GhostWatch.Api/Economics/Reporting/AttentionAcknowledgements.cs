using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GhostWatch.Api.Economics.Reporting;

public sealed class AttentionAcknowledgement
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public string Key {get;set;}="";
 public string Rule {get;set;}="";
 public string? SubjectType {get;set;}
 public string? SubjectId {get;set;}
 public DateTime AcknowledgedAt {get;set;}=DateTime.UtcNow;
 // Retain context for inspection even after a condition clears or its subject is removed.
 public string Message {get;set;}="";
 public string Path {get;set;}="";
}

public sealed record AcknowledgeInput(string? Key);
public sealed record AcknowledgedAttention(Guid Id,AttentionItem Item,DateTime AcknowledgedAt,bool Matches);
public sealed record AttentionState(List<AttentionItem> Active,List<AcknowledgedAttention> Acknowledged);

public static class AttentionAcknowledgements
{
 public static async Task<AttentionState> Read(GhostWatchDbContext db,List<AttentionItem> matching,CancellationToken ct)
 {
  var saved=await db.AttentionAcknowledgements.AsNoTracking().OrderByDescending(x=>x.AcknowledgedAt).ToListAsync(ct);
  var byKey=matching.ToDictionary(x=>x.Key);
  var accepted=saved.Select(x=>new AcknowledgedAttention(x.Id,
   byKey.GetValueOrDefault(x.Key)??new AttentionItem(x.Rule,x.Message,x.Path,x.SubjectType,x.SubjectId),
   x.AcknowledgedAt,byKey.ContainsKey(x.Key))).ToList();
  var keys=saved.Select(x=>x.Key).ToHashSet();
  return new(matching.Where(x=>!keys.Contains(x.Key)).ToList(),accepted);
 }

 public static void MapAttention(this WebApplication app)
 {
  app.MapGet("/api/economics/attention",async(GhostWatchDbContext db,CancellationToken ct)=>
   Results.Ok(await Read(db,await Matching(db,ct),ct)));

  app.MapPost("/api/economics/attention/acknowledgements",async(AcknowledgeInput input,GhostWatchDbContext db,CancellationToken ct)=>{
   if(string.IsNullOrWhiteSpace(input.Key)||input.Key.Length>200)
    return Results.Problem(statusCode:400,title:"Choose a currently matching Needs Attention item.");
   // Only generated findings may be accepted. Read and save in one database transaction.
   await using var transaction=await db.Database.BeginTransactionAsync(ct);
   var item=(await Matching(db,ct)).Find(x=>x.Key==input.Key);
   if(item is null)return Results.Problem(statusCode:409,title:"This finding no longer matches. Reload Needs Attention.");
   if(await db.AttentionAcknowledgements.AnyAsync(x=>x.Key==item.Key,ct))return Conflict();
   var row=new AttentionAcknowledgement{Key=item.Key,Rule=item.Rule,SubjectType=item.SubjectType,SubjectId=item.SubjectId,Message=item.Message,Path=item.Path};
   db.AttentionAcknowledgements.Add(row);
   try{await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);}
   catch(DbUpdateException e) when(e.InnerException is Microsoft.Data.Sqlite.SqliteException{SqliteErrorCode:19}){return Conflict();}
   return Results.Ok(new AcknowledgedAttention(row.Id,item,row.AcknowledgedAt,true));
  });

  app.MapDelete("/api/economics/attention/acknowledgements/{id:guid}",async(Guid id,GhostWatchDbContext db,CancellationToken ct)=>{
   // The acknowledgement ID is a generation token: a stale restore cannot delete a later acceptance.
   var row=await db.AttentionAcknowledgements.FindAsync([id],ct);
   if(row is null)return Conflict();
   db.AttentionAcknowledgements.Remove(row);
   try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict();}
   return Results.NoContent();
  });
 }

 private static async Task<List<AttentionItem>> Matching(GhostWatchDbContext db,CancellationToken ct)=>
  await EconomicReporting.Attention(db,await EconomicReporting.Capture(db,DateTime.UtcNow,ct),ct);
 private static IResult Conflict()=>Results.Problem(statusCode:409,title:"Acknowledgement changed. Reload Needs Attention before trying again.");
}
