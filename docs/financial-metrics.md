# Financial metrics

Manual estimates, actual results and EVE facts are separate. Blank costs/revenue are unknown; enter zero explicitly when it is known. An unknown operand makes its result unknown, including aggregates containing an incomplete Run. Empty financial performance windows are unknown; empty activity counts and commitments are zero.

`FinancialMath`, `RunMetrics` and `EconomicReporting` own the calculations. Angular formats returned values.

| Metric | Calculation |
|---|---|
| Cost | Input cost + other cost, independently for expected and actual |
| Profit | Revenue − cost, independently for expected and actual |
| Margin | Actual profit / actual revenue × 100; unknown for nonpositive revenue |
| Slot-days | Manufacturing hours × concurrent slots / 24 |
| Profit per slot-day | Actual profit / slot-days; unknown for zero or missing duration |
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
