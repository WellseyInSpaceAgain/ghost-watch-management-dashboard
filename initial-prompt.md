# Ghost Watch Management Dashboard

Build a new local application called:

**Ghost Watch Management Dashboard**

This application will initially focus on an **Economics Management Dashboard**, but the repository is deliberately broader than economics.

Long term, this project may grow into a wider Ghost Watch operations and management system.

Do not build those future modules now.

The immediate goal is to create a strong economics-management foundation that can coexist with future Ghost Watch functionality without requiring the whole product to be renamed or substantially restructured.

---

# Repository identity

The repository/project directory should be called:

```text
Ghost Watch Management Dashboard
```

Use **Ghost Watch Management Dashboard** as the overall application/product identity.

Treat **Economics** as the first major functional module.

A reasonable conceptual hierarchy is:

```text
Ghost Watch Management Dashboard

    Economics
        Dashboard
        Tracks
        Runs
        Capital Pools
        Objectives / Gates
        Playbooks
        Records
        Replacement Packages
        Snapshots

    EVE Data
        Characters
        Industry Jobs
        Assets
        Blueprints
        Wallets
        Market Orders
        Planetary Interaction

    Future Ghost Watch modules
        Not part of this implementation
```

Do not prematurely implement speculative future modules.

---

# Project creation strategy

Build **Ghost Watch Management Dashboard as a new application from a clean repository**.

Do NOT copy the EVE Economic Snapshot Exporter repository and then attempt to transform it into this application.

The existing **EVE Economic Snapshot Exporter** repository should instead be treated as a **read-only reference implementation**.

Its purpose is to show how working EVE Online integration has already been implemented and to provide examples of economic assessment/domain logic that may be useful here.

The new Ghost Watch application should have its own:

- solution/project structure
- database
- EF migrations
- Angular application
- configuration
- application identity
- domain model
- UI
- Git history

Do not attempt to migrate the old application's database or preserve its local application state.

It is acceptable for EVE characters to need authenticating again in the new application.

---

# Technology

Use the same general technology choices that have already worked successfully for the Economic Snapshot Exporter:

- .NET 10
- C#
- ASP.NET Core
- Angular 22
- Angular Material
- Entity Framework Core 10
- SQLite
- Docker / simple local deployment

Optimise for:

- useful workflows
- understandable code
- easy iteration
- good local UX
- preserving historical economic information
- straightforward debugging
- avoiding unnecessary enterprise architecture

This is a personal/local application.

It is not a production SaaS product.

Do not over-engineer it.

---

# Reference the Economic Snapshot Exporter

Before implementing EVE integration, inspect the existing **EVE Economic Snapshot Exporter** repository.

Treat that repository as read-only.

Do not modify it.

Use it to understand how the existing working application handles:

- EVE SSO
- OAuth authorisation
- PKCE
- callback handling
- token exchange
- refresh tokens
- local token persistence
- character identity
- multiple authenticated characters
- required ESI scopes
- ESI HTTP requests
- ESI error handling
- ESI pagination where applicable
- character refresh orchestration
- skill retrieval
- skill queue retrieval
- assets
- blueprints
- wallets
- market orders
- industry jobs
- planetary interaction
- standings
- loyalty points
- account grouping
- manually recorded Alpha/Omega state
- trained vs currently usable economic capacity
- economic character assessments
- asset categorisation
- blueprint categorisation
- useful material/stock categorisation

Do not guess how an ESI integration works if the reference repository already contains a working implementation.

Inspect it first.

Where useful, identify:

1. the ESI endpoint being called
2. the required scope
3. request/response DTOs
4. pagination behaviour
5. authentication requirements
6. refresh behaviour
7. persistence behaviour
8. error handling
9. important EVE-specific assumptions

Then implement the equivalent capability cleanly in Ghost Watch Management Dashboard.

If the exact location of the reference repository cannot be determined from the local workspace/environment, report that clearly rather than inventing a location.

---

# The reference repository is not the new architecture

Do not blindly reproduce the architecture of Economic Snapshot Exporter.

It solved a different problem.

The exporter was primarily character/snapshot-oriented.

Ghost Watch Management Dashboard is primarily an operations and management application whose Economics module centres on:

```text
Economy Tracks
Runs
Capital Pools
Playbooks
Records
Objectives
Economic Snapshots
Charts
```

The reference repository should therefore inform:

> How do we successfully obtain and interpret this information from EVE?

It should not dictate:

> How should Ghost Watch Management Dashboard itself be structured?

---

# Selective code reuse

Small, well-contained pieces of code may be ported from the Economic Snapshot Exporter where doing so is clearly preferable to rewriting already working logic.

Good candidates may include:

- ESI DTOs
- scope constants
- OAuth/PKCE helpers
- ESI client helpers
- pagination helpers
- ISK formatting
- character economic-capacity calculations
- asset categorisation logic
- blueprint categorisation logic

However:

- understand code before moving it
- only bring across functionality the new application actually needs
- adapt namespaces and naming immediately
- remove exporter-specific assumptions
- do not copy unused services/components
- do not copy old migrations
- do not copy the old database
- do not copy old build output
- do not copy configuration containing secrets
- do not copy the old `.git` directory
- do not carry `Economic Snapshot Exporter` branding into the new project

Prefer a small clean implementation informed by existing working code over wholesale repository copying.

---

# Git repository requirements

Create a completely new local Git repository.

The repository/project directory should be:

```text
Ghost Watch Management Dashboard
```

Initialise Git immediately, before substantial implementation:

```bash
git init
```

Configure the Git identity **locally for this repository only**:

```bash
git config user.name "WellseyInSpaceAgain"
git config user.email "wells3y@gmail.com"
```

