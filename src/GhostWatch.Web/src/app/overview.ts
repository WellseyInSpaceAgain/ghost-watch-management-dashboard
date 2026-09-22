import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { TrackApi, Track, requestError } from './tracks/track-api';

@Component({
  selector: 'app-overview',
  imports: [RouterLink, MatButtonModule],
  template: `
    <p class="eyebrow">ECONOMICS / OVERVIEW</p>
    <div class="page-heading"><div><h1>Economic operations</h1><p class="muted">Plan the programmes. Track the work. Preserve what you learn.</p></div><a mat-flat-button routerLink="/tracks/new">Create Track</a></div>
    <section class="metrics" aria-label="Programme metrics">
      @for (metric of metrics; track metric.name) {
        <article class="metric"><h2>{{ metric.name }}</h2><strong>—</strong><p>{{ metric.reason }}</p></article>
      }
    </section>
    <section class="panel">
      <div class="section-heading"><h2>Active Tracks</h2><a routerLink="/tracks">All Tracks →</a></div>
      @if (error()) { <p role="alert" class="error">{{ error() }} <button mat-button (click)="load()">Retry</button></p> }
      @else if (loading()) { <p role="status">Loading active Tracks…</p> }
      @else if (tracks().length) {
        <div class="table-wrap"><table><caption class="visually-hidden">Active economic programmes</caption><thead><tr><th>Track</th><th>Purpose</th><th>Status</th></tr></thead><tbody>
        @for (track of tracks(); track track.id) { <tr><td><a [routerLink]="['/tracks', track.id]">{{ track.name }}</a><small>{{ track.description }}</small></td><td>{{ track.purpose }}</td><td><span class="status">{{ track.status }}</span></td></tr> }
        </tbody></table></div>
      } @else {
        <p>No active programmes yet.</p><p class="muted">Create a Track for your strategy, then set its status to Active when work begins.</p>
        <a mat-stroked-button routerLink="/tracks">Manage Tracks</a>
      }
    </section>
    <section class="panel"><h2>Workspace setup</h2><p>Track planning is ready. EVE character connections, capital allocations, Runs, and historical reporting are next in development.</p>
      <p class="muted">Financial metrics remain unavailable until their underlying data is recorded.</p></section>
  `,
})
export class Overview {
  private readonly api = inject(TrackApi);
  readonly tracks = signal<Track[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly metrics = [
    { name: 'Core Capital', reason: 'No capital pool configured' },
    { name: '30-day realised profit', reason: 'No recorded financial results' },
    { name: 'Ghost Watch Treasury', reason: 'No treasury configured' },
    { name: 'Replacement coverage', reason: 'No replacement package configured' },
  ];
  constructor() { this.load(); }
  load() {
    this.loading.set(true); this.error.set('');
    this.api.list().subscribe({
      next: tracks => { this.tracks.set(tracks.filter(track => track.status === 'Active')); this.loading.set(false); },
      error: error => { this.error.set(requestError(error)); this.loading.set(false); },
    });
  }
}
