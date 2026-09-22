import { Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { requestError } from './tracks/track-api';
@Component({selector:'app-track-strategy',imports:[FormsModule,DecimalPipe,MatButtonModule],template:`
  <section class="panel"><h2>Production stages / internalisation</h2><p class="muted">A manual strategic measure: internal stages divided by selected stages. This is not recipe or production eligibility.</p>
    @if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload stages</button></p>}
    <p>Saved internalisation: {{percentage()===null?'Unknown — no stages selected':(percentage()|number:'1.0-2')+'%'}}</p>
    @if(ready()){<form class="compact-form" #form="ngForm" (ngSubmit)="save()"><fieldset><legend>Selected production stages</legend>
      @for(stage of stages;track $index;let index=$index){<div class="stage"><input [name]="'name'+index" [attr.aria-label]="'Stage '+(index+1)+' name'" [(ngModel)]="stage.name" required maxlength="120"><label class="check"><input type="checkbox" [name]="'internal'+index" [(ngModel)]="stage.internal">Internal</label><button mat-button type="button" (click)="stages.splice(index,1)">Remove stage {{index+1}}</button></div>}
      <button mat-stroked-button type="button" (click)="stages.push({name:'',internal:false})">Add stage</button></fieldset><button mat-flat-button [disabled]="form.invalid||busy()">Save stages</button>@if(saved()){<p role="status">Stages saved.</p>}</form>}
  </section>`,styles:`.stage{display:flex;align-items:center;gap:8px;margin:8px 0;}.stage>input{flex:1;}@media(max-width:600px){.stage{flex-wrap:wrap;}}`})
export class TrackStrategy implements OnInit {
  readonly trackId=input.required<string>();readonly changed=output<void>();private readonly http=inject(HttpClient);readonly error=signal('');readonly percentage=signal<number|null>(null);readonly ready=signal(false);readonly busy=signal(false);readonly saved=signal(false);stages:{name:string;internal:boolean}[]=[];revision=0;
  ngOnInit(){this.load();}
  load(){this.http.get<{stages:{name:string;internal:boolean}[];revision:number;internalisation:number|null}>(`/api/economics/tracks/${this.trackId()}/strategy`).subscribe({next:data=>{this.stages=data.stages;this.revision=data.revision;this.percentage.set(data.internalisation);this.ready.set(true);this.error.set('');},error:error=>this.error.set(requestError(error))});}
  save(){this.busy.set(true);this.saved.set(false);this.http.put<{revision:number;internalisation:number|null}>(`/api/economics/tracks/${this.trackId()}/strategy`,{stages:this.stages,revision:this.revision}).subscribe({next:data=>{this.revision=data.revision;this.percentage.set(data.internalisation);this.busy.set(false);this.saved.set(true);this.error.set('');this.changed.emit();},error:error=>{this.error.set(requestError(error));this.busy.set(false);}});}
}