Do not modify global Git configuration.

Verify:

```bash
git config --local user.name
git config --local user.email
```

Expected:

```text
WellseyInSpaceAgain
wells3y@gmail.com
```

---

# Git safety

Create an appropriate `.gitignore` before making the initial commit.

Do not commit:

- EVE access tokens
- EVE refresh tokens
- OAuth secrets
- SSO client secrets
- sensitive local configuration
- databases containing authentication credentials
- `node_modules`
- .NET build output
- Angular build output
- IDE state
- machine-specific files
- temporary/generated files that should not be source controlled

Do not copy private runtime data from the Economic Snapshot Exporter into the new repository.

---

# Git workflow

Use Git throughout implementation.

Do **not** build the entire application and produce one giant final commit.

Commit work regularly at logical boundaries.

A commit should represent a coherent piece of implementation that can reasonably be understood independently.

Good commit boundaries include things such as:

```text
Initialise Ghost Watch Management Dashboard
Add EVE SSO authentication
Add reusable ESI client infrastructure
Add character economic data refresh
Add economy track domain model
Add capital pool management
Add economic runs
Link ESI industry jobs to runs
Add playbook management
Add economic records
Add objectives and gates
Add replacement packages
Add snapshot history
Add reusable chart definitions
Add chart placements and drag ordering
Add monthly automatic snapshots
Add needs-attention rules
Add track dashboard UI
Add tests for economic calculations
```

These are examples, not a mandatory exact sequence.

Commit when:

- a coherent feature is working
- a meaningful refactor is complete
- a domain/database change forms a clean boundary
- a UI workflow becomes usable
- a testable subsystem is complete
- preparatory work should be isolated from the feature depending on it

Avoid commits mixing unrelated concerns.

Avoid meaningless messages such as:

```text
changes
updates
stuff
fix
wip
more work
```

Prefer concise descriptive messages such as:

```text
Add conceptual capital pool management
```

```text
Link ESI industry jobs to economic runs
```

```text
Add chart configuration validation
```

Where practical, before a feature-sized commit:

- build affected backend projects
- build the Angular frontend if relevant
- run relevant automated tests
- avoid knowingly committing a broken state

A feature may span several commits where that creates cleaner boundaries.

For example:

```text
Add chart definition persistence
Add chart config validation
Add reusable chart renderer
Add chart placement drag ordering
```

is preferable to one enormous chart-system commit.

---

# Product purpose

The existing Economic Snapshot Exporter primarily answers:

> What characters, skills, assets, blueprints, wallets and economic capabilities do I currently have?

The Economics module of Ghost Watch Management Dashboard should answer:

> What economic programmes am I running?

> What am I trying to accomplish?

> What capital have I allocated?

> What work is currently happening?

> What actually made or lost ISK?

> What have I learned?

> What requires attention?

> Can the economic engine currently fund Ghost Watch?

The application should combine:

1. factual ESI-derived state
2. manually maintained economic planning
3. manually enriched activity records
4. lightweight performance tracking
5. historical economic state
6. flexible notes and playbooks
7. configurable visualisations

Within the Economics module, the central planning concept should be an **Economy Track**, not a Character.

---

# Architectural principle: factual ESI data vs management data

Keep ESI factual state separate from locally managed economic information.

Conceptually:

```text
ESI factual data
+
Ghost Watch local management data
=
Economics Management Dashboard
```

Examples of factual ESI data:

- character skills
- skill queue
- industry job state
- wallet balances
- market orders
- assets
- blueprints
- PI colonies
- standings
- loyalty points

Examples of local management data:

- Economy Tracks
- Run annotations
- Playbooks
- Capital Pool allocations
- Objectives
- Records
- Verdicts
- Chart definitions
- Chart placements
- economic character assignments

An ESI refresh must never delete or overwrite local management information.

---

# EVE functionality required

The new application should support the factual EVE data required by the Economics module:

```text
Characters
Accounts / manual account grouping
User-set Alpha/Omega status

Skills
Skill Queue
Economic Capacity Assessment

Wallets
Market Orders

Industry Jobs
Blueprints
Assets

Planetary Interaction

Standings
Loyalty Points
```

Use the Economic Snapshot Exporter as the reference implementation for obtaining this information.

Do not port functionality merely because it exists in the exporter.

If an exporter feature does not support Ghost Watch Management Dashboard's current requirements, leave it behind.

---

# Character economic assessments

Inspect the latest Economic Snapshot Exporter implementation of economic character assessments and reproduce the useful behaviour.

In particular, preserve the distinction between:

```text
trained skill potential
```

and:

```text
currently usable skill potential
```

This is important for Alpha/Omega characters.

Useful assessment areas include:

```text
Manufacturing job capacity
Research job capacity
Reaction capacity
Market order capacity
PI colony potential
Trading foundation
Refining foundation
Hauling foundation
Invention foundation
```

Continue to distinguish broad skill foundations from recipe-specific eligibility.

For example:

```text
Strong invention foundation
```

must not imply:

```text
Can invent every T2/T3 item
```

unless exact recipe requirements have actually been checked.

---

# Character economic assignments

Allow local economic assignments independently of factual ESI capability.

Examples:

```text
Wellsey
Economic assignment: Economic Controller
```

```text
Ironman Dimaloun
Economic assignment: Research / Invention
```

```text
Ray Il Gunn
Economic assignment: Industrial Reserve
```

```text
Tradesey
Economic assignment: Industrial / Logistics Expansion
```

```text
Von Lupwig Ozran
Economic assignment: PI / Science Support
```

```text
Artemis Aggro
Economic assignment: None / PvP
```

