using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Charts;
public sealed record ChartInput(string? Name,string? ConfigJson,int Revision=0);
public sealed record PreviewInput(string? ConfigJson,Guid? TrackId=null);
public sealed record PlacementInput(Guid ChartDefinitionId,string PageType,Guid? PageId,string Width="Medium");
public sealed record LayoutRow(Guid Id,string Width,int Revision);
public sealed record LayoutInput(string PageType,Guid? PageId,LayoutRow[]? Placements);
public static class ChartEndpoints
{
 public static void MapCharts(this WebApplication app)
 {
  var group=app.MapGroup("/api/economics/charts");
  group.AddEndpointFilter(async(context,next)=>{try{return await next(context);}catch(ChartValidationException error){return Results.Problem(statusCode:400,title:error.Message);}});
  group.MapGet("/schema",()=>new {sample=ChartValidation.Sample,sources=ChartValidation.Sources,types=ChartValidation.Types,aggregations=ChartValidation.Aggregations,formats=ChartValidation.Formats});
  group.MapPost("/preview",async(PreviewInput input,GhostWatchDbContext db,CancellationToken ct)=>await ChartQuery.Execute(ChartValidation.Parse(input.ConfigJson),input.TrackId,db,ct));
  group.MapGet("/definitions",async(GhostWatchDbContext db,CancellationToken ct)=>await db.ChartDefinitions.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Title,x.Description,x.ConfigJson,x.Revision,x.UpdatedAt,placementCount=db.ChartPlacements.Count(p=>p.ChartDefinitionId==x.Id)}).ToListAsync(ct));
  group.MapPost("/definitions",async(ChartInput input,GhostWatchDbContext db,CancellationToken ct)=>{
   var config=Validate(input);var row=new ChartDefinition{Name=input.Name!.Trim(),Title=config.Title,Description=config.Description??"",ConfigJson=input.ConfigJson!};db.ChartDefinitions.Add(row);await db.SaveChangesAsync(ct);return Results.Created($"/api/economics/charts/definitions/{row.Id}",row);
  });
  group.MapPut("/definitions/{id:guid}",async(Guid id,ChartInput input,GhostWatchDbContext db,CancellationToken ct)=>{
   var config=Validate(input);var row=await db.ChartDefinitions.FindAsync([id],ct);if(row==null)return Results.NotFound();if(row.Revision!=input.Revision)return Conflict();row.Name=input.Name!.Trim();row.Title=config.Title;row.Description=config.Description??"";row.ConfigJson=input.ConfigJson!;row.Revision++;row.UpdatedAt=DateTime.UtcNow;
   try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict();}return Results.Ok(row);
  });
  group.MapDelete("/definitions/{id:guid}",async(Guid id,int revision,GhostWatchDbContext db,CancellationToken ct)=>{
   var row=await db.ChartDefinitions.FindAsync([id],ct);if(row==null)return Results.NotFound();if(row.Revision!=revision)return Conflict();if(await db.ChartPlacements.AnyAsync(x=>x.ChartDefinitionId==id,ct))return Results.Problem(statusCode:409,title:"Remove this definition's page placements before deleting it.");db.ChartDefinitions.Remove(row);
   try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){return Conflict();}return Results.NoContent();
  });
  group.MapGet("/placements",async(string pageType,Guid? pageId,GhostWatchDbContext db,CancellationToken ct)=>{
   await ValidatePage(pageType,pageId,db,ct);return Results.Ok(await db.ChartPlacements.AsNoTracking().Where(x=>x.PageType==pageType&&x.PageId==pageId).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).ToListAsync(ct));
  });
  group.MapPost("/placements",async(PlacementInput input,GhostWatchDbContext db,CancellationToken ct)=>{
   await ValidatePage(input.PageType,input.PageId,db,ct);ValidateWidth(input.Width);
   var definition=await db.ChartDefinitions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.ChartDefinitionId,ct);if(definition==null)return Results.Problem(statusCode:400,title:"Choose an existing chart definition.");
   if(input.PageType=="Dashboard"&&ChartValidation.Parse(definition.ConfigJson).Filters?.TrackId=="CURRENT_TRACK")throw new ChartValidationException("This definition uses CURRENT_TRACK. Place it on a Track page or use an explicit Track filter for the Dashboard.");
   var count=await db.ChartPlacements.CountAsync(x=>x.PageType==input.PageType&&x.PageId==input.PageId,ct);if(count>=30)return Results.Problem(statusCode:400,title:"A page supports up to 30 charts.");
   var order=await db.ChartPlacements.Where(x=>x.PageType==input.PageType&&x.PageId==input.PageId).Select(x=>(int?)x.SortOrder).MaxAsync(ct)??-1;
   var row=new ChartPlacement{ChartDefinitionId=input.ChartDefinitionId,PageType=input.PageType,PageId=input.PageId,Width=input.Width,SortOrder=order+1};db.ChartPlacements.Add(row);await db.SaveChangesAsync(ct);return Results.Created($"/api/economics/charts/placements/{row.Id}",row);
  });
  group.MapPut("/layout",async(LayoutInput input,GhostWatchDbContext db,CancellationToken ct)=>{
   await ValidatePage(input.PageType,input.PageId,db,ct);if(input.Placements==null||input.Placements.Length>30||input.Placements.Any(x=>x==null)||input.Placements.Select(x=>x.Id).Distinct().Count()!=input.Placements.Length)throw new ChartValidationException("Provide this page's placements once each, in the desired order.");
   foreach(var placement in input.Placements)ValidateWidth(placement.Width);
   await using var transaction=await db.Database.BeginTransactionAsync(ct);
   var rows=await db.ChartPlacements.Where(x=>x.PageType==input.PageType&&x.PageId==input.PageId).ToListAsync(ct);
   if(rows.Count!=input.Placements.Length||input.Placements.Any(x=>!rows.Any(r=>r.Id==x.Id&&r.Revision==x.Revision)))return Conflict();
   for(var i=0;i<input.Placements.Length;i++){var p=input.Placements[i];var row=rows.Single(x=>x.Id==p.Id);row.SortOrder=i;row.Width=p.Width;row.Revision++;row.UpdatedAt=DateTime.UtcNow;}
   try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict();}await transaction.CommitAsync(ct);return Results.Ok(rows.OrderBy(x=>x.SortOrder));
  });
  group.MapDelete("/placements/{id:guid}",async(Guid id,int revision,GhostWatchDbContext db,CancellationToken ct)=>{
   var row=await db.ChartPlacements.FindAsync([id],ct);if(row==null)return Results.NotFound();if(row.Revision!=revision)return Conflict();db.ChartPlacements.Remove(row);try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict();}return Results.NoContent();
  });
  group.MapGet("/placements/{id:guid}/data",async(Guid id,GhostWatchDbContext db,CancellationToken ct)=>{
   var placement=await db.ChartPlacements.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(placement==null)return Results.NotFound();var definition=await db.ChartDefinitions.AsNoTracking().SingleAsync(x=>x.Id==placement.ChartDefinitionId,ct);
   return Results.Ok(await ChartQuery.Execute(ChartValidation.Parse(definition.ConfigJson),placement.PageType=="Track"?placement.PageId:null,db,ct));
  });
 }
 private static ChartConfig Validate(ChartInput input){if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>120)throw new ChartValidationException("Provide a definition name up to 120 characters.");return ChartValidation.Parse(input.ConfigJson);}
 private static void ValidateWidth(string width){if(width is not("Small" or "Medium" or "Wide"))throw new ChartValidationException("Width must be Small, Medium or Wide.");}
 private static async Task ValidatePage(string type,Guid? id,GhostWatchDbContext db,CancellationToken ct){if(type=="Dashboard"&&id==null)return;if(type=="Track"&&id!=null&&await db.EconomyTracks.AnyAsync(x=>x.Id==id,ct))return;throw new ChartValidationException("Choose Dashboard without a page ID, or Track with an existing Track ID.");}
 private static IResult Conflict()=>Results.Problem(statusCode:409,title:"Charts or placements changed. Reload before retrying.");
}
