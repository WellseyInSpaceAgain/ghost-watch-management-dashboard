using System.Text.Json;
using GhostWatch.Api.Economics.Reporting;
namespace GhostWatch.Api.Economics.Charts;
public static class ChartValidation
{
 public static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,MaxDepth=16};
 public static readonly string[] Types=["line","bar","stackedBar","pie","donut","kpi"];
 public static readonly string[] Aggregations=["sum","average","min","max","count","latest"];
 public static readonly string[] Formats=["isk","number","percent","days","count","ratio"];
 public static readonly string[] ProgrammeMeasures=["coreCapital","treasury","rdCapital","expansionCapital","profit30d","replacementValue","replacementCoverage","activeRuns","completedRuns","allocated","committed","liquid","marketBuyCommitments","sellOrderListedValue"];
 public static readonly Dictionary<string,ChartSource> Sources=new(){
  ["runs"]=new(["all","name","productName","trackName","status","runType","capitalPoolName","date","month"],["expectedCost","expectedRevenue","expectedProfit","actualCost","actualRevenue","actualProfit","margin","slotDays","profitPerSlotDay","capitalEfficiency","capitalTurnDays","timeToSellDays","committed","quantity"],["trackId","status","runType","capitalPoolId","product","from","to"]),
  ["tracks"]=new(["all","name","status","purpose"],EconomicReporting.Catalog.Select(x=>x.Key).ToArray(),["trackId","status"]),
  ["capitalPools"]=new(["all","name","role"],["allocated","committed","available","lifetimeProfit"],["capitalPoolId"]),
  ["snapshots"]=new(["all","date","month","name","trigger"],ProgrammeMeasures.Concat(EconomicReporting.Catalog.Select(x=>x.Key)).Distinct().ToArray(),["trackId","from","to"]),
  ["objectives"]=new(["all","name","type","status","trackName","date","month"],["manualProgress","checkedConditions","totalConditions"],["trackId","status","from","to"])
 };
 public const string Sample="""
 {
   "title": "Expected and actual profit by product",
   "description": "Recorded Run estimates compared with actual results",
   "type": "bar",
   "dataSource": "runs",
   "filters": { "status": ["Completed", "Evaluated"] },
   "x": { "field": "productName", "label": "Product" },
   "series": [
     { "field": "expectedProfit", "label": "Expected profit", "aggregation": "sum", "format": "isk" },
     { "field": "actualProfit", "label": "Actual profit", "aggregation": "sum", "format": "isk" }
   ],
   "timeRange": "30d"
 }
 """;
 public static ChartConfig Parse(string? json)
 {
  if(string.IsNullOrWhiteSpace(json)||json.Length>20000)throw new ChartValidationException("Provide chart JSON up to 20,000 characters.");
  ChartConfig config;
  try{config=JsonSerializer.Deserialize<ChartConfig>(json,JsonOptions)??throw new JsonException();}
  catch(JsonException error){throw new ChartValidationException($"Invalid chart JSON at {error.Path??"root"}. Check property names and value types; unknown properties are not allowed.");}
  if(string.IsNullOrWhiteSpace(config.Title)||config.Title.Length>160||config.Description?.Length>2000)throw new ChartValidationException("Provide a title up to 160 characters and description up to 2,000.");
  if(config.Type==null||!Types.Contains(config.Type))throw new ChartValidationException("Type must be line, bar, stackedBar, pie, donut or kpi.");
  if(config.DataSource==null||!Sources.TryGetValue(config.DataSource,out var source))throw new ChartValidationException("Choose a known dataSource: runs, tracks, capitalPools, snapshots or objectives.");
  if(config.X==null||!source.Dimensions.Contains(config.X.Field)||config.X.Label?.Length>120)throw new ChartValidationException($"x.field for {config.DataSource} must be one of: {string.Join(", ",source.Dimensions)}.");
  if(config.Series==null||config.Series.Length is <1 or >4||config.Series.Any(s=>s==null||!source.Measures.Contains(s.Field)||string.IsNullOrWhiteSpace(s.Label)||s.Label.Length>120||!Aggregations.Contains(s.Aggregation)||!Formats.Contains(s.Format)))throw new ChartValidationException($"Provide 1–4 series using supported measures ({string.Join(", ",source.Measures)}), aggregation and format.");
  if((config.Type is "pie" or "donut" or "kpi")&&config.Series.Length!=1)throw new ChartValidationException("Pie, donut and KPI charts use exactly one series.");
  if(config.Type=="kpi"&&config.X.Field!="all")throw new ChartValidationException("KPI charts require x.field = all.");
  if(config.TimeRange is not (null or "all" or "30d" or "90d" or "365d"))throw new ChartValidationException("timeRange must be all, 30d, 90d or 365d.");
  if(config.DataSource is "tracks" or "capitalPools" && config.TimeRange is not(null or "all"))throw new ChartValidationException("Current-state sources use timeRange all. Choose snapshots for history.");
  if(config.Filters is {} f){
   if(f.TrackId!=null&&f.TrackId!="CURRENT_TRACK"&&!Guid.TryParse(f.TrackId,out _))throw new ChartValidationException("filters.trackId must be a Track ID or CURRENT_TRACK.");
   if(f.Status?.Length>10||f.Status?.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>40)==true||f.RunType?.Length>80||f.Product?.Length>200||f.From>f.To)throw new ChartValidationException("Check filter values and date range; from cannot follow to.");
   var used=new Dictionary<string,bool>{{"trackId",f.TrackId!=null},{"status",f.Status!=null},{"runType",f.RunType!=null},{"capitalPoolId",f.CapitalPoolId!=null},{"product",f.Product!=null},{"from",f.From!=null},{"to",f.To!=null}};
   if(used.Any(x=>x.Value&&!source.Filters.Contains(x.Key)))throw new ChartValidationException($"Supported filters for {config.DataSource}: {string.Join(", ",source.Filters)}.");
   if(config.DataSource=="snapshots"){
    var allowed=f.TrackId==null?ProgrammeMeasures:EconomicReporting.Catalog.Select(x=>x.Key).ToArray();
    if(config.Series.Any(x=>!allowed.Contains(x.Field)))throw new ChartValidationException("Snapshot fields must match the chosen scope: programme metrics without trackId, Track metrics with trackId.");
   }
  }else if(config.DataSource=="snapshots"&&config.Series.Any(x=>!ProgrammeMeasures.Contains(x.Field)))throw new ChartValidationException("Track snapshot measures require filters.trackId.");
  return config;
 }
}