These are user-defined planning labels.

Do not mix them into ESI-derived capability assessment.

Characters may link to multiple Tracks.

---

# Core domain entities

Implement the following as first-class concepts.

---

# 1. Economy Track

An Economy Track represents an ongoing economic strategy or programme.

Initial examples:

```text
Jita Trading
T2 Workshop
Project Tengu
Planetary Industry
Active ISK Injection
Ghost Watch Loot
```

Suggested fields:

```text
Id
Name
Description
Status
Purpose
CreatedAt
UpdatedAt
ArchivedAt nullable
DefaultCapitalPoolId nullable
Notes
```

Suggested statuses:

```text
Planning
Active
Paused
Completed
Archived
```

Suggested purposes:

```text
Cashflow
R&D
Strategic Supply
Background Income
Capital Growth
Other
```

These classifications are mainly metadata.

Do not build excessive business logic around them.

A Track should aggregate or link to:

- Runs
- Playbooks
- Records
- Characters
- Objectives
- Capital Pools
- selected KPIs
- Charts
- financial performance

The Track detail page should be one of the primary operational views in the application.

---

# 2. Playbook

A Playbook describes a repeatable economic procedure.

Examples:

```text
T2 Product Selection
T2 Module Production
T2 Invention
Project Tengu First Hull
T3 Reverse Engineering
T3 Component Production
Jita Station Trading
Loot Liquidation
```

Playbooks should primarily be flexible Markdown documents.

Suggested fields:

```text
Id
Name
Description
MarkdownBody
Tags
Status
CreatedAt
UpdatedAt
```

Allow links between Playbooks and:

- Tracks
- Characters
- Runs

A simple checklist is useful if easy to implement.

Do not build a workflow/BPM engine.

## Playbook revision history

Preserve simple revision history where practical.

For example:

```text
T2 Product Selection v1
T2 Product Selection v2
```

A revision should preserve the old Markdown and timestamp.

Do not implement branching, merging or Git-like version control.

---

# 3. Economic Run

A Run represents an actual economic attempt, batch or experiment.

Examples:

```text
MSE II Test Batch #1
Damage Control II Batch #3
First Tengu Hull
Jita Trading Experiment: X
PI Collection September 2026
Ghost Watch Loot Liquidation #4
```

Runs must support both ESI-assisted and completely manual creation.

---

# ESI-assisted Runs

An imported ESI industry job can be associated with a Run.

The ESI industry job remains factual EVE data.

Local management information is layered around it.

For example:

```text
ESI job:
10x Medium Shield Extender II manufacturing

Local management:
Track: T2 Workshop
Playbook: T2 Module Production
Run: MSE II Test Batch #1
Capital Pool: T2 Workshop
Purpose: Commercial
Expected Cost: ...
Expected Revenue: ...
Notes: ...
```

Allow one Run to link to one or more ESI industry jobs.

Provide actions to:

```text
Create Run from Job
Associate Job with Existing Run
Remove Association
```

ESI refreshes may update:

- job status
- completion time
- factual ESI values

They must not overwrite:

- Run
- Track
- Playbook
- annotations
- notes
- financial estimates
- verdict

---

# Manual Runs

Allow completely manual Runs with no ESI job.

Required use cases include:

- station trading
- hauling
- PI
- loot liquidation
- T3 R&D
- purchased intermediate materials
- strategic/internal supply
- miscellaneous economic experiments

Suggested fields:

```text
Id
Name

TrackId
PlaybookId nullable
CapitalPoolId nullable

RunType
Purpose
Status

ProductTypeId nullable
ProductName nullable
Quantity nullable

StartedAt
CompletedAt nullable

ExpectedInputCost nullable
ExpectedOtherCost nullable
ExpectedRevenue nullable
ExpectedProfit calculated where possible

ActualInputCost nullable
ActualOtherCost nullable
ActualRevenue nullable
ActualProfit calculated where possible

ManufacturingDuration nullable
SlotDays nullable
TimeToSell nullable

Verdict nullable
Notes

CreatedAt
UpdatedAt
```

Suggested Run Types:

```text
Manufacturing
Invention
Research
Reaction
Trading
PI
R&D
Strategic Supply
Loot
Other
```

Suggested purposes:

```text
Commercial
R&D
Strategic Supply
Internal Consumption
Other
```

Suggested statuses:

```text
Planning
Active
Completed
Selling
Evaluated
Cancelled
```

Suggested verdicts:

```text
Scale
Repeat
Retest
Drop
R&D Successful
R&D Failed
Internal Supply
No Verdict
```

Keep this system extensible without turning these enums into complicated workflows.

---

# ESI industry jobs

Industry jobs are particularly important.

Inspect how the Economic Snapshot Exporter currently queries and persists ESI industry jobs before implementing this functionality.

Preserve raw ESI job information.

Enhance the UI with local management information.

Suggested columns:

```text
Character
Activity
Product / Blueprint
Status
Start
End
Run
Track
Association State
```

Clearly show:

```text
Unassociated
```

where appropriate.

Provide quick actions:

```text
Create Run
Associate With Existing Run
```

The conceptual boundary is important:

```text
ESI Industry Job
    factual EVE state

Economic Run
    Ghost Watch management state
```

ESI jobs should act as the factual backbone for assisted Runs.

---

# 4. Capital Pool

Capital Pools are **conceptual management allocations** layered over actual wallet balances.

They are not physical wallets.

Initial examples:

```text
Core Capital
Ghost Watch Treasury
T2 Workshop
T3 R&D
Expansion Capital
```

Example:

