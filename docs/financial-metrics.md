# Financial metrics

Manual estimates, actual results and EVE facts are separate. Blank costs/revenue are unknown; enter zero explicitly when it is known. An unknown operand makes its result unknown, including aggregates containing an incomplete Run. Empty financial performance windows are unknown; empty activity counts and commitments are zero.

`FinancialMath`, `RunMetrics` and `EconomicReporting` own the calculations. Angular formats returned values.

| Metric | Calculation |
|---|---|
| Cost | Input cost + job cost + other cost, independently for expected and actual |
| Profit | Revenue − cost, independently for expected and actual |
| Margin | Actual profit / actual revenue × 100; unknown for nonpositive revenue |
| Slot-days | Manufacturing hours × concurrent slots / 24 |
| Profit per slot-day | Actual profit / slot-days; unknown for zero or missing duration |
| Capital efficiency | Actual profit / slot-days / explicitly recorded Capital Tied Up; a ratio |
| Capital turn time | Completion − start, in days |
| Time to sell | User-recorded days |
| Run commitment | Active/Selling: complete actual cost, otherwise complete expected cost; unknown if neither is complete. Other statuses: zero |
| Pool available | Allocated − all Run commitments assigned to that pool |
| Realised profit | Actual profit of Completed/Evaluated Runs |
| 30-day realised profit | Realised Runs completed between now − 30 days and now |
| Track lifetime profit | All realised Run profit for that Track |
| Recorded R&D spend | Actual costs of Runs with purpose R&D; independent of verdict |
| Internalisation | Internal manual stages / selected manual stages × 100 |
| Replacement coverage | Active Treasury-role allocation / positive default package estimate |

Track capital allocation and availability refer to the **default pool**, including other Tracks using that pool. Track commitments refer to the Track's own Runs, including any assigned to other pools. The UI labels this distinction. Programme allocation totals sum pools directly and never sum the repeated Track references.

Selected Track key performance indicators (KPIs) come from the API catalog, with at most six selected per Track. They are manual choices and persist independently of EVE refresh. Pool roles identify programme concepts independently of their editable names.

Wallet and order totals require a collected section for every connected character. Stale retained values remain visibly labelled. Buy commitments are price × remaining volume; sell listed value uses the same multiplication for sell orders. Listed sell orders are not realised revenue.

Needs Attention rules are deterministic: unassociated retained industry jobs; completed commercial Runs without actual revenue; completed Runs without verdicts; pool commitments at least 90% of allocation; active incomplete checklists; overdue active objectives; conceptual over-allocation; missing/stale wallets. The UI links to the record needing attention. It never changes records or takes action automatically.

## Needs Attention acknowledgements

Rules always calculate from current data. The dashboard separates matching, unacknowledged findings from **Show acknowledged**. **Acknowledge** persists an explicit acceptance without changing capital, Tracks, Runs, Objectives, EVE facts or any financial calculation. The acknowledged list shows the timestamp and whether the condition still matches; **Restore** removes acceptance and brings back a finding only if it currently matches.

Identity is `rule:subject-type:subject-id`: Run and Objective GUIDs distinguish individual warnings, and pool utilisation uses the pool GUID even though its link is the shared capital page. Renames do not change identity. Different rules on the same entity remain independent. Over-allocation, wallet freshness and the existing aggregate unassociated-job count are programme-wide rules (`rule:programme:all`); acknowledging an aggregate accepts that programme-level condition, including changes in its count or affected characters. It does not create per-job or per-character acknowledgements.

Acceptance remains until explicitly restored, including after a condition clears and later recurs. This makes it independent of dashboard polling or whether anyone observed the clear interval. Cleared/deleted-subject findings remain inspectable as **Condition no longer matches**, using their saved context without a potentially broken link. If they recur, the current generated message is shown. New entities receive new GUIDs and cannot inherit acceptance from a deleted entity with the same name. The UI explains this persistent acceptance before the action.

`AttentionAcknowledgements` stores a unique stable key, rule, optional subject type/ID, UTC timestamp and saved message/link context. Human-readable text is not the identity. No note editor or arbitrary suppression rules are introduced. ESI refresh does not own or delete these rows.

