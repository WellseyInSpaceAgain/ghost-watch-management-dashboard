import { HttpClient } from '@angular/common/http';
import { Pool } from '../capital';
import { TrackCharacters } from '../track-characters';
import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin, of } from 'rxjs';
import { Track, TrackApi, TrackDraft, TrackOptions, requestError } from './track-api';

@Component({
  selector: 'app-track-detail',
  imports: [DatePipe, FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, TrackCharacters],
  template: `
    <a routerLink="/tracks" class="back-link">← Economy Tracks</a>
    <p class="eyebrow">ECONOMICS / {{ id ? 'TRACK DETAIL' : 'NEW TRACK' }}</p>
    <h1>{{ track()?.name || 'Create an Economy Track' }}</h1>
    <p class="subtitle">An ongoing strategy or programme, with a purpose and a place to keep your notes.</p>
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    @if (loading()) { <p role="status">Loading Track…</p> }
    @else if (ready()) {
      @if (track()?.archivedAt) { <p class="notice">Archived on {{ track()!.archivedAt | date:'mediumDate' }}. History is retained. Choose another status to restore this Track.</p> }
      <form #form="ngForm" (ngSubmit)="save(form)" class="panel track-form">
        <div class="section-heading"><h2>Programme details</h2><span class="tag">MANUAL DATA</span></div>
        <mat-form-field appearance="outline" class="full"><mat-label>Name</mat-label>
          <input matInput name="name" [(ngModel)]="draft.name" required maxlength="120" #name="ngModel" [disabled]="saving()">
          @if (name.invalid) { <mat-error>Enter a name (up to 120 characters).</mat-error> }
        </mat-form-field>
        <div class="form-row">
          <mat-form-field appearance="outline"><mat-label>Status</mat-label><mat-select name="status" [(ngModel)]="draft.status" required [disabled]="saving()">
            @for (status of options().statuses; track status) { <mat-option [value]="status">{{ status }}</mat-option> }
          </mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Purpose</mat-label><mat-select name="purpose" [(ngModel)]="draft.purpose" required [disabled]="saving()">
            @for (purpose of options().purposes; track purpose) { <mat-option [value]="purpose">{{ purpose }}</mat-option> }
          </mat-select></mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full"><mat-label>Default Capital Pool</mat-label><mat-select name="defaultCapitalPoolId" [(ngModel)]="draft.defaultCapitalPoolId"><mat-option [value]="null">None</mat-option>@for (pool of pools(); track pool.id) { <mat-option [value]="pool.id">{{ pool.name }}{{ pool.archivedAt ? ' (archived)' : '' }}</mat-option> }</mat-select></mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Description</mat-label>
          <textarea matInput name="description" [(ngModel)]="draft.description" rows="3" maxlength="2000" [disabled]="saving()"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full"><mat-label>Notes</mat-label>
          <textarea matInput name="notes" [(ngModel)]="draft.notes" rows="7" maxlength="20000" [disabled]="saving()"></textarea>
          <mat-hint>Local planning notes. EVE data refreshes will not overwrite these.</mat-hint>
        </mat-form-field>
        <div class="form-actions"><button mat-flat-button type="submit" [disabled]="saving() || form.invalid || !draft.name.trim()">{{ saving() ? 'Saving…' : id ? 'Save changes' : 'Create Track' }}</button>
          <a mat-button routerLink="/tracks">Back to Tracks</a>
          @if (saved()) { <span role="status" class="muted">Changes saved.</span> }
        </div>
      </form>
      @if (track(); as current) { <app-track-characters [trackId]="current.id" /><p class="muted metadata">Created {{ current.createdAt | date:'medium' }} · Updated {{ current.updatedAt | date:'medium' }} · Revision {{ current.revision }}</p> }
    } @else { <button mat-stroked-button (click)="load()">Retry loading</button> }
  `,
})
export class TrackDetail {
  private readonly http = inject(HttpClient);
  readonly pools = signal<Pool[]>([]);
  private readonly api = inject(TrackApi);
  private readonly router = inject(Router);
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
  readonly track = signal<Track | null>(null);
  readonly options = signal<TrackOptions>({ statuses: [], purposes: [] });
  readonly loading = signal(true);
  readonly ready = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');
  draft: TrackDraft = { name: '', description: '', status: 'Planning', purpose: 'Other', notes: '' };
  constructor() { this.load(); this.http.get<{pools:Pool[]}>('/api/economics/capital').subscribe({next: data => this.pools.set(data.pools), error: () => this.error.set('Capital Pools could not be loaded. Reload this page.')}); }
  load() {
    this.loading.set(true); this.error.set('');
    forkJoin({ options: this.api.options(), track: this.id ? this.api.get(this.id) : of(null) }).subscribe({
      next: ({ options, track }) => {
        this.options.set(options); this.track.set(track);
        if (track) this.draft = { name: track.name, description: track.description, status: track.status, purpose: track.purpose, notes: track.notes, defaultCapitalPoolId: track.defaultCapitalPoolId, revision: track.revision };
        this.ready.set(true); this.loading.set(false);
      },
      error: error => { this.error.set(requestError(error)); this.loading.set(false); },
    });
  }
  save(form: NgForm) {
    if (form.invalid || this.saving() || !this.draft.name.trim()) return;
    this.saving.set(true); this.error.set(''); this.saved.set(false);
    const request = this.id ? this.api.update(this.id, this.draft) : this.api.create(this.draft);
    request.subscribe({
      next: track => {
        this.track.set(track); this.draft.revision = track.revision; this.draft.name = track.name;
        this.saving.set(false); this.saved.set(true); form.form.markAsPristine();
        if (!this.id) void this.router.navigate(['/tracks', track.id]);
      },
      error: error => { this.error.set(requestError(error)); this.saving.set(false); },
    });
  }
}