```text
Actual liquid character wallets:
900m ISK

Conceptual allocation:
Core Capital: 550m
T2 Workshop: 150m
T3 R&D: 75m
Ghost Watch Treasury: 100m
Expansion Capital: 25m
```

Suggested fields:

```text
Id
Name
Description
AllocatedCapital
TargetCapital nullable
CreatedAt
UpdatedAt
ArchivedAt nullable
```

Provide calculated values where data supports them:

```text
Allocated
Committed
Available
Lifetime Spend
Lifetime Revenue
Lifetime Profit/Loss
```

Usually:

```text
Available = Allocated - Committed
```

Runs may reference a Capital Pool.

Tracks may have a default Capital Pool.

---

# Capital Pool adjustments

Allow manual conceptual transfers/adjustments.

Keep adjustment history.

Suggested model:

```text
Id
Date
FromPoolId nullable
ToPoolId nullable
Amount
Reason
Notes
CreatedAt
```

These adjustments do not modify EVE wallets.

They only modify conceptual economic allocation.

---

# Capital over-allocation

Compare total conceptual allocations with actual liquid wallet balances.

Do not prohibit over-allocation.

Instead warn clearly.

Example:

```text
Conceptual capital allocated: 1.20b
Current liquid wallets: 950m
Over-allocated: 250m
```

This is informational.

There may be intentional reasons to over-allocate, such as incoming sales or expected transfers.

---

# 5. Objective / Gate

Objectives represent milestones.

Examples:

```text
First profitable T2 batch
Establish one repeatable T2 product
First successful invention
First self-built Tengu hull
First complete Tengu plus subsystems
Internal T3 component production
Core Capital reaches 2.5b
Ghost Watch Treasury reaches one replacement package
```

A Gate is an Objective whose completion enables or suggests another action.

Example:

```text
Activate 1st Alt Account
```

Possible conditions:

```text
Core Capital >= 2.5b
30-day realised profit comfortably covers Omega
Second-account capacity has demonstrated demand
Expected incremental monthly profit >= 1.5x Omega cost
```

Suggested fields:

```text
Id
Name
Description
TrackId nullable
Type: Objective | Gate
Status
TargetDate nullable
ManualProgress nullable
CreatedAt
CompletedAt nullable
Notes
```

Support simple checklist conditions.

Do not build a generic rules/expression engine in v1.

Manual progress/checklist updates are acceptable.

Design the model so automatic conditions could be added later.

---

# 6. Record / Note

Create a flexible generic Record system.

This prevents every new kind of economic information from requiring a schema change.

Examples:

```text
Decision: Don't specialise Ray yet
Finding: MSE II market turnover too slow
Supplier note
Tengu sourcing note
Economic policy
Industry facility note
Market observation
Research note
```

Suggested fields:

```text
Id
Title
RecordType
MarkdownBody
Tags
CreatedAt
UpdatedAt
```

Records may link to:

- Tracks
- Runs
- Playbooks
- Characters
- Objectives
- Capital Pools

Keep the Markdown body intentionally flexible.

---

# 7. Replacement Package

Replacement Packages are Ghost Watch-specific abstractions.

A package represents the approximate replacement cost of a doctrine asset.

Examples:

```text
Standard Ghost Watch T3C
Faction Ghost Watch T3C
HIC
Support T3C
```

Suggested fields:

```text
Id
Name
Description
EstimatedReplacementValue
IsDefault
Notes
UpdatedAt
```

Primary calculation:

```text
Replacement Coverage =
Ghost Watch Treasury / Replacement Package Value
```

Example:

```text
Ghost Watch Treasury: 2.4b
Replacement Package: 800m

Replacement Coverage: 3.0
```

Replacement Coverage is one of the main programme-level metrics.

Do not implement zKillboard or automatic reimbursement integration in v1.

---

# 8. Economic Snapshot

Economic Snapshots preserve historical programme state.

This is critical because historical wealth cannot reliably be reconstructed later from current ESI values.

Support both:

- automatic monthly snapshots
- manual snapshots

---

# Automatic monthly snapshots

Automatically generate one programme snapshot per calendar month.

Prefer the first day of the month.

Do not generate duplicate automatic snapshots for the same month.

The application does not need to be running at an exact time.

If the application starts during a month and that month's automatic snapshot has not been created, it is acceptable to create it then.

Use an appropriate lightweight ASP.NET Core background service or existing application scheduling convention.

Do not introduce heavyweight job infrastructure solely for this feature.

---

# Manual snapshots

Provide an obvious:

```text
Take Snapshot
```

action.

Allow optional:

```text
Name
Note
```

Examples:

```text
Before activating Tradesey
First Tengu completed
Ghost Watch launch
Major capital reallocation
```

---

# Snapshot values

Snapshots should persist calculated values **at the moment the snapshot is created**.

Do not simply retain references and recalculate historical values using today's state.

Persist information such as:

```text
Timestamp
Trigger: AutomaticMonthly | Manual
Name nullable
Note nullable
```

Actual economic state:

```text
Total liquid ISK
Market buy commitments
Sell-order listed value
```

Capital:

```text
Capital Pool allocations
Capital Pool commitments
Core Capital
Ghost Watch Treasury
T3 R&D
Expansion Capital
```

Performance:

```text
30-day realised profit
Track-level profit/loss
Active Run count
Completed Run count
```

Track state:

```text
Capital allocated
Capital committed
Realised P/L
Selected KPI values
```

Ghost Watch state:

```text
Selected Replacement Package value
Replacement Coverage
```

Objectives:

```text
Objective/Gate progress
```

Use a sensible snapshot child-table/value structure or serialized snapshot values where appropriate.

Avoid one enormous fragile database table with a column for every possible metric.

