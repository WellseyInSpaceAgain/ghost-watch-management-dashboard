import { Component, inject, input, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { RunView } from './runs';
import { requestError } from './tracks/track-api';
export interface IndustryJob { characterId:number;characterName:string;jobId:number;activityId:number;productName:string;productTypeId:number|null;blueprintTypeId:number;runs:number;status:string;startDate:string;endDate:string;runId:string|null;runName:string|null;trackId:string|null;trackName:string|null; }
@Component({selector:'app-industry-jobs',imports:[FormsModule,RouterLink,DatePipe,MatButtonModule],template:`
  @if (!runId()) { <p class="eyebrow">EVE DATA / EXECUTION</p><h1>Industry jobs</h1> }
  <section class="panel"><h2>{{ runId() ? 'Linked ESI jobs' : 'Collected industry jobs' }}</h2><p class="muted">Job state is factual EVE data. Associations and Run results are managed locally and survive refresh.</p>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button mat-button (click)="load()">Reload jobs</button></p> }
    @if(trackId){<p class="muted">Showing this Track’s associated jobs and unassociated jobs from its linked characters.</p>}
    @if (!runId()) { <label><input type="checkbox" [(ngModel)]="onlyUnassociated"> Unassociated only</label> }
    <div class="table-wrap"><table><thead><tr><th>Character / job</th><th>Activity / product</th><th>Status / dates</th><th>Run / Track</th><th>Association</th></tr></thead><tbody>
      @for (job of visible();track job.characterId + ':' + job.jobId) { <tr><td><a [routerLink]="['/characters',job.characterId]">{{job.characterName}}</a><small>Job {{job.jobId}}</small></td><td>{{activity(job.activityId)}} · {{job.productName}}<small>{{job.runs}} runs</small></td><td>{{job.status}}<small>{{job.startDate | date:'medium'}} → {{job.endDate | date:'medium'}}</small></td><td>@if(job.runId){<a [routerLink]="['/runs',job.runId]">{{job.runName}}</a><small><a [routerLink]="['/tracks',job.trackId]">{{job.trackName}}</a></small>}@else{Unassociated}</td><td>
        @if(job.runId){<button mat-button [disabled]="busy()" (click)="remove(job)">Remove association</button>}
        @else{<a mat-button routerLink="/runs/new" [queryParams]="{characterId:job.characterId,jobId:job.jobId,trackId:trackId}">Create Run</a><button mat-button (click)="selected.set(job)">Associate existing Run</button>}
      </td></tr> } @empty { <tr><td colspan="5">{{runId() ? 'No ESI jobs linked. Manual Runs do not require a job.' : 'No jobs match. Refresh characters to collect their industry history.'}}</td></tr> }
    </tbody></table></div>
    @if(selected();as job){<form class="compact-form" (ngSubmit)="associate()"><h3>Associate {{job.productName}}</h3><label>Existing Run<select name="run" [(ngModel)]="targetRun" required><option value="">Choose a Run</option>@for(view of runs();track view.run.id){<option [value]="view.run.id">{{view.run.name}} · {{view.trackName}}</option>}</select></label><button mat-flat-button [disabled]="!targetRun || busy()">Save association</button><button mat-button type="button" (click)="selected.set(null)">Cancel</button></form>}
    @if(runId()){<a routerLink="/industry-jobs">Find unassociated jobs →</a>}
  </section>`,
})
export class IndustryJobs implements OnInit {
  readonly runId=input<string|null>(null);readonly trackId=inject(ActivatedRoute).snapshot.queryParamMap.get('trackId');private readonly http=inject(HttpClient);readonly jobs=signal<IndustryJob[]>([]);readonly runs=signal<RunView[]>([]);readonly selected=signal<IndustryJob|null>(null);readonly error=signal('');readonly busy=signal(false);targetRun='';onlyUnassociated=false;
  ngOnInit(){this.load();}
  load(){this.http.get<IndustryJob[]>('/api/eve/industry-jobs',{params:this.runId()?{runId:this.runId()!}:this.trackId?{trackId:this.trackId}:{}}).subscribe({next:rows=>{this.jobs.set(rows);this.error.set('');},error:error=>this.error.set(requestError(error))});this.http.get<RunView[]>('/api/economics/runs').subscribe({next:rows=>this.runs.set(rows),error:error=>this.error.set(requestError(error))});}
  visible(){return this.jobs().filter(x=>!this.onlyUnassociated||!x.runId);}
  activity(id:number){return ({1:'Manufacturing',3:'Time research',4:'Material research',5:'Copying',8:'Invention',11:'Reaction'} as Record<number,string>)[id]??`Activity ${id}`;}
  associate(){const job=this.selected();if(!job||!this.targetRun)return;this.busy.set(true);this.http.post(`/api/economics/runs/${this.targetRun}/jobs`,{characterId:job.characterId,jobId:job.jobId}).subscribe({next:()=>{this.busy.set(false);this.selected.set(null);this.load();},error:error=>{this.error.set(requestError(error));this.busy.set(false);}});}
  remove(job:IndustryJob){this.busy.set(true);this.http.delete(`/api/economics/runs/${job.runId}/jobs/${job.characterId}/${job.jobId}`).subscribe({next:()=>{this.busy.set(false);this.load();},error:error=>{this.error.set(requestError(error));this.busy.set(false);}});}
}
