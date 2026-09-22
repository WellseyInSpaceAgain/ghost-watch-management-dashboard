import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { TrackApi, Track, requestError } from './track-api';

@Component({
  selector: 'app-track-list',
  imports: [DatePipe, RouterLink, MatButtonModule, MatCheckboxModule],
  template: `
    <p class="eyebrow">ECONOMICS / TRACKS</p>
    <div class="page-heading"><div><h1>Economy Tracks</h1><p class="muted">The programmes behind your economic activity.</p></div>
      <a mat-flat-button routerLink="/tracks/new">Create Track</a></div>
    <div class="list-controls"><span class="muted">Locally managed · independent of EVE refreshes</span>
      <mat-checkbox [checked]="includeArchived()" (change)="includeArchived.set($event.checked); load()">Include archived</mat-checkbox></div>
    @if (error()) { <p role="alert" class="error">{{ error() }} <button mat-button (click)="load()">Retry</button></p> }
    @if (loading()) { <p role="status">Loading Tracks…</p> }
    @else if (!error() && tracks().length === 0) {
      <section class="panel"><h2>No Tracks yet</h2><p>Start with an ongoing programme, such as Jita Trading, T2 Workshop, or Project Tengu.</p>
        <p class="muted">Tracks hold your strategy and notes. You can archive them later without losing their history.</p>
        <a mat-stroked-button routerLink="/tracks/new">Create your first Track</a></section>
    } @else if (!error()) {
      <div class="table-wrap"><table><caption class="visually-hidden">Economy Tracks</caption><thead><tr><th>Track</th><th>Purpose</th><th>Status</th><th>Updated</th></tr></thead>
        <tbody>@for (track of tracks(); track track.id) {
          <tr><td><a [routerLink]="['/tracks', track.id]">{{ track.name }}</a><small>{{ track.description }}</small></td>
            <td>{{ track.purpose }}</td><td><span class="status">{{ track.status }}</span></td><td>{{ track.updatedAt | date:'mediumDate' }}</td></tr>
        }</tbody></table></div>
    }
  `,
})
export class TrackList {
  private readonly api = inject(TrackApi);
  readonly tracks = signal<Track[]>([]);
  readonly error = signal('');
  readonly loading = signal(true);
  readonly includeArchived = signal(false);
  private requestNumber = 0;
  constructor() { this.load(); }
  load() {
    const request = ++this.requestNumber;
    this.loading.set(true); this.error.set('');
    this.api.list(this.includeArchived()).subscribe({
      next: tracks => { if (request === this.requestNumber) { this.tracks.set(tracks); this.loading.set(false); } },
      error: error => { if (request === this.requestNumber) { this.error.set(requestError(error)); this.loading.set(false); } },
    });
  }
}
