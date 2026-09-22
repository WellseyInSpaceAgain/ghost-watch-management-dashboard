import {Component,inject,signal} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {FormsModule} from '@angular/forms';
import {DecimalPipe} from '@angular/common';
import {RouterLink} from '@angular/router';
import {MatButtonModule} from '@angular/material/button';
import {requestError} from './tracks/track-api';
interface Package {id:string;name:string;description:string;estimatedReplacementValue:number;isDefault:boolean;notes:string;revision:number;}
interface Summary {packages:Package[];treasury:number|null;treasuryName:string|null;coverage:Record<string,number|null>;}
@Component({selector:'app-replacements',imports:[FormsModule,DecimalPipe,RouterLink,MatButtonModule],template:`
<p class="eyebrow">ECONOMICS / DOCTRINE</p><div class="page-heading"><h1>Replacement Packages</h1><button mat-flat-button (click)="reset()">New package</button></div>
<p class="muted">Manual replacement estimates for doctrine assets. Coverage divides the Ghost Watch Treasury allocation by each package value.</p>
@if(error()){<p class="error" role="alert">{{error()}} <button mat-button (click)="load()">Reload</button></p>}
@if(data();as data){<section class="panel"><h2>Treasury coverage</h2><p>{{data.treasuryName??'Ghost Watch Treasury not configured'}}: <strong>{{data.treasury===null?'Unknown':(data.treasury|number:'1.0-2')+' ISK'}}</strong></p><a routerLink="/capital">Manage Capital Pools</a></section>
<section class="panel table-wrap"><table><thead><tr><th>Package</th><th>Estimated value</th><th>Coverage</th><th>Default</th><th></th></tr></thead><tbody>
@for(row of data.packages;track row.id){<tr><td>{{row.name}}</td><td>{{row.estimatedReplacementValue|number:'1.0-2'}} ISK</td><td>{{data.coverage[row.id]===null?'Unknown':(data.coverage[row.id]|number:'1.0-2')+' packages'}}</td><td>{{row.isDefault?'Default':''}}</td><td><button mat-button (click)="edit(row)">Edit {{row.name}}</button></td></tr>}
@empty{<tr><td colspan="5">No replacement packages yet.</td></tr>}
</tbody></table></section>}
<form class="panel compact-form" #form="ngForm" (ngSubmit)="save()"><h2>{{draft.id?'Edit':'Create'}} package</h2>
<label>Package name<input name="name" [(ngModel)]="draft.name" required maxlength="120"></label>
<label>Description<textarea name="description" [(ngModel)]="draft.description" maxlength="2000"></textarea></label>
<label>Estimated replacement value (ISK)<input name="value" type="number" min="0.01" step="any" [(ngModel)]="draft.estimatedReplacementValue" required></label>
<label class="check"><input name="default" type="checkbox" [(ngModel)]="draft.isDefault">Default programme package</label>
<label>Notes<textarea name="notes" [(ngModel)]="draft.notes" maxlength="20000"></textarea></label>
<button mat-flat-button [disabled]="form.invalid||busy()">Save package</button>@if(saved()){<p role="status">Package saved.</p>}
</form>`})
export class Replacements {
 private readonly http=inject(HttpClient);readonly data=signal<Summary|null>(null);readonly error=signal('');readonly saved=signal(false);readonly busy=signal(false);draft=this.empty();
 private empty():Package{return{id:'',name:'',description:'',estimatedReplacementValue:0,isDefault:false,notes:'',revision:0};}
 constructor(){this.load();}
 load(){this.http.get<Summary>('/api/economics/replacement-packages').subscribe({next:data=>{this.data.set(data);this.error.set('');},error:e=>this.error.set(requestError(e))});}
 reset(){this.draft=this.empty();this.saved.set(false);}
 edit(row:Package){this.draft={...row};this.saved.set(false);}
 save(){this.busy.set(true);this.saved.set(false);const request=this.draft.id?this.http.put<Package>(`/api/economics/replacement-packages/${this.draft.id}`,this.draft):this.http.post<Package>('/api/economics/replacement-packages',this.draft);request.subscribe({next:row=>{this.draft=row;this.saved.set(true);this.busy.set(false);this.load();},error:e=>{this.error.set(requestError(e));this.busy.set(false);}});}
}
