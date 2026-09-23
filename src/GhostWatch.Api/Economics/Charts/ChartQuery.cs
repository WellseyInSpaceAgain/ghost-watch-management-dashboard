using System.Globalization;
using System.Text.Json;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Reporting;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Planning;
using GhostWatch.Api.Economics.Snapshots;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Charts;
public sealed record ChartRow(DateTime Timestamp,Dictionary<string,string> Dimensions,Dictionary<string,decimal?> Measures,Guid? TrackId=null,Guid? PoolId=null);
public static class ChartQuery
{
 public static async Task<ChartData> Execute(ChartConfig config,Guid? contextTrackId,GhostWatchDbContext db,CancellationToken ct)
 {
  var f=config.Filters;Guid? trackId=f?.TrackId=="CURRENT_TRACK"?contextTrackId:f?.TrackId is {} text?Guid.Parse(text):null;
  if(f?.TrackId=="CURRENT_TRACK"&&contextTrackId==null)throw new ChartValidationException("CURRENT_TRACK needs a Track page or a selected preview Track.");
  if(trackId!=null&&!await db.EconomyTracks.AnyAsync(x=>x.Id==trackId,ct))throw new ChartValidationException("The filtered Track no longer exists.");
  if(f?.CapitalPoolId is {} poolId&&!await db.CapitalPools.AnyAsync(x=>x.Id==poolId,ct))throw new ChartValidationException("The filtered Capital Pool no longer exists.");
  var now=DateTime.UtcNow;var rows=new List<ChartRow>();
  if(config.DataSource=="runs"){
   var runs=await db.EconomicRuns.AsNoTracking().Include(x=>x.Track).Include(x=>x.CapitalPool).OrderBy(x=>x.Id).Take(10001).ToListAsync(ct);
   foreach(var run in runs){var m=RunMetrics.Calculate(run);rows.Add(new(run.CompletedAt??run.StartedAt,new(){{"name",run.Name},{"productName",run.ProductName??"Product unspecified"},{"trackName",run.Track.Name},{"capitalPoolName",run.CapitalPool?.Name??"No pool"},{"status",run.Status},{"runType",run.RunType}},new(){{"expectedCost",m.ExpectedCost},{"expectedRevenue",run.ExpectedRevenue},{"expectedProfit",m.ExpectedProfit},{"actualCost",m.ActualCost},{"actualRevenue",run.ActualRevenue},{"actualProfit",m.ActualProfit},{"margin",m.Margin},{"slotDays",m.SlotDays},{"profitPerSlotDay",m.ProfitPerSlotDay},{"capitalEfficiency",m.CapitalEfficiency},{"capitalTurnDays",m.CapitalTurnDays},{"timeToSellDays",m.TimeToSellDays},{"committed",m.Committed},{"quantity",run.Quantity}},run.TrackId,run.CapitalPoolId));}
  }else if(config.DataSource is "tracks" or "capitalPools"){
   var summary=await EconomicReporting.Capture(db,now,ct);
   if(config.DataSource=="tracks")foreach(var track in summary.Tracks)rows.Add(new(now,new(){{"name",track.Name},{"status",track.Status},{"purpose",track.Purpose}},track.Metrics,track.Id,track.PoolId));
   else foreach(var pool in summary.Pools)rows.Add(new(now,new(){{"name",pool.Name},{"role",pool.Role}},new(){{"allocated",pool.Allocated},{"committed",pool.Committed},{"available",pool.Available},{"lifetimeProfit",pool.LifetimeProfit}},null,pool.Id));
  }else if(config.DataSource=="snapshots"){
   var snapshots=await db.EconomicSnapshots.AsNoTracking().OrderBy(x=>x.Timestamp).ThenBy(x=>x.Id).Take(10001).ToListAsync(ct);
   if(snapshots.Count>10000)throw new ChartValidationException("This source exceeds the v1 limit of 10,000 records.");
   foreach(var snapshot in snapshots){var values=SnapshotStore.Values(snapshot);var metrics=trackId==null?values.Metrics:values.Tracks.Find(x=>x.Id==trackId)?.Metrics;if(metrics==null)continue;rows.Add(new(snapshot.Timestamp,new(){{"name",snapshot.Name??(snapshot.Trigger=="Manual"?"Manual snapshot":"Monthly snapshot")},{"trigger",snapshot.Trigger}},metrics,trackId));}
  }else if(config.DataSource=="objectives"){
   var objectives=await db.Objectives.AsNoTracking().Include(x=>x.Track).OrderBy(x=>x.Id).Take(10001).ToListAsync(ct);
   foreach(var o in objectives){var checks=JsonSerializer.Deserialize<ChecklistItem[]>(o.ConditionsJson)!;rows.Add(new(o.CreatedAt,new(){{"name",o.Name},{"type",o.Type},{"status",o.Status},{"trackName",o.Track?.Name??"Programme"}},new(){{"manualProgress",o.ManualProgress},{"checkedConditions",checks.Count(x=>x.Done)},{"totalConditions",checks.Length}},o.TrackId));}
  }
  if(rows.Count>10000)throw new ChartValidationException("This source exceeds the v1 limit of 10,000 records.");
  var days=config.TimeRange switch{"30d"=>30,"90d"=>90,"365d"=>365,_=>0};var from=f?.From?.ToUniversalTime()??DateTime.MinValue;if(days>0&&now.AddDays(-days)>from)from=now.AddDays(-days);var to=f?.To?.ToUniversalTime()??DateTime.MaxValue;
  rows=rows.Where(x=>(trackId==null||x.TrackId==trackId)&&(f?.CapitalPoolId==null||x.PoolId==f.CapitalPoolId)&&(f?.Status==null||f.Status.Contains(x.Dimensions.GetValueOrDefault("status")))&&(f?.RunType==null||x.Dimensions.GetValueOrDefault("runType")==f.RunType)&&(f?.Product==null||string.Equals(x.Dimensions.GetValueOrDefault("productName"),f.Product,StringComparison.OrdinalIgnoreCase))&&x.Timestamp>=from&&x.Timestamp<=to).ToList();
  string Dimension(ChartRow row)=>config.X.Field switch{"all"=>"All records","date"=>row.Timestamp.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),"month"=>row.Timestamp.ToString("yyyy-MM",CultureInfo.InvariantCulture),_=>row.Dimensions[config.X.Field]};
  var groups=rows.GroupBy(Dimension).OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray();if(groups.Length>200)throw new ChartValidationException("More than 200 chart groups. Narrow the date range or use a broader grouping.");
  var series=config.Series.Select(s=>new ChartDataSeries(s.Label,s.Format,groups.Select(g=>Aggregate(g,s)).ToArray())).ToArray();
  if(config.Type is "pie" or "donut" && series.Any(s=>s.Values.Any(x=>x<0)))throw new ChartValidationException("Pie/donut charts cannot represent negative values. Use a bar or line chart for profit/loss.");
  return new(config.Title,config.Description,config.Type,config.X.Label,groups.Select(x=>x.Key).ToArray(),series,rows.Count,rows.Count==0?"No records match these filters.":series.Any(s=>s.Values.Any(x=>x==null))?"Some values are incomplete and remain unknown; missing inputs are never treated as zero.":null);
 }
 private static decimal? Aggregate(IEnumerable<ChartRow> rows,ChartSeries series)
 {
  var ordered=rows.OrderBy(x=>x.Timestamp).ToArray();if(series.Aggregation=="count")return ordered.Length;
  if(series.Aggregation=="latest")return ordered[^1].Measures.GetValueOrDefault(series.Field);
  var values=ordered.Select(x=>x.Measures.GetValueOrDefault(series.Field)).ToArray();if(values.Any(x=>x==null))return null;
  return series.Aggregation switch{"sum"=>values.Sum(),"average"=>values.Average(),"min"=>values.Min(),"max"=>values.Max(),_=>throw new ChartValidationException("Unsupported aggregation.")};
 }
}
