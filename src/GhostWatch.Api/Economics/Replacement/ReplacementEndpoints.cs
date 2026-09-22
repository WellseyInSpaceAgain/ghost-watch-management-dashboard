using GhostWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Replacement;
public sealed class ReplacementPackage
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public decimal EstimatedReplacementValue {get;set;}
    public bool IsDefault {get;set;}
    public string Notes {get;set;}="";
    public DateTime UpdatedAt {get;set;}=DateTime.UtcNow;
    public int Revision {get;set;}=1;
}
public sealed record ReplacementInput(string? Name,string? Description,decimal EstimatedReplacementValue,bool IsDefault,string? Notes,int Revision=0);
public static class ReplacementEndpoints
{
    public static void MapReplacement(this WebApplication app)
    {
        var group=app.MapGroup("/api/economics/replacement-packages");
        group.MapGet("/",async(GhostWatchDbContext db,CancellationToken ct)=> {
            var packages=await db.ReplacementPackages.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
            var treasury=await db.CapitalPools.AsNoTracking().SingleOrDefaultAsync(x=>x.Role=="Ghost Watch Treasury"&&x.ArchivedAt==null,ct);
            return Results.Ok(new { packages,treasury=treasury?.AllocatedCapital,treasuryName=treasury?.Name,coverage=packages.ToDictionary(x=>x.Id,x=>FinancialMath.Coverage(treasury?.AllocatedCapital,x.EstimatedReplacementValue)) });
        });
        group.MapPost("/",(ReplacementInput input,GhostWatchDbContext db,CancellationToken ct)=>Save(null,input,db,ct));
        group.MapPut("/{id:guid}",(Guid id,ReplacementInput input,GhostWatchDbContext db,CancellationToken ct)=>Save(id,input,db,ct));
    }
    private static async Task<IResult> Save(Guid? id,ReplacementInput input,GhostWatchDbContext db,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>120||input.Description?.Length>2000||input.Notes?.Length>20000||input.EstimatedReplacementValue<=0||input.EstimatedReplacementValue>1000000000000000m)
            return Results.Problem(statusCode:400,title:"Provide a name up to 120 characters and a positive estimated replacement value.");
        var row=id.HasValue?await db.ReplacementPackages.FindAsync([id.Value],ct):new ReplacementPackage();
        if(row is null)return Results.NotFound();
        if(id.HasValue&&row.Revision!=input.Revision)return Conflict();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try {
            // Clear the previous default before setting the new one, within the same transaction.
            if(input.IsDefault) {
                var defaults=await db.ReplacementPackages.Where(x=>x.IsDefault&&x.Id!=row.Id).ToListAsync(ct);
                foreach(var previous in defaults){previous.IsDefault=false;previous.Revision++;previous.UpdatedAt=DateTime.UtcNow;}
                await db.SaveChangesAsync(ct);
            }
            row.Name=input.Name.Trim();row.Description=input.Description??"";row.EstimatedReplacementValue=input.EstimatedReplacementValue;row.IsDefault=input.IsDefault;row.Notes=input.Notes??"";row.UpdatedAt=DateTime.UtcNow;
            if(id.HasValue)row.Revision++;else db.ReplacementPackages.Add(row);
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        }catch(DbUpdateConcurrencyException){return Conflict();}
        catch(DbUpdateException error) when(error.InnerException is Microsoft.Data.Sqlite.SqliteException {SqliteErrorCode:19}){return Conflict();}
        return id.HasValue?Results.Ok(row):Results.Created($"/api/economics/replacement-packages/{row.Id}",row);
    }
    private static IResult Conflict()=>Results.Problem(statusCode:409,title:"Replacement packages changed. Reload before saving.");
}