Historical charts should use stored snapshot values.

---

# Inventory and blueprints

Use the Economic Snapshot Exporter as the reference for querying and categorising assets and blueprints.

Retain the useful distinction between:

```text
Available stock
Fitted / contained assets
Unknown availability
```

Avoid reintroducing categorisation mistakes already solved in the reference implementation.

The dashboard should allow planning objects to reference relevant inventory where practical.

For example, a Track may display relevant blueprint stock.

Do not build a complete inventory-reservation system in v1.

---

# Dashboard philosophy

The dashboard should be useful rather than decorative.

Avoid corporate dashboard theatre.

Do not create concepts such as:

```text
Economic Health Score
Strategic Alignment Score
Performance Index
Traffic-light executive score
```

unless they correspond to a genuinely useful user-defined metric.

A KPI belongs in the system only when looking at it can affect a decision.

---

# Main headline metrics

Keep the main dashboard headline area deliberately small.

Initial values:

```text
Core Capital
30-day Realised Profit
Ghost Watch Treasury
Replacement Coverage
```

If data is incomplete, show useful unknown/empty states rather than fake zeros.

---

# Main dashboard tables

## Active Tracks

Suggested columns:

```text
Track
Purpose
Allocated Capital
Committed Capital
30d P/L
Status
```

## Needs Attention

Create a deterministic **Needs Attention** section.

Do not use an LLM.

Useful examples include:

```text
2 ESI industry jobs are not associated with a Run
```

```text
MSE II Test Batch #1 completed but has no actual sales result
```

```text
T2 Workshop has used 92% of allocated capital
```

```text
An active Objective has incomplete checklist conditions
```

```text
Core Capital is 340m below the next Gate
```

```text
A completed Run has not been evaluated
```

Keep the initial rules small, transparent and useful.

---

# Economic metrics

Support a restrained set of useful metrics.

Initial metrics may include:

```text
Realised Profit
Expected Profit
Profit Margin
Capital Allocated
Capital Committed
Capital Available
Slot Days
Profit per Slot-Day
Capital Turn Time
Time to Sell
30-day Profit
Lifetime Track P/L
Replacement Coverage
T3 Internalisation Percentage
```

Do not force every Track to use every metric.

---

# Track-specific KPIs

Tracks should be able to select the small number of metrics relevant to them.

Examples:

## Jita Trading

```text
Realised Profit
Capital Committed
Capital Turn Time
```

## T2 Workshop

```text
Realised Profit
Profit per Slot-Day
Time to Sell
Capital Committed
```

## Project Tengu

```text
R&D Spend
T3Cs Completed
Internalisation %
Milestone Progress
```

## Planetary Industry

```text
30-day Contribution
Active Colonies
Last Collection
```

Keep KPIs concrete.

Do not turn this into a corporate scorecard framework.

---

# Financial calculations

Financial calculations should have one clear implementation location in the backend/domain/application layer.

Do not duplicate calculation logic in multiple Angular components.

Useful calculations include:

```text
Run Expected Profit
Run Actual Profit
Profit Margin
Slot Days
Profit per Slot-Day
Capital Pool Committed
Capital Pool Available
30-day Realised Profit
Track Lifetime P/L
Replacement Coverage
```

Where required information is missing, return:

```text
Unknown
Incomplete
Not Available
```

Do not silently interpret missing values as zero.

---

# Expected vs actual

Keep expected and actual financial values separate.

For example:

```text
Expected Cost
Actual Cost

Expected Revenue
Actual Revenue

Expected Profit
Actual Profit
```

Never silently overwrite an estimate with an actual result.

Charts should be capable of comparing expected vs actual values.

---

# Profit is not the same as success

Do not assume every Run should make money.

For example:

```text
Project Tengu First Hull
Actual P/L: -80m
Verdict: R&D Successful
```

is completely valid.

Financial results remain factual.

Run success is represented separately by the Verdict.

---

# T3 internalisation

Project Tengu needs a strategic progress measurement.

Support a manually configurable set of production stages.

Initial conceptual examples:

```text
T3 invention / reverse engineering
Hybrid polymer reactions
T3 component production
Hull assembly
Subsystem assembly
Material sourcing
```

Do not treat this list as authoritative recipe data.

Allow the Track/user to define the relevant stages and mark whether each is currently internal.

Calculate:

```text
Internalisation % =
Internal stages / selected stages
```

This is primarily a strategic progress measurement rather than a financial KPI.

Keep it manual/simple in v1.

---

# Configurable chart system

Charts must be reusable and user-configurable.

There must be a clear separation between:

```text
ChartDefinition
```

and:

```text
ChartPlacement
```

---

# ChartDefinition

A ChartDefinition represents **what a chart means**.

Suggested fields:

```text
Id
Name
Title
Description
ConfigJson
CreatedAt
UpdatedAt
```

---

# ChartPlacement

A ChartPlacement represents **where and how a ChartDefinition appears**.

Suggested fields:

```text
Id
ChartDefinitionId
PageType
PageId nullable
SortOrder
Width
CreatedAt
UpdatedAt
```

This must allow the same ChartDefinition to appear on multiple pages.

Initial Page Types:

```text
Dashboard
Track
```

Design the model so future page types can be added cleanly.

Potential future examples:

```text
CapitalPool
Character
ReplacementPackage
```

Do not implement them unless useful during this build.

Deleting a Placement must not automatically delete a shared ChartDefinition.

---

# Reusable chart renderer

Create one reusable chart rendering component/system.

Do not implement every chart as a bespoke Angular component.

Initial supported chart types:

```text
Line
Bar
Stacked Bar
Pie / Donut
Value / KPI Card
```

