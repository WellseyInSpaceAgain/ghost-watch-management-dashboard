import {ChartArea} from './charts/chart-area';
import { Component, inject, signal } from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {DatePipe} from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import {requestError} from './tracks/track-api';
import {MetricPipe,ProgrammeSummary} from './economic-reporting';
interface AttentionItem {key:string;rule:string;message:string;path:string;}
interface AcknowledgedAttention {id:string;item:AttentionItem;acknowledgedAt:string;matches:boolean;}
interface OverviewData {summary:ProgrammeSummary;attention:AttentionItem[];acknowledged:AcknowledgedAttention[];activity:{name:string;kind:string;path:string;timestamp:string}[];}
@Component({selector:'app-overview',imports:[RouterLink,MatButtonModule,MetricPipe,DatePipe,ChartArea],template:`
<p class="eyebrow">ECONOMICS / OVERVIEW</p>
<div class="page-heading"><div><h1>Economic operations</h1><p class="muted">Plan the programmes. Track the work. Preserve what you learn.</p></div><a mat-flat-button routerLink="/tracks/new">Create Track</a></div>
@if(error()){<p role="alert" class="error">{{error()}} <button mat-button (click)="load()">Retry</button></p>}
@if(loading()){<p role="status">Loading programme state…</p>}
@if(data();as data){
<p><a mat-stroked-button routerLink="/snapshots">Take Snapshot / history</a></p>
<section class="metrics" aria-label="Programme metrics">@for(metric of headlines;track metric.key){<article class="metric"><h2>{{metric.label}}</h2><strong>{{data.summary.metrics[metric.key]|metric:metric.unit}}</strong><p>{{data.summary.metrics[metric.key]===null?metric.empty:metric.context}}</p></article>}</section>
<section class="panel"><div class="section-heading"><h2>Active Tracks</h2><a routerLink="/tracks">All Tracks →</a></div><p class="muted">Allocation is the Track's default pool allocation; shared pools are not summed across Tracks.</p>
<div class="table-wrap"><table><thead><tr><th>Track</th><th>Purpose</th><th>Default pool allocation</th><th>Track commitments</th><th>30d P/L</th><th>Status</th></tr></thead><tbody>
@for(track of activeTracks();track track.id){<tr><td><a [routerLink]="['/tracks',track.id]">{{track.name}}</a><small>{{track.poolName??'No default pool'}}</small></td><td>{{track.purpose}}</td><td>{{track.metrics['capitalAllocated']|metric}}</td><td>{{track.metrics['capitalCommitted']|metric}}</td><td>{{track.metrics['profit30d']|metric}}</td><td>{{track.status}}</td></tr>}
@empty{<tr><td colspan="6">No active programmes yet. Create a Track, then set its status to Active.</td></tr>}
</tbody></table></div></section>
<section class="panel"><h2>Needs Attention</h2><p class="muted">Rules check unassociated jobs, missing commercial sales, verdicts, pool commitments at 90% or more, active checklists, overdue objectives, over-allocation and wallet completeness.</p>
<p class="muted">Acknowledging accepts a finding until you restore it, including if it clears and returns. It does not change the underlying condition.</p>
@if(attentionError()){<p role="alert" class="error">{{attentionError()}}</p>}
@if(attentionStatus()){<p role="status">{{attentionStatus()}}</p>}
<ul aria-label="Active findings">@for(item of data.attention;track item.key){<li><a [routerLink]="path(item.path)" [queryParams]="query(item.path)">{{item.message}}</a> <button mat-button [disabled]="busy()||loading()" (click)="acknowledge(item)">Acknowledge</button></li>}@empty{<li>No unacknowledged findings currently match these rules.</li>}</ul>
<button mat-button [attr.aria-expanded]="showAcknowledged()" aria-controls="acknowledged-findings" (click)="showAcknowledged.set(!showAcknowledged())">{{showAcknowledged()?'Hide acknowledged':'Show acknowledged'}} ({{data.acknowledged.length}})</button>
@if(showAcknowledged()){
<div id="acknowledged-findings"><h3>Acknowledged findings</h3><ul aria-label="Acknowledged findings">
@for(ack of data.acknowledged;track ack.id){<li>
@if(ack.matches){<a [routerLink]="path(ack.item.path)" [queryParams]="query(ack.item.path)">{{ack.item.message}}</a>}@else{<span>{{ack.item.message}}</span>}
<small>Acknowledged {{ack.acknowledgedAt|date:'medium'}} · {{ack.matches?'Condition still matches':'Condition no longer matches'}}</small>
<button mat-button [disabled]="busy()||loading()" (click)="restore(ack)">Restore</button></li>
}@empty{<li>No acknowledged findings.</li>}
</ul></div>}
</section>
<section class="panel"><h2>Collected EVE finances</h2><p>Liquid wallets: <strong>{{data.summary.facts.liquid|metric}}</strong> · {{data.summary.facts.walletCount}}/{{data.summary.facts.characterCount}} collected {{data.summary.facts.walletsStale?'· Stale data':''}}</p><p>Buy commitments: {{data.summary.facts.marketBuyCommitments|metric}} · Sell-order listed value: {{data.summary.facts.sellOrderListedValue|metric}} {{data.summary.facts.ordersStale?'· Stale orders':''}}</p><p class="muted">Listed sell orders are not realised revenue. Programme profit comes from recorded completed Run actuals.</p></section>
<section class="panel"><h2>Recent activity</h2><ul>@for(item of data.activity;track $index){<li><a [routerLink]="path(item.path)" [queryParams]="query(item.path)">{{item.name}}</a> · {{item.kind}} · {{item.timestamp|date:'medium'}}</li>}@empty{<li>No recorded activity yet.</li>}</ul></section>
}
@defer(on viewport){<app-chart-area />}@placeholder{<section class="panel"><h2>Dashboard charts</h2></section>}
`})
export class Overview {
 private readonly http=inject(HttpClient);readonly data=signal<OverviewData|null>(null);readonly loading=signal(true);readonly error=signal('');
 readonly showAcknowledged=signal(false);readonly busy=signal(false);readonly attentionError=signal('');readonly attentionStatus=signal('');
 acknowledge(item:AttentionItem){this.changeAcknowledgement(this.http.post('/api/economics/attention/acknowledgements',{key:item.key}),'Finding acknowledged.');}
 restore(ack:AcknowledgedAttention){this.changeAcknowledgement(this.http.delete(`/api/economics/attention/acknowledgements/${ack.id}`),'Acknowledgement restored. Matching findings appear in Needs Attention.');}
 private changeAcknowledgement(request:import('rxjs').Observable<unknown>,message:string){
  this.busy.set(true);this.attentionError.set('');this.attentionStatus.set('');
  request.subscribe({next:()=>{this.busy.set(false);this.attentionStatus.set(message);this.load();},error:e=>{this.busy.set(false);this.attentionError.set(requestError(e));this.load();}});
 }
 readonly headlines=[{key:'coreCapital',label:'Core Capital',unit:'ISK',empty:'Configure a Core Capital pool',context:'Conceptual pool allocation'},{key:'profit30d',label:'30-day realised profit',unit:'ISK',empty:'No complete recorded results in this window',context:'Completed/evaluated Run actuals'},{key:'treasury',label:'Ghost Watch Treasury',unit:'ISK',empty:'Configure a Treasury pool',context:'Conceptual Treasury allocation'},{key:'replacementCoverage',label:'Replacement coverage',unit:'packages',empty:'Select a default package and Treasury pool',context:'Treasury / default package estimate'}];
 constructor(){this.load();}
 load(){this.loading.set(true);this.error.set('');this.http.get<OverviewData>('/api/economics/overview').subscribe({next:data=>{this.data.set(data);this.loading.set(false);},error:e=>{this.error.set(requestError(e));this.loading.set(false);}});}
 activeTracks(){return this.data()?.summary.tracks.filter(x=>x.status==='Active')??[];}
 path(value:string){return value.split('?')[0];}query(value:string){return Object.fromEntries(new URLSearchParams(value.split('?')[1]??''));}
}
