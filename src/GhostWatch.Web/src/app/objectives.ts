import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { Track, requestError } from './tracks/track-api';
export interface Objective { id:string;name:string;description:string;trackId:string|null;trackName?:string;type:string;status:string;targetDate:string|null;manualProgress:number|null;conditions:{label:string;done:boolean}[];notes:string;revision:number;completedAt?:string|null; }
@Component({selector:'app-objectives',imports:[FormsModule,DatePipe,RouterLink,MatButtonModule],template:`
  <p class="eyebrow">ECONOMICS / PLANNING</p><div class="page-heading"><h1>Objectives / Gates</h1><button mat-flat-button (click)="reset()">New objective</button></div>
  <p class="muted">Manual milestones and checklist conditions. Gates record readiness to act; they do not execute actions automatically.</p>
  @if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload objectives</button></p>}
  <section class="panel table-wrap"><table><thead><tr><th>Objective / Gate</th><th>Track</th><th>Status / target</th><th>Progress</th><th></th></tr></thead><tbody>
    @for(row of rows();track row.id){<tr><td>{{row.name}}<small>{{row.type}}</small></td><td>@if(row.trackId){<a [routerLink]="['/tracks',row.trackId]">{{row.trackName}}</a>}@else{Programme}</td><td>{{row.status}}<small>{{row.targetDate?(row.targetDate|date:'mediumDate'):'No target date'}}</small></td><td>{{row.manualProgress===null?'Unknown':row.manualProgress+'%'}}<small>{{checked(row)}}/{{row.conditions.length}} conditions checked</small></td><td><button mat-button (click)="edit(row)">Edit {{row.name}}</button></td></tr>}
    @empty{<tr><td colspan="5">No objectives yet. Record a milestone or an expansion Gate below.</td></tr>}
  </tbody></table></section>
  <form class="panel compact-form" #form="ngForm" (ngSubmit)="save()"><h2>{{draft.id?'Edit objective':'New objective'}}</h2>
    <label>Objective name<input name="name" [(ngModel)]="draft.name" required maxlength="120"></label>
    <label>Description<textarea name="description" rows="2" [(ngModel)]="draft.description" maxlength="2000"></textarea></label>
    <div class="form-row"><label>Type<select name="type" [(ngModel)]="draft.type"><option>Objective</option><option>Gate</option></select></label><label>Status<select name="status" [(ngModel)]="draft.status">@for(status of statuses;track status){<option>{{status}}</option>}</select></label></div>
    <label>Track<select name="track" [(ngModel)]="draft.trackId"><option [ngValue]="null">Programme-wide</option>@for(track of tracks();track track.id){<option [ngValue]="track.id">{{track.name}}</option>}</select></label>
    <div class="form-row"><label>Target date (UTC)<input name="targetDate" type="date" [ngModel]="draft.targetDate?.slice(0,10)" (ngModelChange)="draft.targetDate=$event? $event+'T00:00:00Z':null"></label><label>Manual progress (%)<input name="progress" type="number" min="0" max="100" [(ngModel)]="draft.manualProgress"></label></div>
    <fieldset><legend>Checklist conditions</legend>@for(condition of draft.conditions;track $index;let index=$index){<div class="condition"><input [name]="'done'+index" type="checkbox" [(ngModel)]="condition.done" [attr.aria-label]="'Condition '+(index+1)+' complete'"><input [name]="'label'+index" [(ngModel)]="condition.label" required maxlength="500" [attr.aria-label]="'Condition '+(index+1)"><button mat-button type="button" (click)="draft.conditions.splice(index,1)">Remove condition {{index+1}}</button></div>}<button mat-stroked-button type="button" (click)="draft.conditions.push({label:'',done:false})">Add condition</button></fieldset>
    <label>Objective notes<textarea name="notes" rows="4" maxlength="20000" [(ngModel)]="draft.notes"></textarea></label>
    <button mat-flat-button [disabled]="busy()||form.invalid">Save objective</button>@if(saved()){<p role="status">Objective saved.</p>}
  </form>`,styles:`.condition{display:flex;align-items:center;gap:8px;margin:8px 0;}.condition input[type=checkbox]{width:auto;}.condition input:not([type=checkbox]){flex:1;}`})
export class Objectives {
  private readonly http=inject(HttpClient);private readonly requested=inject(ActivatedRoute).snapshot.queryParamMap.get('edit');readonly rows=signal<Objective[]>([]);readonly tracks=signal<Track[]>([]);readonly error=signal('');readonly busy=signal(false);readonly saved=signal(false);readonly statuses=['Planning','Active','Completed','Cancelled'];
  draft:Objective={id:'',name:'',description:'',trackId:null,type:'Objective',status:'Active',targetDate:null,manualProgress:null,conditions:[],notes:'',revision:0};private initial=true;
  constructor(){this.load();this.http.get<Track[]>('/api/economics/tracks?includeArchived=true').subscribe({next:rows=>this.tracks.set(rows),error:error=>this.error.set(requestError(error))});}
  load(){this.http.get<Objective[]>('/api/economics/objectives').subscribe({next:rows=>{this.rows.set(rows);this.error.set('');if(this.initial&&this.requested){const found=rows.find(x=>x.id===this.requested);if(found)this.edit(found);}this.initial=false;},error:error=>this.error.set(requestError(error))});}
  reset(){this.draft={id:'',name:'',description:'',trackId:null,type:'Objective',status:'Active',targetDate:null,manualProgress:null,conditions:[],notes:'',revision:0};this.saved.set(false);}
  edit(row:Objective){this.draft=structuredClone(row);this.saved.set(false);}
  checked(row:Objective){return row.conditions.filter(x=>x.done).length;}
  save(){this.busy.set(true);this.saved.set(false);const request=this.draft.id?this.http.put<{id:string;revision:number}>(`/api/economics/objectives/${this.draft.id}`,this.draft):this.http.post<{id:string;revision?:number}>('/api/economics/objectives',this.draft);request.subscribe({next:result=>{this.draft.id=result.id;this.draft.revision=result.revision??1;this.saved.set(true);this.busy.set(false);this.load();},error:error=>{this.error.set(requestError(error));this.busy.set(false);}});}
}
