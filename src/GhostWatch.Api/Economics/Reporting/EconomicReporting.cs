using System.Text.Json;
using System.Text.Json.Nodes;
using GhostWatch.Api.Data;
using GhostWatch.Api.Economics.Runs;
using GhostWatch.Api.Economics.Planning;
using Microsoft.EntityFrameworkCore;
namespace GhostWatch.Api.Economics.Reporting;
public sealed class TrackKpiSelection
{
 public Guid TrackId {get;set;}
 public string KeysJson {get;set;}="[]";
 public int Revision {get;set;}=1;
}
public sealed record MetricDefinition(string Key,string Label,string Unit);
public sealed record TrackSummary(Guid Id,string Name,string Status,string Purpose,Guid? PoolId,string? PoolName,Dictionary<string,decimal?> Metrics,string[] SelectedKpis,int KpiRevision);
public sealed record PoolSummary(Guid Id,string Name,string Role,decimal Allocated,decimal? Committed,decimal? Available,decimal? LifetimeProfit,bool Archived);
public sealed record FactualTotals(decimal? Liquid,decimal KnownLiquid,int WalletCount,int CharacterCount,bool WalletsStale,decimal? MarketBuyCommitments,decimal? SellOrderListedValue,bool OrdersStale);
public sealed record ProgrammeSummary(DateTime Timestamp,Dictionary<string,decimal?> Metrics,FactualTotals Facts,List<PoolSummary> Pools,List<TrackSummary> Tracks,Guid? ReplacementPackageId,string? ReplacementPackageName,List<Objective> Objectives);
public sealed record AttentionItem(string Rule,string Message,string Path);
public sealed record ActivityItem(string Name,string Kind,string Path,DateTime Timestamp);
public static class EconomicReporting
{
 public static readonly MetricDefinition[] Catalog=[
  new("profit30d","30-day realised profit","ISK"),new("lifetimeProfit","Lifetime realised profit","ISK"),new("expectedProfit","Expected profit (open Runs)","ISK"),
  new("capitalAllocated","Default pool allocation","ISK"),new("capitalCommitted","Track committed capital","ISK"),new("capitalAvailable","Default pool available","ISK"),
  new("slotDays","Completed Run slot-days","days"),new("profitPerSlotDay","Profit per slot-day","ISK/day"),new("capitalEfficiency","Profit / Slot-Day / ISK Tied Up","ISK/slot-day/ISK"),new("capitalTurnDays","Average capital turn time","days"),new("timeToSellDays","Average time to sell","days"),
  new("rdSpend","Recorded R&D spend","ISK"),new("completedRuns","Completed Runs","count"),new("activeRuns","Active Runs","count"),new("internalisation","Internalisation","%")];
 public static async Task<ProgrammeSummary> Capture(GhostWatchDbContext db,DateTime now,CancellationToken ct)
 {
  var runs=await db.EconomicRuns.AsNoTracking().ToListAsync(ct);
  var pools=await db.CapitalPools.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
  var tracks=await db.EconomyTracks.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
  var strategies=await db.TrackStrategies.AsNoTracking().ToDictionaryAsync(x=>x.TrackId,ct);
  var selections=await db.TrackKpiSelections.AsNoTracking().ToDictionaryAsync(x=>x.TrackId,ct);
  var objectives=await db.Objectives.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
  var replacement=await db.ReplacementPackages.AsNoTracking().SingleOrDefaultAsync(x=>x.IsDefault,ct);
  var poolSummaries=pools.Select(pool=>{var matching=runs.Where(x=>x.CapitalPoolId==pool.Id).ToArray();var committed=FinancialMath.CompleteSum(matching.Select(x=>RunMetrics.Calculate(x).Committed));return new PoolSummary(pool.Id,pool.Name,pool.Role,pool.AllocatedCapital,committed,pool.AllocatedCapital-committed,Profit(matching.Where(RunMetrics.Realised)),pool.ArchivedAt!=null);}).ToList();
  var trackSummaries=tracks.Select(track=>{
   var matching=runs.Where(x=>x.TrackId==track.Id).ToArray();var completed=matching.Where(RunMetrics.Realised).ToArray();var completedMetrics=completed.Select(RunMetrics.Calculate).ToArray();
   var pool=poolSummaries.Find(x=>x.Id==track.DefaultCapitalPoolId);var lifetime=Profit(completed);var days=SumOrUnknown(completedMetrics.Select(x=>x.SlotDays));
   var kpis=new Dictionary<string,decimal?> {
    ["profit30d"]=Profit(completed.Where(x=>x.CompletedAt>=now.AddDays(-30)&&x.CompletedAt<=now)),["lifetimeProfit"]=lifetime,
    ["expectedProfit"]=SumOrUnknown(matching.Where(x=>x.Status is "Planning" or "Active" or "Selling").Select(x=>RunMetrics.Calculate(x).ExpectedProfit)),
    ["capitalAllocated"]=pool?.Allocated,["capitalCommitted"]=FinancialMath.CompleteSum(matching.Select(x=>RunMetrics.Calculate(x).Committed)),["capitalAvailable"]=pool?.Available,
    ["capitalEfficiency"]=RunMetrics.AggregateCapitalEfficiency(completed),["slotDays"]=days,["profitPerSlotDay"]=FinancialMath.PerSlotDay(lifetime,days),["capitalTurnDays"]=Average(completedMetrics.Select(x=>x.CapitalTurnDays)),["timeToSellDays"]=Average(completedMetrics.Select(x=>x.TimeToSellDays)),
    ["rdSpend"]=SumOrUnknown(matching.Where(x=>x.Purpose=="R&D").Select(x=>RunMetrics.Calculate(x).ActualCost)),["completedRuns"]=completed.Length,["activeRuns"]=matching.Count(x=>x.Status is "Active" or "Selling"),
    ["internalisation"]=strategies.TryGetValue(track.Id,out var strategy)?PlanningEndpoints.Internalisation(JsonSerializer.Deserialize<ProductionStage[]>(strategy.StagesJson)!):null
   };
   var selection=selections.GetValueOrDefault(track.Id);
   return new TrackSummary(track.Id,track.Name,track.Status,track.Purpose,pool?.Id,pool?.Name,kpis,selection is null?["profit30d","capitalCommitted"]:JsonSerializer.Deserialize<string[]>(selection.KeysJson)!,selection?.Revision??0);
  }).ToList();
  var facts=await Facts(db,now,ct);var treasury=poolSummaries.Find(x=>x.Role=="Ghost Watch Treasury"&&!x.Archived)?.Allocated;
  var metrics=new Dictionary<string,decimal?> {
   ["coreCapital"]=poolSummaries.Find(x=>x.Role=="Core Capital"&&!x.Archived)?.Allocated,["treasury"]=treasury,
   ["rdCapital"]=poolSummaries.Find(x=>x.Role=="T3 R&D"&&!x.Archived)?.Allocated,["expansionCapital"]=poolSummaries.Find(x=>x.Role=="Expansion Capital"&&!x.Archived)?.Allocated,
   ["profit30d"]=Profit(runs.Where(x=>RunMetrics.Realised(x)&&x.CompletedAt>=now.AddDays(-30)&&x.CompletedAt<=now)),["replacementValue"]=replacement?.EstimatedReplacementValue,["replacementCoverage"]=FinancialMath.Coverage(treasury,replacement?.EstimatedReplacementValue),
   ["activeRuns"]=runs.Count(x=>x.Status is "Active" or "Selling"),["completedRuns"]=runs.Count(RunMetrics.Realised),["allocated"]=pools.Sum(x=>x.AllocatedCapital),["committed"]=FinancialMath.CompleteSum(runs.Select(x=>RunMetrics.Calculate(x).Committed)),["liquid"]=facts.Liquid,["marketBuyCommitments"]=facts.MarketBuyCommitments,["sellOrderListedValue"]=facts.SellOrderListedValue
  };
  return new(now,metrics,facts,poolSummaries,trackSummaries,replacement?.Id,replacement?.Name,objectives);
 }
 public static decimal? SumOrUnknown(IEnumerable<decimal?> values){var rows=values.ToArray();return rows.Length==0?null:FinancialMath.CompleteSum(rows);}
 private static decimal? Profit(IEnumerable<EconomicRun> runs)=>SumOrUnknown(runs.Select(x=>RunMetrics.Calculate(x).ActualProfit));
 private static decimal? Average(IEnumerable<decimal?> values){var rows=values.ToArray();return rows.Length==0||rows.Any(x=>x==null)?null:rows.Average();}
 private static async Task<FactualTotals> Facts(GhostWatchDbContext db,DateTime now,CancellationToken ct)
 {
  var count=await db.EveCharacters.CountAsync(ct);var sections=await db.EveSections.AsNoTracking().Where(x=>x.Name=="wallet"||x.Name=="marketOrders").ToListAsync(ct);
  var wallets=sections.Where(x=>x.Name=="wallet"&&x.Json!=null).ToArray();var known=wallets.Sum(x=>JsonNode.Parse(x.Json!)!.GetValue<decimal>());
  var orders=sections.Where(x=>x.Name=="marketOrders"&&x.Json!=null).ToArray();decimal? buy=null,sell=null;
  if(count>0&&orders.Length==count){buy=0;sell=0;foreach(var section in orders)foreach(var order in JsonNode.Parse(section.Json!)!.AsArray()){var value=order!["price"]!.GetValue<decimal>()*order["volume_remain"]!.GetValue<decimal>();if(order["is_buy_order"]?.GetValue<bool>()==true)buy+=value;else sell+=value;}}
  return new(count>0&&wallets.Length==count?known:null,known,wallets.Length,count,wallets.Any(x=>x.Error!=null||x.UpdatedAt==null||x.UpdatedAt<now.AddDays(-1)),buy,sell,orders.Any(x=>x.Error!=null||x.UpdatedAt==null||x.UpdatedAt<now.AddDays(-1)));
 }
 public static async Task<List<AttentionItem>> Attention(GhostWatchDbContext db,ProgrammeSummary summary,CancellationToken ct)
 {
  var items=new List<AttentionItem>();
  var unassociated=await db.EveIndustryJobs.CountAsync(x=>!db.RunJobs.Any(link=>link.CharacterId==x.CharacterId&&link.JobId==x.JobId),ct);
  if(unassociated>0)items.Add(new("unassociated-jobs",$"{unassociated} ESI industry jobs are not associated with a Run.","/industry-jobs"));
  var runs=await db.EconomicRuns.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
  foreach(var run in runs.Where(RunMetrics.Realised)){
   if(run.Purpose=="Commercial"&&run.ActualRevenue==null)items.Add(new("missing-sales",$"{run.Name} completed but has no actual sales result.",$"/runs/{run.Id}"));
   if(run.Verdict=="No Verdict")items.Add(new("unevaluated-run",$"{run.Name} completed without a verdict.",$"/runs/{run.Id}"));
  }
  foreach(var pool in summary.Pools.Where(x=>!x.Archived&&x.Committed>0&&(x.Allocated==0||x.Committed>=x.Allocated*.9m)))items.Add(new("pool-utilisation",$"{pool.Name} has committed {(pool.Allocated==0?"more than its allocation":$"{pool.Committed/pool.Allocated:P0} of its allocation")}.","/capital"));
  foreach(var objective in summary.Objectives.Where(x=>x.Status=="Active")){
   if(JsonSerializer.Deserialize<ChecklistItem[]>(objective.ConditionsJson)!.Any(x=>!x.Done))items.Add(new("incomplete-checklist",$"{objective.Name} has incomplete checklist conditions.",$"/objectives?edit={objective.Id}"));
   if(objective.TargetDate<summary.Timestamp)items.Add(new("overdue-objective",$"{objective.Name} is past its target date.",$"/objectives?edit={objective.Id}"));
  }
  if(summary.Facts.Liquid is {} liquid&&summary.Metrics["allocated"]>liquid)items.Add(new("over-allocation","Conceptual allocations exceed collected liquid wallets.","/capital"));
  if(summary.Facts.WalletsStale||summary.Facts.WalletCount<summary.Facts.CharacterCount)items.Add(new("wallet-freshness","Some character wallets are missing or stale. Refresh characters before relying on totals.","/characters"));
  return items;
 }
}