Use a sensible lightweight Angular-compatible chart library.

Do not write a chart engine from scratch.

---

# Chart JSON configuration

For v1, charts may be created/edited through JSON configuration.

Do not execute arbitrary JavaScript.

Do not expose arbitrary SQL.

The backend/application owns:

- valid data sources
- valid fields
- filters
- aggregations
- formatting
- chart types

The JSON only describes how known application data should be visualised.

Example:

```json
{
  "title": "T2 Profit by Product",
  "description": "Realised profit for completed T2 runs",
  "type": "bar",
  "dataSource": "runs",
  "filters": {
    "trackId": "CURRENT_TRACK",
    "status": ["Completed", "Evaluated"]
  },
  "x": {
    "field": "productName",
    "label": "Product"
  },
  "series": [
    {
      "field": "actualProfit",
      "label": "Profit",
      "aggregation": "sum",
      "format": "isk"
    }
  ],
  "timeRange": "30d"
}
```

Implement:

1. a typed application chart-config model
2. JSON validation
3. helpful validation errors
4. a sample/default configuration
5. live preview before saving
6. documentation of supported schema/fields

A standalone JSON Schema file is desirable if practical.

When creating a new chart, display the sample configuration as the initial/example value.

---

# Initial chart data sources

Support a small explicit list.

Initial candidates:

```text
runs
tracks
capitalPools
snapshots
industryJobs
marketOrders
objectives
```

Do not expose unrestricted database querying.

---

# Chart aggregations

Initial useful aggregations:

```text
sum
average
min
max
count
latest
```

Only add additional aggregations when there is an actual use case.

---

# Chart filters

Support useful simple filters such as:

```text
Track
Status
RunType
CapitalPool
Date Range
Product
```

Do not build a general-purpose query language.

---

# Track charts

Every Track detail page should have a configurable chart area.

Examples for **T2 Workshop**:

```text
Profit by Product
Profit per Slot-Day
Capital Committed Over Time
Time to Sell
```

Examples for **Project Tengu**:

```text
R&D Spend Over Time
T3 Internalisation %
Internal vs Purchased Value
T3Cs Completed
```

Do not automatically force the same charts onto every Track.

Charts should be user-created/configured.

---

# Main dashboard charts

The main Dashboard should also support ChartPlacements.

Useful default examples:

```text
Core Capital Over Time
Profit by Track
Capital Allocation
Monthly Realised Profit
```

Seed sensible defaults if doing so is straightforward.

Users must be able to:

- edit
- remove
- reorder
- reuse

these definitions/placements.

---

# Drag-and-drop chart ordering

Charts must be manually reorderable.

Use Angular CDK drag/drop where appropriate.

Persist chart order through `ChartPlacement.SortOrder`.

Support an explicit edit/layout mode if that improves usability.

Initial width options:

```text
Small
Medium
Wide
```

Persist Width as part of ChartPlacement.

Do not build a freeform Grafana-style canvas.

The required interaction is approximately:

```text
enter layout mode
drag
drop
save
```

The resulting layout should remain responsive.

---

# Fixed page content plus configurable charts

Do not turn every page into a blank dashboard designer.

Pages should combine:

```text
Purpose-built operational content
+
Configurable chart section
```

## Main Dashboard fixed content

Include:

- headline metrics
- Needs Attention
- Active Tracks
- recent relevant activity

Then:

- configurable charts

## Track Detail fixed content

Include:

- name
- description
- status
- purpose
- selected KPIs
- capital
- Objectives/Gates
- active Runs
- recent Runs
- Playbooks
- Characters
- Records
- relevant ESI jobs

Then:

- configurable Track charts

---

# Programme history

Economic Snapshots should power historical charts.

Examples:

```text
Core Capital Over Time
Ghost Watch Treasury Over Time
Replacement Coverage Over Time
Track Profit Over Time
Capital Allocation Over Time
```

Do not attempt to reconstruct past wealth from current wallet data.

Use values stored in historical Snapshots.

---

# Navigation

A reasonable initial navigation model is:

```text
Dashboard

Economics
    Tracks
    Runs
    Capital Pools
    Objectives / Gates

Knowledge
    Playbooks
    Records

Ghost Watch
    Replacement Packages

EVE Data
    Characters
    Industry Jobs
    Assets
    Blueprints
    Wallets
    Market Orders
    PI

History
    Snapshots

Settings
```

This exact hierarchy is not sacred.

Use good judgement if the Angular application has a cleaner way to express it.

Keep the application easy to navigate.

---

# Track detail page

The Track detail page should be one of the central operational screens.

Show approximately:

```text
Track name
Description
Status
Purpose
```

Selected KPIs:

```text
Current relevant metrics
```

Capital:

```text
Allocated
Committed
Available
Lifetime P/L
```

Planning:

```text
Objectives / Gates
```

Execution:

```text
Active Runs
Recent Runs
Relevant / Unassociated ESI jobs
```

Knowledge:

```text
Linked Playbooks
Linked Characters
Recent Records / decisions / findings
```

Visualisation:

```text
Configurable Charts
```

Make the page useful for actually operating the Track rather than merely viewing it.

---

# Run detail page

Show only relevant fields cleanly.

Approximately:

```text
Name
Track
Playbook
Capital Pool
Type
Purpose
Status

Product
Quantity
```

Expected:

```text
Input Cost
Other Cost
Revenue
Profit
```

Actual:

```text
Input Cost
Other Cost
Revenue
Profit
```

Calculated where possible:

```text
Margin
Slot Days
Profit per Slot-Day
Time to Sell
```

Also:

```text
Linked ESI Jobs
Notes
Verdict
```

