# Configurable charts

Open **Chart library** to create a named shared definition. New definitions begin with a working expected/actual example. Edit JSON and choose an optional preview Track; the preview validates and refreshes after typing. Saving changes a shared definition everywhere it is used.

Dashboard and Track pages each have a chart area. Select a definition and **Add chart to page**. **Edit chart layout**, drag handles or use accessible Move up/down buttons, choose Small/Medium/Wide and **Save layout**. Cancel discards unsaved ordering/width edits. Removing a placement immediately removes it from that page; it does not delete the definition. Definitions in use cannot be deleted from the library.

The renderer uses [Chart.js](https://www.chartjs.org/docs/latest/getting-started/integration.html), importing only the required controllers/scales/plugins. Layout uses [Angular CDK drag/drop](https://angular.dev/guide/drag-drop). A data table accompanies every chart. Unknown values stay unknown, including missing points in charts. Pie/donut charts reject negative values; use bar/line for profit and loss.

## JSON contract

`docs/chart-config.schema.json` describes the structural schema. The backend additionally validates the exact source-specific fields and filters. `GET /api/economics/charts/schema` exposes the current catalog and example. Unknown properties, sources, measures, dimensions, aggregations and formats are rejected. No SQL, expressions or JavaScript callbacks are accepted or evaluated.

```json
{
  "title": "Profit by product",
  "description": "Completed Run estimates compared with actual results",
  "type": "bar",
  "dataSource": "runs",
  "filters": { "trackId": "CURRENT_TRACK", "status": ["Completed", "Evaluated"] },
  "x": { "field": "productName", "label": "Product" },
  "series": [
    { "field": "expectedProfit", "label": "Expected", "aggregation": "sum", "format": "isk" },
    { "field": "actualProfit", "label": "Actual", "aggregation": "sum", "format": "isk" }
  ],
  "timeRange": "30d"
}
```

Supported types: `line`, `bar`, `stackedBar`, `pie`, `donut`, `kpi`. Use one to four series; pie/donut/KPI requires exactly one. KPI requires `x.field: "all"`.

Aggregations: `sum`, `average`, `min`, `max`, `count`, `latest`. Count counts matching records, even when the selected measure is unknown. Other aggregates preserve unknowns; latest selects the most recent record's value, including null. Groups sort by their displayed label; date/month groups sort chronologically. Formats: `isk`, `number`, `percent`, `days`, `count`, `ratio`. Formatting changes presentation, not units or calculations.

Time ranges: `all` (default), `30d`, `90d`, `365d`. Explicit ISO date/time `filters.from` and `filters.to` are inclusive and interpreted in UTC. A rolling range intersects explicit dates. Runs use completion time when recorded, otherwise start time. Objectives use creation time. Snapshots use capture time. Current Tracks/Capital Pools require `all`; use snapshots for their history.

| Source | Group fields (in addition to `all`) | Measures | Filters |
|---|---|---|---|
| `runs` | name, productName, trackName, status, runType, capitalPoolName, date, month | expectedCost, expectedRevenue, expectedProfit, actualCost, actualRevenue, actualProfit, margin, slotDays, profitPerSlotDay, capitalEfficiency, capitalTurnDays, timeToSellDays, committed, quantity | trackId, status (array), runType, capitalPoolId, product (exact name, case insensitive), from, to |
| `tracks` | name, status, purpose | Track KPI catalog below | trackId, status (array) |
| `capitalPools` | name, role | allocated, committed, available, lifetimeProfit | capitalPoolId |
| `snapshots` | date, month, name, trigger | Stored programme or Track metrics below | trackId, from, to |
| `objectives` | name, type, status, trackName, date, month | manualProgress, checkedConditions, totalConditions | trackId, status (array), from, to |

Track KPI measures: `profit30d`, `lifetimeProfit`, `expectedProfit`, `capitalAllocated`, `capitalCommitted`, `capitalAvailable`, `slotDays`, `profitPerSlotDay`, `capitalEfficiency`, `capitalTurnDays`, `timeToSellDays`, `rdSpend`, `completedRuns`, `activeRuns`, `internalisation`. See `financial-metrics.md` for formulas and shared-pool semantics.

Programme snapshot measures: `coreCapital`, `treasury`, `rdCapital`, `expansionCapital`, `profit30d`, `replacementValue`, `replacementCoverage`, `activeRuns`, `completedRuns`, `allocated`, `committed`, `liquid`, `marketBuyCommitments`, `sellOrderListedValue`.

Snapshot charts without `trackId` use stored programme metrics. With `trackId`, they use the stored matching Track's metrics; snapshots predating that Track are omitted. `CURRENT_TRACK` resolves only from a Track placement or explicit preview context. Use a literal Track UUID for a Dashboard definition targeting one Track. Context never silently widens a filter to the whole programme.

Historical metrics represent **state at capture**, not disjoint accounting periods. Prefer `latest` per date/month for capital balances, counts or rolling 30-day profit. Summing overlapping snapshots is usually not meaningful. No current balances are substituted for historical unknowns.

Limits: 20,000 JSON characters, four series, 10,000 source records, 200 groups and 30 placements per page. Oversized queries are rejected instead of silently dropping records. Five explicit sources provide v1 charts; raw industry-job/order chart sources are deferred because those were candidates, not required data-source commitments. Their factual tables and programme order totals remain available.

## Capital-efficiency measure

`capitalEfficiency` is available on `runs`, `tracks` and Track-scoped `snapshots`. Run values are calculated centrally from actual profit, slot-days and explicit Capital Tied Up. Missing inputs remain null through chart queries and rendering. Track values use the weighted realised cohort documented in [Financial metrics](financial-metrics.md); historical snapshots use stored values only.

Use `format: "ratio"` to display the direct ratio with the unit `ISK/slot-day/ISK` and up to eight significant digits. Existing formats and saved definitions remain supported unchanged. A Run comparison can use:

```json
{
  "title": "Product-test capital efficiency",
  "type": "bar",
  "dataSource": "runs",
  "x": { "field": "name", "label": "Run" },
  "series": [{ "field": "capitalEfficiency", "label": "Profit / Slot-Day / ISK Tied Up", "aggregation": "latest", "format": "ratio" }],
  "timeRange": "all"
}
```

`latest` reports the latest Run within each named group; use unique Run names for individual comparisons. Existing Run chart aggregations keep their literal semantics: `average` is an arithmetic mean of Run ratios, `sum` a sum of ratios, and neither represents the weighted Track KPI. For aggregate Track performance use the `tracks` source (dimension `name`, measure `capitalEfficiency`, aggregation `latest`, format `ratio`). As with other chart fields, `count` counts records, not known financial values.
