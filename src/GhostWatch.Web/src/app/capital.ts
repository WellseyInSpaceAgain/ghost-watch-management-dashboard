import { ActivatedRoute } from '@angular/router';
import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { requestError } from './tracks/track-api';
export interface Pool { id: string; name: string; description: string; role: string; allocatedCapital: number; targetCapital: number | null; archivedAt: string | null; revision: number; }
interface CapitalState { metrics:Record<string, {committed:number|null;available:number|null;lifetimeSpend:number|null;lifetimeRevenue:number|null;lifetimeProfit:number|null}>; pools: Pool[]; roles: string[]; allocated: number; liquid: number | null; knownLiquid: number; overAllocated: number | null; walletCount: number; characterCount: number; walletsStale: boolean; adjustments: {id:string; date:string; fromName:string; toName:string; amount:number; reason:string; notes:string}[]; }
@Component({selector:'app-capital', imports:[FormsModule, DatePipe, DecimalPipe, MatButtonModule], template:`
  <p class="eyebrow">ECONOMICS / CAPITAL</p><h1>Capital Pools</h1><p class="muted">Conceptual allocations over real character wallets. Adjustments here never transfer ISK in EVE.</p>
  @if (error()) { <p role="alert" class="error">{{ error() }} <button mat-button (click)="load()">Reload capital</button></p> }
  @if (state(); as state) {
    <section class="panel"><h2>Allocation and liquidity</h2><p>Conceptually allocated: <strong>{{ state.allocated | number:'1.2-2' }} ISK</strong></p>
      @if (state.liquid !== null) { <p>Collected liquid wallets: {{ state.liquid | number:'1.2-2' }} ISK</p> } @else { <p class="notice">Wallet total incomplete: {{ state.walletCount }} of {{ state.characterCount }} characters collected. Known balances: {{ state.knownLiquid | number:'1.2-2' }} ISK. Overallocation cannot be determined yet.</p> }
      @if (state.walletsStale) { <p class="notice">Some wallet balances are stale or their latest refresh failed. Refresh characters before relying on this comparison.</p> }
      @if ((state.overAllocated ?? 0) > 0) { <p class="notice">Over-allocated: {{ state.overAllocated | number:'1.2-2' }} ISK. This is informational; intentional over-allocation is allowed.</p> }
    </section>
    <section class="panel"><div class="section-heading"><h2>Pools</h2><button mat-stroked-button (click)="reset()">New pool</button></div><div class="table-wrap"><table><thead><tr><th>Pool</th><th>Programme role</th><th>Allocated ISK</th><th>Committed / available ISK</th><th>Lifetime spend / revenue ISK</th><th>Lifetime P/L ISK</th><th>Target ISK</th><th></th></tr></thead><tbody>
      @for (pool of state.pools; track pool.id) { <tr><td>{{ pool.name }}<small>{{ pool.description }}{{ pool.archivedAt ? ' · Archived' : '' }}</small></td><td>{{ pool.role }}</td><td>{{ pool.allocatedCapital | number:'1.2-2' }}</td><td>{{ state.metrics[pool.id].committed === null ? 'Unknown' : (state.metrics[pool.id].committed | number:'1.2-2') }} / {{ state.metrics[pool.id].available === null ? 'Unknown' : (state.metrics[pool.id].available | number:'1.2-2') }}</td><td>{{ state.metrics[pool.id].lifetimeSpend === null ? 'Unknown' : (state.metrics[pool.id].lifetimeSpend | number:'1.2-2') }} / {{ state.metrics[pool.id].lifetimeRevenue === null ? 'Unknown' : (state.metrics[pool.id].lifetimeRevenue | number:'1.2-2') }}</td><td>{{ state.metrics[pool.id].lifetimeProfit === null ? 'Unknown' : (state.metrics[pool.id].lifetimeProfit | number:'1.2-2') }}</td><td>{{ pool.targetCapital === null ? 'Not set' : (pool.targetCapital | number:'1.2-2') }}</td><td><button mat-button (click)="edit(pool)">Edit {{ pool.name }}</button></td></tr> } @empty { <tr><td colspan="8">No pools yet. Create a pool, then allocate capital below.</td></tr> }
    </tbody></table></div></section>
    <form class="panel compact-form" #poolForm="ngForm" (ngSubmit)="savePool()"><h2>{{ draft.id ? 'Edit pool' : 'New pool' }}</h2>
      <label>Pool name<input name="poolName" [(ngModel)]="draft.name" required maxlength="120"></label>
      <label>Description<textarea name="description" [(ngModel)]="draft.description" maxlength="2000" rows="2"></textarea></label>
      <label>Programme role<select name="role" [(ngModel)]="draft.role">@for (role of state.roles; track role) { <option>{{ role }}</option> }</select></label>
      <p class="muted">Programme roles identify headline capital and treasury metrics. Only one active pool can hold each named role.</p>
      <label>Target capital (ISK, optional)<input name="target" type="number" min="0" step="0.01" [(ngModel)]="draft.targetCapital"></label>
      <label class="check"><input name="archived" type="checkbox" [(ngModel)]="draft.archived">Archived</label>
      <p class="muted">Archiving retains history and allocation. Transfer capital out first if it should no longer be allocated.</p>
      <button mat-flat-button [disabled]="poolForm.invalid || busy()">Save pool</button>
    </form>
    <form class="panel compact-form" #transfer="ngForm" (ngSubmit)="adjust()"><h2>Allocate, release or transfer capital</h2>
      <label>From pool<select name="from" [(ngModel)]="movement.fromPoolId"><option [ngValue]="null">Unallocated</option>@for (pool of state.pools; track pool.id) { @if (!pool.archivedAt) { <option [ngValue]="pool.id">{{ pool.name }}</option> } }</select></label>
      <label>To pool<select name="to" [(ngModel)]="movement.toPoolId"><option [ngValue]="null">Unallocated</option>@for (pool of state.pools; track pool.id) { @if (!pool.archivedAt) { <option [ngValue]="pool.id">{{ pool.name }}</option> } }</select></label>
      <label>Amount (ISK)<input name="amount" type="number" min="0.01" step="0.01" required [(ngModel)]="movement.amount"></label>
      <label>Reason<input name="reason" required maxlength="500" [(ngModel)]="movement.reason"></label>
      <label>Adjustment notes<textarea name="notes" rows="2" maxlength="2000" [(ngModel)]="movement.notes"></textarea></label>
      <button mat-flat-button [disabled]="transfer.invalid || busy() || movement.fromPoolId === movement.toPoolId">Record adjustment</button>
    </form>
    @if (message()) { <p role="status">{{ message() }}</p> }
    <section class="panel"><h2>Adjustment history</h2><div class="table-wrap"><table><thead><tr><th>Date</th><th>From</th><th>To</th><th>Amount ISK</th><th>Reason</th></tr></thead><tbody>
      @for (row of state.adjustments; track row.id) { <tr><td>{{ row.date | date:'medium' }}</td><td>{{ row.fromName }}</td><td>{{ row.toName }}</td><td>{{ row.amount | number:'1.2-2' }}</td><td>{{ row.reason }}<small>{{ row.notes }}</small></td></tr> } @empty { <tr><td colspan="5">No adjustments recorded.</td></tr> }
    </tbody></table></div></section>
  }
`})
export class Capital {
  private readonly requested=inject(ActivatedRoute).snapshot.queryParamMap.get('edit');private initial=true;
  private readonly http = inject(HttpClient); readonly state = signal<CapitalState | null>(null); readonly error = signal(''); readonly message = signal(''); readonly busy = signal(false);
  draft = { id:'', name:'', description:'', role:'Other', targetCapital:null as number | null, archived:false, revision:0 };
  movement = { fromPoolId:null as string | null, toPoolId:null as string | null, amount:null as number | null, reason:'', notes:'' };
  constructor() { this.load(); }
  load() { this.http.get<CapitalState>('/api/economics/capital').subscribe({next: result => { this.state.set(result); this.error.set('');if(this.initial&&this.requested){const pool=result.pools.find(x=>x.id===this.requested);if(pool)this.edit(pool);}this.initial=false; }, error: error => this.error.set(requestError(error))}); }
  reset() { this.draft = { id:'', name:'', description:'', role:'Other', targetCapital:null, archived:false, revision:0 }; this.message.set(''); }
  edit(pool: Pool) { this.draft = {...pool, archived:!!pool.archivedAt}; this.message.set(''); }
  savePool() {
    this.busy.set(true); this.message.set('');
    const request = this.draft.id ? this.http.put<Pool>(`/api/economics/capital/pools/${this.draft.id}`, this.draft) : this.http.post<Pool>('/api/economics/capital/pools', this.draft);
    request.subscribe({next: pool => { this.edit(pool); this.message.set('Pool saved.'); this.busy.set(false); this.load(); }, error: error => { this.error.set(requestError(error)); this.busy.set(false); }});
  }
  adjust() { this.busy.set(true); this.message.set(''); this.http.post('/api/economics/capital/adjustments', this.movement).subscribe({next: () => { this.busy.set(false); this.movement.amount = null; this.movement.reason = ''; this.reset(); this.message.set('Capital adjustment recorded.'); this.load(); }, error: error => { this.error.set(requestError(error)); this.busy.set(false); }}); }
}
