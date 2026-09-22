import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { forkJoin, of } from 'rxjs';
import { Run, RunView } from './runs';
import { IndustryJob, IndustryJobs } from './industry-jobs';
import { Pool } from './capital';
import { Track, requestError } from './tracks/track-api';
interface RunOptions {types:string[];purposes:string[];statuses:string[];verdicts:string[];}
type MoneyKey='expectedInputCost'|'expectedOtherCost'|'expectedRevenue'|'actualInputCost'|'actualOtherCost'|'actualRevenue';
@Component({selector:'app-run-detail',imports:[FormsModule,RouterLink,DecimalPipe,MatButtonModule,IndustryJobs],template:`
  <a routerLink="/runs" class="back-link">← Economic Runs</a><p class="eyebrow">ECONOMICS / RUN</p><h1>{{id ? draft.name : 'Create Run'}}</h1>
  @if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload</button></p>}
  @if(ready()){
    @if(sourceJob()){<p class="notice">Creating a Run associated with {{sourceJob()!.productName}} for {{sourceJob()!.characterName}}. Financial estimates remain yours to record.</p>}
    <form class="panel compact-form" #form="ngForm" (ngSubmit)="save()">
      <label>Run name<input name="name" [(ngModel)]="draft.name" required maxlength="120"></label>
      <div class="form-row"><label>Track<select name="track" [(ngModel)]="draft.trackId" (ngModelChange)="selectTrack()" required><option value="">Choose a Track</option>@for(track of tracks();track track.id){<option [value]="track.id">{{track.name}}</option>}</select></label>
      <label>Capital Pool<select name="pool" [(ngModel)]="draft.capitalPoolId"><option [ngValue]="null">None</option>@for(pool of pools();track pool.id){<option [ngValue]="pool.id">{{pool.name}}{{pool.archivedAt?' (archived)':''}}</option>}</select></label></div>
      <div class="form-row"><label>Run type<select name="type" [(ngModel)]="draft.runType">@for(value of options().types;track value){<option>{{value}}</option>}</select></label><label>Purpose<select name="purpose" [(ngModel)]="draft.purpose">@for(value of options().purposes;track value){<option>{{value}}</option>}</select></label></div>
      <div class="form-row"><label>Status<select name="status" [(ngModel)]="draft.status">@for(value of options().statuses;track value){<option>{{value}}</option>}</select></label><label>Verdict<select name="verdict" [(ngModel)]="draft.verdict">@for(value of options().verdicts;track value){<option>{{value}}</option>}</select></label></div>
      <div class="form-row"><label>Product name<input name="product" [(ngModel)]="draft.productName" maxlength="200"></label><label>Quantity<input name="quantity" type="number" min="0" step="any" [(ngModel)]="draft.quantity"></label></div>
      <div class="form-row"><label>Started at (UTC)<input name="start" type="datetime-local" required [ngModel]="draft.startedAt.slice(0,16)" (ngModelChange)="date('startedAt',$event)"></label><label>Completed at (UTC)<input name="complete" type="datetime-local" [ngModel]="draft.completedAt?.slice(0,16)" (ngModelChange)="date('completedAt',$event)"></label></div>
      <p class="muted">Blank financial fields mean unknown. Enter 0 only when the amount is known to be zero. An R&D or internal-supply Run can complete without sales revenue.</p>
      <fieldset><legend>Expected results</legend><div class="form-row">@for(field of expected;track field.key){<label>{{field.label}}<input [name]="field.key" type="number" min="0" step="0.01" [(ngModel)]="draft[field.key]"></label>}</div></fieldset>
      <fieldset><legend>Actual results</legend><div class="form-row">@for(field of actual;track field.key){<label>{{field.label}}<input [name]="field.key" type="number" min="0" step="0.01" [(ngModel)]="draft[field.key]"></label>}</div></fieldset>
      <details><summary>Duration and efficiency</summary><div class="form-row"><label>Manufacturing duration (hours)<input name="hours" type="number" min="0" step="any" [(ngModel)]="draft.manufacturingHours"></label><label>Concurrent slots<input name="slots" type="number" min="1" max="1000" step="1" [(ngModel)]="draft.concurrentSlots"></label><label>Time to sell (days)<input name="sellDays" type="number" min="0" step="any" [(ngModel)]="draft.timeToSellDays"></label></div></details>
      <label>Run notes<textarea name="notes" rows="6" maxlength="20000" [(ngModel)]="draft.notes"></textarea></label>
      <button mat-flat-button [disabled]="form.invalid || busy()">{{id?'Save Run':'Create Run'}}</button>@if(saved()){<p role="status">Run saved.</p>}
    </form>
    @if(view();as view){<section class="panel"><h2>Saved financial results</h2><p class="muted">Calculated by the backend from the saved inputs. Active/Selling Runs commit complete actual costs where available, otherwise complete expected costs.</p><div class="table-wrap"><table><thead><tr><th>Metric</th><th>Value</th></tr></thead><tbody>@for(metric of metrics;track metric.key){<tr><td>{{metric.label}}</td><td>{{view.financials[metric.key]===null?'Unknown':(view.financials[metric.key] | number:'1.2-2')}}</td></tr>}</tbody></table></div></section>}
    @if(id){<app-industry-jobs [runId]="id" />}
  }
`})
export class RunDetail {
  private readonly http=inject(HttpClient);private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);
  readonly id=this.route.snapshot.paramMap.get('id');readonly error=signal('');readonly ready=signal(false);readonly busy=signal(false);readonly saved=signal(false);readonly view=signal<RunView|null>(null);readonly sourceJob=signal<IndustryJob|null>(null);
  readonly tracks=signal<Track[]>([]);readonly pools=signal<Pool[]>([]);readonly options=signal<RunOptions>({types:[],purposes:[],statuses:[],verdicts:[]});
  draft:Run={id:'',name:'',trackId:'',capitalPoolId:null,runType:'Other',purpose:'Commercial',status:'Planning',productTypeId:null,productName:null,quantity:null,startedAt:new Date().toISOString(),completedAt:null,expectedInputCost:null,expectedOtherCost:null,expectedRevenue:null,actualInputCost:null,actualOtherCost:null,actualRevenue:null,manufacturingHours:null,concurrentSlots:null,timeToSellDays:null,verdict:'No Verdict',notes:'',revision:0};
  readonly expected:{key:MoneyKey;label:string}[]=[{key:'expectedInputCost',label:'Expected input cost (ISK)'},{key:'expectedOtherCost',label:'Expected other cost (ISK)'},{key:'expectedRevenue',label:'Expected revenue (ISK)'}];
  readonly actual:{key:MoneyKey;label:string}[]=[{key:'actualInputCost',label:'Actual input cost (ISK)'},{key:'actualOtherCost',label:'Actual other cost (ISK)'},{key:'actualRevenue',label:'Actual revenue (ISK)'}];
  readonly metrics=[{key:'expectedProfit',label:'Expected profit (ISK)'},{key:'actualProfit',label:'Actual profit (ISK)'},{key:'margin',label:'Actual margin (%)'},{key:'slotDays',label:'Slot days'},{key:'profitPerSlotDay',label:'Profit per slot-day (ISK)'},{key:'capitalTurnDays',label:'Capital turn time (days)'},{key:'timeToSellDays',label:'Time to sell (days)'},{key:'committed',label:'Committed capital (ISK)'}];
  constructor(){this.load();}
  date(key:'startedAt'|'completedAt',value:string){if(key==='startedAt')this.draft.startedAt=value?`${value}:00Z`:'';else this.draft.completedAt=value?`${value}:00Z`:null;}
  selectTrack(){this.draft.capitalPoolId=this.tracks().find(x=>x.id===this.draft.trackId)?.defaultCapitalPoolId??null;}
  load(){
    this.error.set('');
    forkJoin({options:this.http.get<RunOptions>('/api/economics/runs/options'),tracks:this.http.get<Track[]>('/api/economics/tracks?includeArchived=true'),capital:this.http.get<{pools:Pool[]}>('/api/economics/capital'),view:this.id?this.http.get<RunView>(`/api/economics/runs/${this.id}`):of(null),jobs:!this.id&&this.route.snapshot.queryParamMap.has('jobId')?this.http.get<IndustryJob[]>('/api/eve/industry-jobs'):of([])}).subscribe({next:result=>{
      this.options.set(result.options);this.tracks.set(result.tracks);this.pools.set(result.capital.pools);this.view.set(result.view);
      if(result.view)this.draft={...result.view.run};
      else if(this.route.snapshot.queryParamMap.has('jobId')){const job=result.jobs.find(x=>x.jobId===Number(this.route.snapshot.queryParamMap.get('jobId'))&&x.characterId===Number(this.route.snapshot.queryParamMap.get('characterId')));if(!job||job.runId){this.error.set('This ESI job is unavailable or already associated. Return to industry jobs.');return;}this.sourceJob.set(job);this.draft.name=`${job.productName} batch`.slice(0,120);this.draft.productName=job.productName;this.draft.productTypeId=job.productTypeId??job.blueprintTypeId;this.draft.startedAt=job.startDate;this.draft.runType=({1:'Manufacturing',3:'Research',4:'Research',5:'Research',8:'Invention',11:'Reaction'} as Record<number,string>)[job.activityId]??'Other';this.draft.manufacturingHours=Math.max(0,(Date.parse(job.endDate)-Date.parse(job.startDate))/3600000);this.draft.concurrentSlots=1;}
      this.ready.set(true);
    },error:error=>this.error.set(requestError(error))});
  }
  save(){this.busy.set(true);this.saved.set(false);const job=this.sourceJob();const body={run:{...this.draft,id:undefined},job:job?{characterId:job.characterId,jobId:job.jobId}:null};const request=this.id?this.http.put<{id:string}>(`/api/economics/runs/${this.id}`,body):this.http.post<{id:string}>('/api/economics/runs',body);request.subscribe({next:result=>{this.busy.set(false);if(!this.id)void this.router.navigate(['/runs',result.id]);else{this.saved.set(true);this.load();}},error:error=>{this.error.set(requestError(error));this.busy.set(false);}});}
}