- `GET /api/economics/attention` returns `active` and `acknowledged` lists, with a `matches` flag for each acknowledgement. The overview returns equivalent `attention` and `acknowledged` lists alongside unchanged economic summaries.
- `POST /api/economics/attention/acknowledgements` accepts `{ "key": "<generated finding key>" }`. It validates against currently generated findings in a transaction; missing/cleared/unsupported keys cannot create suppressions. Duplicate/stale actions return HTTP 409.
- `DELETE /api/economics/attention/acknowledgements/{id}` restores that acceptance. Each acceptance gets a new GUID, so a stale restore cannot delete a later acknowledgement of the same warning. Already removed IDs return HTTP 409.

## Snapshot history

Manual snapshots accept an optional name and note. Automatic snapshots use UTC calendar months, create the current month at application startup when absent, and check hourly thereafter. A unique nullable month key prevents duplicate automatic captures; manual snapshots have no month key. Missing historical months are not backfilled.

A transaction reads local programme state and persists versioned JSON values, including names, programme/pool/Track metrics, selected KPIs, factual completeness/staleness, replacement estimates/coverage and Objective/Gate state. History reads these stored values. There are no snapshot editing or deletion endpoints. Later renames, allocations, refreshed wallets or Run edits cannot change an earlier snapshot.

## Run accounting and capital efficiency

`Expected Cost = Expected Input Cost + Expected Job Cost + Expected Other Cost`

`Actual Cost = Actual Input Cost + Actual Job Cost + Actual Other Cost`

Expected/actual profit is the corresponding revenue minus this total. Job Cost is distinct from Other Cost; all three components are required. Blank Job Cost makes the corresponding cost/profit unknown, just like blank Input Cost or Other Cost. Explicit zero is accepted. Historical Runs receive null Expected Job Cost, Actual Job Cost and Capital Tied Up without inferring values or moving Other Cost. Their current calculated totals may therefore become unknown until the missing Job Cost is recorded; stored snapshot results are unchanged.

Capital Tied Up is explicitly recorded Run data for product-test capital-efficiency comparison. It is independent of total Run cost, Capital Pool allocation and Run commitment, even if a user records equivalent amounts. It has no inferred default and does not affect commitments. Active/Selling commitments continue to use complete actual cost, otherwise complete expected cost, now including Job Cost. Pool availability, lifetime spend/profit, realised/30-day profit and recorded R&D spend consequently include Job Cost through the same central calculations. R&D may still complete without revenue and retain an unknown profit.

`Capital Efficiency = Actual Profit / Slot Days / Capital Tied Up`

Equivalently: `Capital Efficiency = Profit Per Slot-Day / Capital Tied Up`.

The result is unknown when Actual Profit is unknown, Slot Days is missing or nonpositive (including missing/invalid hours or slots), or Capital Tied Up is missing or nonpositive. The API accepts null or nonnegative finite decimal amounts within the existing financial bound (10^15); it rejects negative, nonnumeric and out-of-range new inputs. Zero capital can be recorded but produces unknown efficiency. Zero profit produces zero efficiency; losses produce a negative ratio when both denominators are valid. No cost, commitment or pool value substitutes for Capital Tied Up.

The label **Profit / Slot-Day / ISK Tied Up** denotes a direct capital-efficiency ratio, not an ISK amount or a percentage. For example, `0.00025` means 0.00025 ISK profit per slot-day per ISK tied up. Ratio displays use up to eight significant digits so small values remain visible; Angular formats the API result without calculating it.

### Track aggregation

The selected Track KPI uses all Completed/Evaluated Runs, matching the existing lifetime realised-profit and completed-slot-day cohort:

`Track Capital Efficiency = sum(Actual Profit) / sum(Slot Days × Capital Tied Up)`

This is the weighted mean of Run efficiencies with each Run weighted by its capital × slot-days exposure. It is not an arithmetic average, nor total profit divided by total days and then total capital. The whole value is unknown for an empty cohort or if any included Run has an unknown/invalid efficiency input. Incomplete R&D Runs are not silently excluded. Planning/Active/Selling/Cancelled Runs are outside this realised cohort. KPI selection, Track reporting, Track chart measures and Track snapshot charts use this centrally calculated value.

### Snapshot compatibility

New captures use schema version 2, identifying the revised Job Cost totals and additional Track capital-efficiency metric. Version 1 and version 2 both read their stored JSON dictionaries directly. No migration edits snapshot payloads or versions, and reads never recalculate historical values. A Track capital-efficiency chart over a version 1 snapshot returns unknown for the absent key. Automatic monthly capture still returns an existing month's snapshot unchanged, even when its version is 1.
