import {Component,effect,inject,input,signal} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {MatButtonModule} from '@angular/material/button';
import {CdkDrag,CdkDragHandle,CdkDropList,CdkDragDrop,moveItemInArray} from '@angular/cdk/drag-drop';
import {catchError,forkJoin,of} from 'rxjs';
import {ChartData,ChartRenderer} from './chart-renderer';
import type {ChartDefinition} from './chart-library';
import {requestError} from '../tracks/track-api';
interface Placement {id:string;chartDefinitionId:string;pageType:string;pageId:string|null;sortOrder:number;width:string;revision:number;}
@Component({selector:'app-chart-area',imports:[FormsModule,RouterLink,MatButtonModule,CdkDrag,CdkDragHandle,CdkDropList,ChartRenderer],template:`
<section class="panel"><div class="section-heading"><h2>{{pageType()==='Track'?'Track charts':'Dashboard charts'}}</h2><a routerLink="/charts" [queryParams]="{trackId:pageId()}">Chart library</a></div>
@if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload charts</button></p>}
@if(loading()){<p>Loading charts…</p>}
<form class="compact-form" (ngSubmit)="add()"><label>Chart definition<select name="definition" [(ngModel)]="selectedDefinition" required><option value="">Choose a shared definition</option>@for(definition of definitions();track definition.id){<option [value]="definition.id">{{definition.name}}</option>}</select></label><button mat-stroked-button [disabled]="!selectedDefinition||busy()||editing()">Add chart to page</button></form>
@if(placements().length){<div class="form-actions">@if(editing()){<button mat-flat-button [disabled]="busy()" (click)="saveLayout()">Save layout</button><button mat-button [disabled]="busy()" (click)="cancelLayout()">Cancel layout changes</button><p class="muted">Drag chart handles, or use Move up/down. Choose widths, then save.</p>}@else{<button mat-stroked-button (click)="editing.set(true);saved.set(false)">Edit chart layout</button>}</div>}
@if(saved()){<p role="status">Chart layout saved.</p>}
</section>
<div class="chart-grid" cdkDropList cdkDropListOrientation="mixed" [cdkDropListData]="placements()" [cdkDropListDisabled]="!editing()||busy()" (cdkDropListDropped)="drop($event)">
@for(placement of placements();track placement.id;let index=$index){<article class="panel chart-placement" data-testid="chart-placement" [attr.data-definition]="name(placement)" [attr.data-width]="placement.width" [class.small]="placement.width==='Small'" [class.wide]="placement.width==='Wide'" cdkDrag [cdkDragData]="placement">
@if(editing()){<div class="layout-controls"><button mat-button type="button" cdkDragHandle [attr.aria-label]="'Drag '+name(placement)">↕ Drag {{name(placement)}}</button><label>Width<select [attr.aria-label]="'Width for '+name(placement)" [(ngModel)]="placement.width"><option>Small</option><option>Medium</option><option>Wide</option></select></label><button mat-button [disabled]="index===0" (click)="move(index,-1)">Move up</button><button mat-button [disabled]="index===placements().length-1" (click)="move(index,1)">Move down</button><button mat-button [disabled]="busy()" (click)="remove(placement)">Remove placement</button></div>}
@if(chartErrors()[placement.id]){<p class="error" role="alert">{{name(placement)}}: {{chartErrors()[placement.id]}}</p>}
@else if(chartData()[placement.id];as data){<app-chart-renderer [data]="data" />}
@else{<p>Loading {{name(placement)}}…</p>}
<a routerLink="/charts" [queryParams]="{edit:placement.chartDefinitionId,trackId:pageId()}">Edit shared definition</a>
</article>}
</div>
@if(!loading()&&!placements().length){<p class="muted">No charts placed here yet. Create a definition in the library, then add it above.</p>}
`,styles:`:host{display:block;margin-top:24px;}.chart-grid{display:grid;grid-template-columns:repeat(12,minmax(0,1fr));gap:16px;}.chart-placement{grid-column:span 6;min-width:0;margin:0;}.chart-placement.small{grid-column:span 4;}.chart-placement.wide{grid-column:span 12;}.layout-controls{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:14px;}.layout-controls label{display:flex;align-items:center;gap:6px;}.layout-controls select{padding:8px;background:#152127;color:#d6e3e8;border:1px solid #425560;}.cdk-drag-preview{box-sizing:border-box;box-shadow:0 8px 30px #0008;}.cdk-drag-placeholder{opacity:.25;}.chart-placement>a{display:inline-block;margin-top:14px;}@media(max-width:950px){.chart-placement.small{grid-column:span 6;}}@media(max-width:650px){.chart-placement,.chart-placement.small{grid-column:span 12;}}`})
export class ChartArea {
 readonly pageType=input<'Dashboard'|'Track'>('Dashboard');readonly pageId=input<string|null>(null);private readonly http=inject(HttpClient);readonly placements=signal<Placement[]>([]);readonly definitions=signal<ChartDefinition[]>([]);readonly chartData=signal<Record<string,ChartData>>({});readonly chartErrors=signal<Record<string,string>>({});readonly error=signal('');readonly busy=signal(false);readonly loading=signal(true);readonly editing=signal(false);readonly saved=signal(false);selectedDefinition='';
 constructor(){effect(()=>{this.pageType();this.pageId();this.load();});}
 name(row:Placement){return this.definitions().find(x=>x.id===row.chartDefinitionId)?.name??'Chart';}
 load(){this.loading.set(true);const params:Record<string,string>={pageType:this.pageType()};if(this.pageId())params['pageId']=this.pageId()!;forkJoin({placements:this.http.get<Placement[]>('/api/economics/charts/placements',{params}),definitions:this.http.get<ChartDefinition[]>('/api/economics/charts/definitions')}).subscribe({next:result=>{this.placements.set(result.placements);this.definitions.set(result.definitions);this.error.set('');this.loading.set(false);this.loadData(result.placements);},error:e=>{this.error.set(requestError(e));this.loading.set(false);}});}
 private loadData(rows:Placement[]){this.chartData.set({});this.chartErrors.set({});if(!rows.length)return;forkJoin(rows.map(row=>this.http.get<ChartData>(`/api/economics/charts/placements/${row.id}/data`).pipe(catchError(e=>of({error:requestError(e)}))))).subscribe(results=>{const data:Record<string,ChartData>={};const errors:Record<string,string>={};results.forEach((result,index)=>{'error' in result?errors[rows[index].id]=result.error:data[rows[index].id]=result;});this.chartData.set(data);this.chartErrors.set(errors);});}
 add(){this.busy.set(true);this.http.post('/api/economics/charts/placements',{chartDefinitionId:this.selectedDefinition,pageType:this.pageType(),pageId:this.pageId(),width:'Medium'}).subscribe({next:()=>{this.busy.set(false);this.selectedDefinition='';this.load();},error:e=>{this.busy.set(false);this.error.set(requestError(e));}});}
 drop(event:CdkDragDrop<Placement[]>){const rows=[...this.placements()];moveItemInArray(rows,event.previousIndex,event.currentIndex);this.placements.set(rows);this.saved.set(false);}
 move(index:number,delta:number){const rows=[...this.placements()];moveItemInArray(rows,index,index+delta);this.placements.set(rows);}
 cancelLayout(){this.editing.set(false);this.load();}
 saveLayout(){this.busy.set(true);this.http.put<Placement[]>('/api/economics/charts/layout',{pageType:this.pageType(),pageId:this.pageId(),placements:this.placements().map(x=>({id:x.id,width:x.width,revision:x.revision}))}).subscribe({next:rows=>{this.placements.set(rows);this.busy.set(false);this.editing.set(false);this.saved.set(true);this.error.set('');},error:e=>{this.busy.set(false);this.error.set(requestError(e));}});}
 remove(row:Placement){this.busy.set(true);this.http.delete(`/api/economics/charts/placements/${row.id}`,{params:{revision:row.revision}}).subscribe({next:()=>{this.busy.set(false);this.editing.set(false);this.load();},error:e=>{this.busy.set(false);this.error.set(requestError(e));}});}
}
