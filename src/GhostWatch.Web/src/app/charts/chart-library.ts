import {Component,DestroyRef,inject,signal} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {FormsModule} from '@angular/forms';
import {ActivatedRoute,RouterLink} from '@angular/router';
import {MatButtonModule} from '@angular/material/button';
import {Subject,debounceTime,switchMap,catchError,of,forkJoin} from 'rxjs';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ChartRenderer,ChartData} from './chart-renderer';
import {requestError} from '../tracks/track-api';
export interface ChartDefinition {id:string;name:string;title:string;description:string;configJson:string;revision:number;placementCount:number;}
interface Schema {sample:string;sources:Record<string,{dimensions:string[];measures:string[];filters:string[]}>;types:string[];aggregations:string[];formats:string[];}
@Component({selector:'app-chart-library',imports:[FormsModule,RouterLink,MatButtonModule,ChartRenderer],template:`
<p class="eyebrow">ECONOMICS / VISUALISATION</p><div class="page-heading"><h1>Chart library</h1><button mat-flat-button (click)="reset()">New chart</button></div><p class="muted">Definitions describe the data. Dashboard and Track placements reuse them with independent widths and ordering.</p>
@if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload library</button></p>}
<section class="panel table-wrap"><table><thead><tr><th>Definition</th><th>Title</th><th>Placements</th><th></th></tr></thead><tbody>@for(row of rows();track row.id){<tr><td>{{row.name}}</td><td>{{row.title}}</td><td>{{row.placementCount}}</td><td><button mat-button (click)="edit(row)">Edit {{row.name}}</button><button mat-button [disabled]="row.placementCount>0||busy()" (click)="remove(row)">Delete {{row.name}}</button></td></tr>}@empty{<tr><td colspan="4">No definitions yet. Start with the sample below.</td></tr>}</tbody></table></section>
<form class="panel compact-form" #form="ngForm" (ngSubmit)="save()"><h2>{{draft.id?'Edit':'Create'}} definition</h2><label>Definition name<input name="name" [(ngModel)]="draft.name" required maxlength="120"></label>
<label>Chart configuration JSON<textarea name="config" rows="22" maxlength="20000" [ngModel]="draft.configJson" (ngModelChange)="draft.configJson=$event;previewChanged()" required spellcheck="false"></textarea></label>
<label>Preview Track context<select name="context" [(ngModel)]="contextTrack" (ngModelChange)="previewChanged()"><option [ngValue]="null">Programme / Dashboard</option>@for(track of tracks();track track.id){<option [ngValue]="track.id">{{track.name}}</option>}</select></label><p class="muted">CURRENT_TRACK resolves to this preview selection or the Track containing a placement. JSON uses only the supported schema below.</p>
<div class="form-actions"><button mat-stroked-button type="button" (click)="previewChanged()">Preview chart</button><button mat-flat-button [disabled]="form.invalid||busy()">Save definition</button><button mat-button type="button" (click)="useSample()">Use example JSON</button></div>
@if(draft.id){<p class="notice">Saving edits updates this shared definition on every page using it. Remove placements on their pages before deleting a definition.</p>}
@if(saved()){<p role="status">Chart definition saved. Add it to a Dashboard or Track chart area.</p>}
</form>
<section class="panel"><h2>Live preview</h2>@if(previewLoading()){<p>Updating preview…</p>}@if(previewError()){<p class="error" role="alert">{{previewError()}}</p>}@if(preview();as chart){<app-chart-renderer [data]="chart" />}</section>
@if(schema();as schema){<section class="panel"><h2>Supported schema</h2><p>Types: {{schema.types.join(', ')}}. Aggregations: {{schema.aggregations.join(', ')}}. Formats: {{schema.formats.join(', ')}}.</p><p>Use 1–4 series, each with field, label, aggregation and format. Time range: all, 30d, 90d or 365d. Pie/donut/KPI uses one series; KPI uses x.field = all. Negative values require a bar or line chart.</p>@for(source of sourceNames();track source){<details><summary>{{source}}</summary><p>Group fields: {{schema.sources[source].dimensions.join(', ')}}</p><p>Measures: {{schema.sources[source].measures.join(', ')}}</p><p>Filters: {{schema.sources[source].filters.join(', ')}}</p></details>}<p class="muted">Snapshots use stored programme measures, or stored Track measures when trackId is present. Unknown values remain unknown in sums/averages; count counts records. A query supports up to 10,000 source records and 200 groups.</p></section>}
<p><a routerLink="/">Dashboard chart area</a> · <a routerLink="/tracks">Track pages</a></p>
`,styles:`textarea{font-family:ui-monospace,monospace;}details{margin:12px 0;}summary{cursor:pointer;}`})
export class ChartLibrary {
 private readonly http=inject(HttpClient);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);private readonly changes=new Subject<{configJson:string;trackId:string|null}>();
 readonly rows=signal<ChartDefinition[]>([]);readonly schema=signal<Schema|null>(null);readonly tracks=signal<{id:string;name:string}[]>([]);readonly error=signal('');readonly previewError=signal('');readonly preview=signal<ChartData|null>(null);readonly previewLoading=signal(false);readonly saved=signal(false);readonly busy=signal(false);contextTrack:string|null=null;private initial=true;
 draft:ChartDefinition={id:'',name:'',title:'',description:'',configJson:'',revision:0,placementCount:0};
 constructor(){this.contextTrack=this.route.snapshot.queryParamMap.get('trackId');this.changes.pipe(debounceTime(350),switchMap(input=>this.http.post<ChartData>('/api/economics/charts/preview',input).pipe(catchError(error=>of({error:requestError(error)})))),takeUntilDestroyed(this.destroy)).subscribe(result=>{this.previewLoading.set(false);if('error' in result){this.previewError.set(result.error);this.preview.set(null);}else{this.previewError.set('');this.preview.set(result);}});this.load();}
 load(){forkJoin({rows:this.http.get<ChartDefinition[]>('/api/economics/charts/definitions'),schema:this.http.get<Schema>('/api/economics/charts/schema'),tracks:this.http.get<{id:string;name:string}[]>('/api/economics/tracks?includeArchived=true')}).subscribe({next:data=>{this.rows.set(data.rows);this.schema.set(data.schema);this.tracks.set(data.tracks);this.error.set('');if(this.initial){const row=data.rows.find(x=>x.id===this.route.snapshot.queryParamMap.get('edit'));if(row)this.edit(row);else this.reset();this.initial=false;}},error:e=>this.error.set(requestError(e))});}
 reset(){this.draft={id:'',name:'',title:'',description:'',configJson:this.schema()?.sample??'',revision:0,placementCount:0};this.saved.set(false);this.previewChanged();}
 edit(row:ChartDefinition){this.draft={...row};this.saved.set(false);this.previewChanged();}
 useSample(){this.draft.configJson=this.schema()?.sample??'';this.previewChanged();}
 previewChanged(){this.previewLoading.set(true);this.preview.set(null);this.previewError.set('');this.changes.next({configJson:this.draft.configJson,trackId:this.contextTrack});}
 sourceNames(){return Object.keys(this.schema()?.sources??{});}
 save(){this.busy.set(true);this.saved.set(false);const request=this.draft.id?this.http.put<ChartDefinition>(`/api/economics/charts/definitions/${this.draft.id}`,this.draft):this.http.post<ChartDefinition>('/api/economics/charts/definitions',this.draft);request.subscribe({next:row=>{this.draft=row;this.busy.set(false);this.saved.set(true);this.load();},error:e=>{this.busy.set(false);this.error.set(requestError(e));}});}
 remove(row:ChartDefinition){this.busy.set(true);this.http.delete(`/api/economics/charts/definitions/${row.id}`,{params:{revision:row.revision}}).subscribe({next:()=>{this.busy.set(false);if(this.draft.id===row.id)this.reset();this.load();},error:e=>{this.busy.set(false);this.error.set(requestError(e));}});}
}
