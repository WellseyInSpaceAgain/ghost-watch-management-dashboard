import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DecimalPipe, DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { requestError } from './tracks/track-api';
export interface Run { id:string; name:string; trackId:string; playbookId?:string|null; capitalPoolId:string|null; runType:string; purpose:string; status:string; productTypeId:number|null; productName:string|null; quantity:number|null; startedAt:string; completedAt:string|null; expectedInputCost:number|null; expectedOtherCost:number|null; expectedRevenue:number|null; actualInputCost:number|null; actualOtherCost:number|null; actualRevenue:number|null; manufacturingHours:number|null; concurrentSlots:number|null; timeToSellDays:number|null; verdict:string; notes:string; revision:number; }
export interface RunView { run:Run; trackName:string; capitalPoolName:string|null; financials:Record<string,number|null>; }
@Component({selector:'app-runs', imports:[FormsModule, RouterLink, DecimalPipe, DatePipe, MatButtonModule], template:`
  <p class="eyebrow">ECONOMICS / EXECUTION</p><div class="page-heading"><h1>Economic Runs</h1><a mat-flat-button routerLink="/runs/new">Create Run</a></div>
  <p class="muted">Manual attempts, batches and experiments. ESI jobs can be associated without replacing local planning or results.</p>
  <label class="compact-form">Search Runs<input [(ngModel)]="query" placeholder="Run, Track, product or status"></label>
  @if (error()) { <p role="alert" class="error">{{ error() }} <button mat-button (click)="load()">Reload Runs</button></p> }
  <div class="panel table-wrap"><table><thead><tr><th>Run</th><th>Track</th><th>Type / status</th><th>Expected profit ISK</th><th>Actual profit ISK</th><th>Verdict</th></tr></thead><tbody>
    @for (view of filtered(); track view.run.id) { <tr><td><a [routerLink]="['/runs', view.run.id]">{{ view.run.name }}</a><small>{{ view.run.productName }} · {{ view.run.startedAt | date:'mediumDate' }}</small></td><td><a [routerLink]="['/tracks', view.run.trackId]">{{ view.trackName }}</a></td><td>{{ view.run.runType }} · {{ view.run.status }}</td><td>{{ view.financials['expectedProfit'] === null ? 'Unknown' : (view.financials['expectedProfit'] | number:'1.2-2') }}</td><td>{{ view.financials['actualProfit'] === null ? 'Unknown' : (view.financials['actualProfit'] | number:'1.2-2') }}</td><td>{{ view.run.verdict }}</td></tr> }
    @empty { <tr><td colspan="6">No Runs match. Create a manual Run or start from an industry job.</td></tr> }
  </tbody></table></div>`,
})
export class Runs {
  private readonly http = inject(HttpClient); readonly rows=signal<RunView[]>([]); readonly error=signal(''); query='';
  constructor(){this.load();}
  load(){this.http.get<RunView[]>('/api/economics/runs').subscribe({next: rows=>{this.rows.set(rows);this.error.set('');},error:error=>this.error.set(requestError(error))});}
  filtered(){const query=this.query.toLowerCase();return this.rows().filter(x=>`${x.run.name} ${x.trackName} ${x.run.productName} ${x.run.status}`.toLowerCase().includes(query));}
}
