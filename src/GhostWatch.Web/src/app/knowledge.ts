import { ChangeDetectorRef, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { forkJoin, of } from 'rxjs';
import { MarkdownView } from './markdown-view';
import { requestError } from './tracks/track-api';
interface Document {id:string;name?:string;title?:string;description?:string;markdownBody:string;status?:string;recordType?:string;tags:string[];revision:number;updatedAt?:string;links:Record<string,(string|number)[]>;}
interface Revision {version:number;name:string;markdownBody:string;savedAt:string;}
interface LinkOption {id:string|number;name:string;}
@Component({selector:'app-knowledge',imports:[FormsModule,DatePipe,RouterLink,MatButtonModule,MarkdownView],template:`
  <p class="eyebrow">KNOWLEDGE / {{isPlaybook?'PROCEDURES':'RECORDS'}}</p><div class="page-heading"><h1>{{isPlaybook?'Playbooks':'Records'}}</h1><button mat-flat-button (click)="reset()">{{isPlaybook?'New Playbook':'New Record'}}</button></div>
  <p class="muted">{{isPlaybook?'Reusable Markdown procedures with retained revision history.':'Decisions, findings, sourcing notes and any other economic knowledge.'}}</p>
  @if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload documents</button></p>}
  <label class="compact-form">Search documents<input [(ngModel)]="query" placeholder="Title, type, status or tags"></label>
  <section class="panel table-wrap"><table><thead><tr><th>{{isPlaybook?'Playbook':'Record'}}</th><th>{{isPlaybook?'Status':'Type'}}</th><th>Tags</th><th>Updated</th><th></th></tr></thead><tbody>
    @for(row of filtered();track row.id){<tr><td>{{row.name??row.title}}</td><td>{{row.status??row.recordType}}</td><td>{{row.tags.join(', ')}}</td><td>{{row.updatedAt|date:'medium'}}</td><td><button mat-button (click)="open(row.id)">Open {{row.name??row.title}}</button></td></tr>}
    @empty{<tr><td colspan="5">No documents match. Create one below.</td></tr>}
  </tbody></table></section>
  <form class="panel compact-form" #form="ngForm" (ngSubmit)="save()"><h2>{{draft.id?'Edit':'Create'}} {{isPlaybook?'Playbook':'Record'}}</h2>
    <label>{{isPlaybook?'Playbook name':'Record title'}}<input name="title" [(ngModel)]="draft.title" required maxlength="160"></label>
    @if(isPlaybook){<label>Description<textarea name="description" rows="2" maxlength="2000" [(ngModel)]="draft.description"></textarea></label><label>Status<select name="status" [(ngModel)]="draft.status"><option>Draft</option><option>Active</option><option>Archived</option></select></label>}
    @else{<label>Record type<input name="recordType" [(ngModel)]="draft.recordType" required maxlength="80" placeholder="Decision, Finding, Supplier note…"></label>}
    <label>Tags (comma separated)<input name="tags" [(ngModel)]="tagsText" maxlength="1200"></label>
    <label>Markdown<textarea name="body" rows="14" maxlength="100000" [(ngModel)]="draft.markdownBody" placeholder="# Procedure or note"></textarea></label>
    <details open><summary>Markdown preview</summary><app-markdown-view [body]="draft.markdownBody" /></details>
    <details><summary>Related objects</summary>
      @for(group of linkGroups();track group.key){<fieldset><legend>{{group.label}}</legend>
        @for(option of options()[group.source]??[];track option.id){<label class="check"><input type="checkbox" [name]="group.key+option.id" [ngModel]="draft.links[group.key].includes(option.id)" (ngModelChange)="toggle(group.key,option.id,$event)">{{option.name}} <a [routerLink]="linkPath(group.source,option.id)" [queryParams]="linkQuery(group.source,option.id)">Open</a></label>}
        @empty{<p class="muted">No {{group.label.toLowerCase()}} yet.</p>}
      </fieldset>}
    </details>
    <button mat-flat-button [disabled]="form.invalid||busy()">{{isPlaybook?'Save Playbook':'Save Record'}}</button>@if(saved()){<p role="status">Document saved.</p>}
  </form>
  @if(isPlaybook&&draft.id){<section class="panel"><h2>Playbook revision history</h2><p>Current version: {{draft.revision}}. Earlier Markdown remains available below.</p>
    @for(revision of revisions();track revision.version){<details><summary>Version {{revision.version}} · {{revision.name}} · {{revision.savedAt|date:'medium'}}</summary><app-markdown-view [body]="revision.markdownBody" /></details>}
    @empty{<p class="muted">No earlier revisions yet.</p>}
  </section>}
`,styles:`details{margin:12px 0;}summary{cursor:pointer;}fieldset{margin:12px 0;}.check a{margin-left:auto;}`})
export class Knowledge {
  private readonly change=inject(ChangeDetectorRef);private readonly http=inject(HttpClient);private readonly route=inject(ActivatedRoute);readonly kind=this.route.snapshot.data['kind'] as 'playbooks'|'records';readonly isPlaybook=this.kind==='playbooks';
  readonly error=signal('');readonly rows=signal<Document[]>([]);readonly revisions=signal<Revision[]>([]);readonly options=signal<Record<string,LinkOption[]>>({});readonly busy=signal(false);readonly saved=signal(false);query='';tagsText='';private initial=true;
  draft:Document=this.empty();
  private empty():Document{return{id:'',title:'',name:'',description:'',markdownBody:'',status:'Draft',recordType:'Note',tags:[],revision:0,links:{trackIds:[],runIds:[],characterIds:[],playbookIds:[],objectiveIds:[],capitalPoolIds:[]}};}
  constructor(){this.load();}
  load(){forkJoin({rows:this.http.get<Document[]>(`/api/knowledge/${this.kind}`),options:this.http.get<Record<string,LinkOption[]>>('/api/knowledge/options')}).subscribe({next:data=>{this.rows.set(data.rows);this.options.set(data.options);this.error.set('');if(this.initial){const id=this.route.snapshot.queryParamMap.get('edit');if(id)this.open(id);}this.initial=false;},error:error=>this.error.set(requestError(error))});}
  reset(){this.draft=this.empty();this.tagsText='';this.revisions.set([]);this.saved.set(false);}
  filtered(){const query=this.query.toLowerCase();return this.rows().filter(x=>`${x.name??x.title} ${x.recordType??x.status} ${x.tags.join(' ')}`.toLowerCase().includes(query));}
  open(id:string){forkJoin({doc:this.http.get<Document>(`/api/knowledge/${this.kind}/${id}`),history:this.isPlaybook?this.http.get<Revision[]>(`/api/knowledge/playbooks/${id}/revisions`):of([])}).subscribe({next:data=>{this.draft={...this.empty(),...data.doc,title:data.doc.name??data.doc.title};this.tagsText=data.doc.tags.join(', ');this.revisions.set(data.history);this.error.set('');this.saved.set(false);this.change.markForCheck();},error:error=>this.error.set(requestError(error))});}
  linkGroups(){return[{key:'trackIds',source:'tracks',label:'Tracks'},{key:'runIds',source:'runs',label:'Runs'},{key:'characterIds',source:'characters',label:'Characters'},...this.isPlaybook?[]:[{key:'playbookIds',source:'playbooks',label:'Playbooks'},{key:'objectiveIds',source:'objectives',label:'Objectives / Gates'},{key:'capitalPoolIds',source:'capitalPools',label:'Capital Pools'}]];}
  toggle(key:string,id:string|number,value:boolean){this.draft.links[key]=value?[...this.draft.links[key],id]:this.draft.links[key].filter(x=>x!==id);}
  linkPath(source:string,id:string|number){return ['tracks','runs','characters'].includes(source)?`/${source}/${id}`:source==='capitalPools'?'/capital':`/${source}`;}
  linkQuery(source:string,id:string|number){return ['playbooks','objectives','capitalPools'].includes(source)?{edit:id}:{};}
  save(){this.busy.set(true);this.saved.set(false);const body={...this.draft,name:this.draft.title,tags:this.tagsText.split(',').map(x=>x.trim()).filter(Boolean)};const request=this.draft.id?this.http.put<{id:string;revision:number}>(`/api/knowledge/${this.kind}/${this.draft.id}`,body):this.http.post<{id:string;revision:number}>(`/api/knowledge/${this.kind}`,body);request.subscribe({next:data=>{this.draft.id=data.id;this.draft.revision=data.revision;this.busy.set(false);this.saved.set(true);this.load();if(this.isPlaybook)this.http.get<Revision[]>(`/api/knowledge/playbooks/${data.id}/revisions`).subscribe(rows=>this.revisions.set(rows));},error:error=>{this.error.set(requestError(error));this.busy.set(false);}});}
}