An R&D Run must work without sales revenue.

---

# Main dashboard visual philosophy

Use charts only when charts improve comprehension.

Good uses include:

- trends over time
- comparing Tracks
- capital allocation
- product profitability
- historical growth

Tables should remain prominent where precise operational information is more useful.

Avoid filling the screen with graphs purely to look sophisticated.

The dashboard should feel like an **EVE operations console**, not corporate BI software.

---

# UI style

Prefer:

- dense but readable layouts
- practical tables
- useful charts
- compact forms
- restrained Angular Material usage
- clear ISK values
- Markdown content
- useful empty states
- good dark-mode behaviour
- actionable information
- clear separation of factual vs manual data

Avoid:

- enormous decorative cards
- excessive whitespace
- meaningless gauges
- unnecessary animation
- traffic-light scoring systems
- fake executive terminology
- excessive modal dialogs
- deeply nested navigation

Use readable abbreviated ISK formatting where appropriate:

```text
812.4m
2.54b
```

Allow precise values where needed.

---

# Development/sample data

Where useful, provide safe development fixtures.

Examples:

```text
Track: Jita Trading
Purpose: Capital Growth
```

```text
Track: T2 Workshop
Purpose: Cashflow
```

```text
Track: Project Tengu
Purpose: R&D
```

```text
Capital Pool: Core Capital
Capital Pool: Ghost Watch Treasury
Capital Pool: T2 Workshop
Capital Pool: T3 R&D
Capital Pool: Expansion Capital
```

Do not overwrite real user data.

If seeding the actual database is inappropriate, use a clearly separated development fixture mechanism.

---

# Database and migrations

This is a new application with a new database.

Create clean Entity Framework Core migrations for the new persistent model.

Do not copy migrations from the Economic Snapshot Exporter.

Do not copy its database.

Do not spend time trying to make its existing schema compatible with this application.

Use stable ESI identifiers where appropriate so factual EVE entities can be safely refreshed without interfering with local management entities.

---

# Manual data safety

Locally maintained economics information is valuable user data.

ESI refreshes must never remove or overwrite:

- Track associations
- Run annotations
- Capital Pool assignments
- Playbook links
- Records
- Objectives
- Verdicts
- manual financial values
- chart definitions
- chart placements
- economic character assignments

Use stable ESI identifiers plus local foreign keys appropriately.

---

# Testing

Add meaningful tests for new behaviour.

At minimum cover:

## Capital Pools

- allocated capital
- committed calculation
- available calculation
- adjustment history
- conceptual pool changes do not modify ESI wallet balances
- over-allocation warning

## Runs

- manual Run creation
- Run from ESI industry job
- associate existing Run with job
- remove job association
- ESI refresh preserves association
- expected profit calculation
- actual profit calculation
- expected and actual remain distinct
- missing financial information does not become zero
- R&D Run can complete with no revenue
- Slot Days handles missing duration safely

## Tracks

- Run aggregation
- financial summaries
- archived Track history retained
- selected KPI configuration persists

## Snapshots

- manual snapshot creation
- automatic monthly creation
- duplicate automatic monthly snapshot prevented
- historical values remain unchanged after current data changes

## Replacement Packages

- replacement coverage calculation
- missing package value handled safely

## Charts

- valid configuration accepted
- invalid fields rejected
- unsupported data source rejected
- arbitrary JavaScript rejected
- ChartDefinition may have multiple ChartPlacements
- placement deletion does not delete shared definition
- drag/drop updates SortOrder
- Width persists
- malformed JSON gives useful error
- preview uses validated config

## ESI boundary

- refresh updates factual job state
- refresh preserves local annotations
- local Track/Run relationships remain intact

## Needs Attention

Test implemented deterministic rules.

---

# Scope exclusions

Do NOT implement the following in this version:

- live market price feeds
- automated market profitability scanning
- full EVE SDE production dependency trees
- Ravworks-style production planning
- automatic optimal product selection
- automated skill-plan generation
- automated market trading
- automatic purchasing
- inventory reservation
- zKillboard integration
- Discord integration
- corporation wallet integration
- corporation asset integration
- automatic reimbursement
- full accounting ledger
- double-entry bookkeeping
- tax accounting
- AI-generated economic advice
- arbitrary SQL
- arbitrary JavaScript chart code
- Grafana-style freeform dashboards
- Jira/project-management functionality

Where natural, leave clean extension points.

Do not implement unused infrastructure merely because a future feature could theoretically require it.

---

# Implementation approach

Before large feature development:

1. create the clean Ghost Watch Management Dashboard project directory
2. initialise the new Git repository
3. configure repository-local Git identity
4. create `.gitignore`
5. create the .NET/Angular application structure
6. verify backend and frontend build
7. make an initial clean project commit
8. inspect the Economic Snapshot Exporter repository
9. document briefly which parts of it are useful as EVE integration references
10. inspect its EVE SSO implementation
11. implement EVE SSO cleanly in the new application
12. authenticate one character successfully
13. inspect its ESI client/refresh architecture
14. implement the minimum reusable ESI infrastructure needed
15. bring across required factual EVE capabilities incrementally
16. verify multiple characters can be authenticated/refreshed
17. implement the Ghost Watch Economics domain independently of the exporter architecture
18. continue through Tracks, Runs, Capital Pools, Playbooks, Records, Objectives, Snapshots and Charts
19. commit regularly at logical boundaries
20. keep the application runnable between major implementation stages

Prefer understanding and selectively reusing solved logic over blindly rewriting it.

Also prefer a clean new design over inheriting architectural baggage from the reference application.

---

# Suggested feature implementation order

