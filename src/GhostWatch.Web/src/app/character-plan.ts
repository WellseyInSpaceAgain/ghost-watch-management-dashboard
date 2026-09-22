import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { forkJoin } from 'rxjs';
import { Account } from './accounts';
import { requestError } from './tracks/track-api';
interface Plan { accountId: string | null; assignment: string; notes: string; revision: number; tracks: { trackId: string; name: string }[]; }
@Component({
  selector: 'app-character-plan', imports: [FormsModule, RouterLink, MatButtonModule],
  template: `<section class="panel"><div class="section-heading"><h2>Economic assignment</h2><span class="tag">MANUAL DATA</span></div>
    <p class="muted">Planning labels are separate from skill capability. <a routerLink="/accounts">Manage account groups</a>.</p>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button mat-button (click)="load()">Reload planning data</button></p> }
    @if (ready()) { <form class="compact-form" #form="ngForm" (ngSubmit)="save()">
      <label>Account<select name="accountId" [(ngModel)]="draft.accountId"><option [ngValue]="null">Ungrouped</option>@for (account of accounts(); track account.id) { <option [ngValue]="account.id">{{ account.name }} · {{ account.subscription }}</option> }</select></label>
      <label>Economic assignment<input name="assignment" [(ngModel)]="draft.assignment" maxlength="120" placeholder="For example, Research / Invention"></label>
      <label>Planning notes<textarea name="notes" [(ngModel)]="draft.notes" maxlength="2000" rows="3"></textarea></label>
      <fieldset><legend>Linked Tracks</legend>@for (track of tracks(); track track.id) { <label class="check"><input type="checkbox" [name]="'track-' + track.id" [ngModel]="selected.includes(track.id)" (ngModelChange)="toggle(track.id, $event)">{{ track.name }} <a [routerLink]="['/tracks', track.id]">Open Track</a></label> } @empty { <p>Create an Economy Track to link this character.</p> }</fieldset>
      <button mat-flat-button [disabled]="saving() || form.invalid">Save assignment</button>@if (saved()) { <p role="status">Assignment saved.</p> }
    </form> }
  </section>`,
})
export class CharacterPlan implements OnInit {
  readonly characterId = input.required<number>(); private readonly http = inject(HttpClient);
  readonly accounts = signal<Account[]>([]); readonly tracks = signal<{ id: string; name: string }[]>([]);
  readonly error = signal(''); readonly ready = signal(false); readonly saving = signal(false); readonly saved = signal(false);
  draft = { accountId: null as string | null, assignment: '', notes: '', revision: 0 }; selected: string[] = [];
  ngOnInit() { this.load(); }
  load() {
    forkJoin({ plan: this.http.get<Plan>(`/api/management/characters/${this.characterId()}`), accounts: this.http.get<Account[]>('/api/management/accounts'), tracks: this.http.get<{ id: string; name: string }[]>('/api/economics/tracks?includeArchived=true') }).subscribe({
      next: result => { this.draft = result.plan; this.selected = result.plan.tracks.map(x => x.trackId); this.accounts.set(result.accounts); this.tracks.set(result.tracks); this.ready.set(true); this.error.set(''); },
      error: error => this.error.set(requestError(error)),
    });
  }
  toggle(id: string, checked: boolean) { this.selected = checked ? [...this.selected, id] : this.selected.filter(x => x !== id); this.saved.set(false); }
  save() { this.saving.set(true); this.saved.set(false); this.http.put<{ revision: number }>(`/api/management/characters/${this.characterId()}`, { ...this.draft, trackIds: this.selected }).subscribe({
    next: result => { this.draft.revision = result.revision; this.saved.set(true); this.error.set(''); this.saving.set(false); }, error: error => { this.error.set(requestError(error)); this.saving.set(false); },
  }); }
}
