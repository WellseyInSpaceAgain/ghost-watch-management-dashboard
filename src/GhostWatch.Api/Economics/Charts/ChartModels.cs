using System.Text.Json.Serialization;
namespace GhostWatch.Api.Economics.Charts;
public sealed class ChartDefinition
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public string Name {get;set;}="";
 public string Title {get;set;}="";
 public string Description {get;set;}="";
 public string ConfigJson {get;set;}="{}";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 public DateTime UpdatedAt {get;set;}=DateTime.UtcNow;
 public int Revision {get;set;}=1;
}
public sealed class ChartPlacement
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public Guid ChartDefinitionId {get;set;}
 public string PageType {get;set;}="Dashboard";
 public Guid? PageId {get;set;}
 public int SortOrder {get;set;}
 public string Width {get;set;}="Medium";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 public DateTime UpdatedAt {get;set;}=DateTime.UtcNow;
 public int Revision {get;set;}=1;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChartConfig(string Title,string? Description,string Type,string DataSource,ChartFilters? Filters,ChartAxis X,ChartSeries[] Series,string? TimeRange="all");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChartFilters(string? TrackId=null,string[]? Status=null,string? RunType=null,Guid? CapitalPoolId=null,string? Product=null,DateTime? From=null,DateTime? To=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChartAxis(string Field,string? Label);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChartSeries(string Field,string Label,string Aggregation,string Format);
public sealed record ChartData(string Title,string? Description,string Type,string? XLabel,string[] Labels,ChartDataSeries[] Series,int RowCount,string? Notice);
public sealed record ChartDataSeries(string Label,string Format,decimal?[] Values);
public sealed record ChartSource(string[] Dimensions,string[] Measures,string[] Filters);
public sealed class ChartValidationException(string message):Exception(message);