Use judgement, but a sensible sequence is:

```text
1. Clean repository and project structure
2. EVE SSO
3. Character authentication/persistence
4. Reusable ESI client
5. Character refresh
6. Skills / economic assessments
7. Wallets / market orders
8. Industry jobs
9. Assets / blueprints
10. PI / standings / LP where required

11. Economy Tracks
12. Capital Pools
13. Runs
14. ESI industry-job associations
15. Playbooks
16. Records
17. Objectives / Gates
18. Replacement Packages
19. Financial calculations and Track summaries
20. Economic Snapshots
21. Monthly snapshot generation

22. ChartDefinition
23. Chart config validation
24. Reusable chart renderer
25. ChartPlacement
26. Drag/drop chart ordering

27. Main Dashboard
28. Track detail dashboard
29. Needs Attention
30. Tests and polish
```

This is a guide, not a requirement to implement the entire backend before creating usable UI.

Prefer vertical slices where useful.

Commit naturally throughout rather than waiting until the end.

---

# Completion criteria

The initial Economics module is successful when I can:

1. launch Ghost Watch Management Dashboard locally
2. authenticate multiple EVE characters
3. refresh those characters through ESI
4. see their current economic ESI data
5. see trained vs currently usable economic capability
6. manually group characters into accounts
7. manually set relevant account Alpha/Omega state
8. create Economy Tracks
9. link Characters to Tracks
10. create Playbooks
11. edit Playbooks as Markdown
12. create arbitrary Records/notes
13. create Objectives and Gates
14. create conceptual Capital Pools
15. adjust/transfer conceptual capital
16. compare conceptual allocations against real wallet balances
17. view ESI industry jobs
18. identify unassociated ESI jobs
19. create a Run from an ESI job
20. associate jobs with existing Runs
21. create completely manual Runs
22. record expected financial results
23. record actual financial results
24. record Run verdicts
25. calculate useful Run/Track metrics
26. create Replacement Packages
27. calculate Ghost Watch replacement coverage
28. take a manual Economic Snapshot
29. automatically generate a monthly Economic Snapshot
30. browse snapshot history
31. create a ChartDefinition using validated JSON
32. see example chart JSON when creating one
33. preview a chart before saving
34. place a ChartDefinition on the main Dashboard
35. place the same ChartDefinition on multiple pages
36. add Track-specific chart placements
37. drag and drop charts to reorder them
38. persist chart order
39. select chart width
40. see main dashboard headline values
41. see Active Tracks
42. see deterministic Needs Attention items
43. see configurable charts
44. operate a Track from its detail page
45. preserve all local management information through ESI refreshes

---

# Git completion criteria

By the end:

- the project is a new Git repository
- no Git history was copied from Economic Snapshot Exporter
- repository-local Git identity is:

```text
WellseyInSpaceAgain
wells3y@gmail.com
```

- global Git configuration was not modified
- sensitive authentication/runtime information is excluded
- implementation has been committed incrementally
- commits represent sensible feature/refactor boundaries
- the final repository does not consist of one giant implementation commit

---

# Final verification

Before declaring the work complete:

- build the backend
- build the frontend
- run automated tests
- fix failures caused by this work
- confirm EVE SSO works
- confirm ESI refresh works for multiple characters
- confirm ESI refresh does not overwrite local management data
- confirm chart configuration cannot execute arbitrary script/code
- confirm monthly snapshot generation cannot create duplicate monthly automatic snapshots
- confirm manually created snapshots remain independent historical records
- confirm application identity is consistently Ghost Watch Management Dashboard

---

# Final report

When finished, give me:

1. a concise architecture summary
2. the final repository/project structure
3. which functionality/code patterns were referenced or selectively ported from Economic Snapshot Exporter
4. all new database/domain entities
5. all EF migrations created
6. how ESI factual data is separated from local management data
7. how EVE SSO/token refresh works
8. which ESI endpoints/scopes are being used
9. how Runs and ESI industry jobs relate
10. how Capital Pools work over real wallet balances
11. how financial calculations are implemented
12. how Snapshots work
13. how monthly automatic snapshots are triggered
14. the ChartDefinition JSON schema/model
15. at least one valid example chart configuration
16. the ChartDefinition / ChartPlacement relationship
17. how drag/drop chart ordering is persisted
18. deterministic Needs Attention rules implemented
19. metrics/KPIs implemented
20. automated tests added
21. functionality deliberately deferred from v1
22. local build/run commands
23. confirmation that backend, frontend and tests succeed
24. confirmation of the repository-local Git username/email
25. confirmation that global Git configuration was not changed
26. the output of:

```bash
git log --oneline --decorate
```

so the implementation commit history can be reviewed.

---

# General design principle

This should become an **operations console for Ghost Watch**, with Economics as its first major management module.

It should not become QuickBooks, Jira or Grafana wearing an EVE skin.

For Economics, use:

```text
ESI
    factual EVE state

Tracks
    strategy

Runs
    execution

Playbooks
    repeatable process

Records
    accumulated knowledge

Capital Pools
    conceptual allocation

Objectives / Gates
    progress and expansion decisions

Replacement Packages
    operational meaning for Ghost Watch

Snapshots
    historical state

Charts
    useful visualisation
```

The Economic Snapshot Exporter is our **working EVE integration reference**.

It answers:

> How did we successfully get this information from EVE?

Ghost Watch Management Dashboard should use that knowledge to answer:

> How do we use this information to operate and grow Ghost Watch?

Do not rebuild solved EVE integration problems unnecessarily.

Do not inherit the solved application's architectural baggage unnecessarily either.

Keep the first version practical, inspectable and easy to extend.