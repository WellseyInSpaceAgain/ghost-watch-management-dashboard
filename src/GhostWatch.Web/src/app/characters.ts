import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { forkJoin } from 'rxjs';

interface EveConfig { configured: boolean; callbackUrl: string; scopes: string[]; }
interface Character { characterId: number; characterName: string; connectedAt: string; lastAuthenticatedAt: string; }

@Component({
  selector: 'app-characters',
  imports: [DatePipe, MatButtonModule, RouterLink],
  template: `
    <p class="eyebrow">EVE DATA / CHARACTERS</p>
    <div class="page-heading"><div><h1>Characters</h1><p class="muted">Connect the characters supporting your economic programmes.</p></div>
      @if (config()?.configured) { <a mat-flat-button href="/api/auth/eve/start">Log in with EVE Online</a> }
    </div>
    @if (outcome) { <p [class]="connected ? 'notice' : 'error'" [attr.role]="connected ? 'status' : 'alert'">{{ outcome }}</p> }
    @if (loading()) { <p role="status">Loading character connections…</p> }
    @else if (error()) { <p class="error" role="alert">{{ error() }} <button mat-button (click)="load()">Retry</button></p> }
    @else {
      @if (!config()?.configured) {
        <section class="panel"><h2>Set up EVE authentication</h2>
          <p>Register a separate Ghost Watch application with EVE Online, then configure its client ID and secret locally. See the repository's EVE SSO setup guide.</p>
          <p>Register this exact callback URL:</p><p><code>{{ config()?.callbackUrl }}</code></p>
          <a mat-stroked-button href="https://developers.eveonline.com/applications" target="_blank" rel="noopener noreferrer">Open EVE Developer Portal</a>
        </section>
      }
      <section class="panel"><div class="section-heading"><h2>Connected characters</h2><span class="tag">EVE IDENTITY</span></div>
        @if (!characters().length) { <p>No characters connected yet.</p><p class="muted">EVE handles your login and character selection. Connect additional characters by repeating the login process.</p> }
        @else {
          <div class="table-wrap"><table><caption class="visually-hidden">Authenticated EVE characters</caption><thead><tr><th>Character</th><th>EVE ID</th><th>Last authenticated</th></tr></thead>
          <tbody>@for (character of characters(); track character.characterId) {
            <tr><td><a [routerLink]="['/characters', character.characterId]">{{ character.characterName }}</a></td><td>{{ character.characterId }}</td><td>{{ character.lastAuthenticatedAt | date:'medium' }}</td></tr>
          }</tbody></table></div>
          <p class="muted" style="margin-top:16px">Use the login button again to add another character or reconnect an existing one. Select the character on EVE's login page.</p>
        }
      </section>
      <p class="muted">Open a character to refresh wallets, skills, skill queues, market orders, industry jobs, assets and blueprints. Accounts and economic assignments are not available yet.</p>
      <details class="panel"><summary>Requested EVE permissions</summary><p class="muted">These permissions cover character economics in the project brief. Corporation access is not requested.</p><ul>
        @for (scope of config()?.scopes; track scope) { <li><code>{{ scope }}</code></li> }
      </ul></details>
    }
  `,
  styles: `code { overflow-wrap: anywhere; } details p { margin-top: 16px; } summary { cursor: pointer; }`,
})
export class Characters {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(ActivatedRoute).snapshot.queryParamMap.get('auth');
  readonly connected = this.auth === 'connected';
  readonly outcome = this.auth ? ({
    connected: 'Character connected successfully.',
    configuration: 'EVE authentication needs local configuration before you can connect a character.',
    'invalid-state': 'This login expired, was already used, or started in another browser. Start a new login here.',
    cancelled: 'EVE login was cancelled. No connection was changed.',
    'invalid-client': 'EVE rejected the application credentials. Check the local client ID and secret.',
    'invalid-grant': 'EVE rejected the login code. Start a new login and check the registered callback URL.',
    'invalid-token': 'The returned EVE identity could not be verified. No connection was saved.',
    'ownership-changed': 'This character has a different owner. The existing connection was retained; ownership changes need manual review.',
    'network-error': 'EVE authentication could not be reached. Try again shortly.',
    timeout: 'EVE authentication timed out. Start a new login.',
  } as Record<string, string>)[this.auth] ?? 'EVE authentication failed. No connection was saved. Try again.' : '';
  readonly config = signal<EveConfig | null>(null);
  readonly characters = signal<Character[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  constructor() { this.load(); }
  load() {
    this.loading.set(true); this.error.set('');
    forkJoin({ config: this.http.get<EveConfig>('/api/auth/eve/config'), characters: this.http.get<Character[]>('/api/eve/characters') }).subscribe({
      next: result => { this.config.set(result.config); this.characters.set(result.characters); this.loading.set(false); },
      error: () => { this.error.set('Character connections could not be loaded. Check that the local API is running.'); this.loading.set(false); },
    });
  }
}
