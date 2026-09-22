import { EvePermissions } from './eve-permissions';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { catchError, finalize, forkJoin, map, of, Subscription } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface EveConfig { configured: boolean; callbackUrl: string; scopes: string[]; }
interface RefreshProgress { state: string; section: string | null; error: string | null; currentStep: number; totalSteps: number; }
interface Character { characterId: number; characterName: string; connectedAt: string; lastAuthenticatedAt: string; progress?: RefreshProgress; permissions?: EvePermissions; }

@Component({
  selector: 'app-characters',
  imports: [DatePipe, MatButtonModule, MatProgressSpinnerModule, MatTooltipModule, RouterLink],
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
      <section class="panel"><div class="section-heading"><h2>Connected characters</h2><button mat-stroked-button (click)="refreshAll()" [disabled]="refreshingAll() || !characters().length">{{ refreshingAll() ? 'Queuing refreshes…' : 'Refresh all characters' }}</button></div>
        @if (refreshMessage()) { <p role="status">{{ refreshMessage() }}</p> }
        @if (refreshError()) { <p role="alert" class="error">{{ refreshError() }}</p> }
        @if (progressError()) { <p class="error" role="alert">{{ progressError() }} <button mat-button (click)="pollCharacters()">Retry status</button></p> }
        @if (!characters().length) { <p>No characters connected yet.</p><p class="muted">EVE handles your login and character selection. Connect additional characters by repeating the login process.</p> }
        @else {
          <div class="table-wrap"><table><caption class="visually-hidden">Authenticated EVE characters</caption><thead><tr><th>Character</th><th>ESI permissions</th><th>Refresh progress</th><th>EVE ID</th><th>Last authenticated</th></tr></thead>
          <tbody>@for (character of characters(); track character.characterId) {
            <tr><td><span class="character-name">
              @if (!progressError() && isRefreshing(character)) { <mat-spinner [diameter]="16" [strokeWidth]="2" [attr.aria-label]="'Refreshing ' + character.characterName" /> }
              <a [routerLink]="['/characters', character.characterId]">{{ character.characterName }}</a></span></td>
              <td><span tabindex="0" class="permission-badge" [class.permission-warning]="!character.permissions?.hasAllRequiredScopes"
                [matTooltip]="permissionTooltip(character)">{{ character.permissions?.hasAllRequiredScopes ? '✓ Permissions OK' : character.permissions?.scopesKnown ? '⚠ Missing permissions' : '⚠ Permissions not checked' }}</span></td>
              <td><span [class.muted]="!isRefreshing(character)">{{ progressLabel(character) }}</span>
                @if (character.progress?.totalSteps) { <small title="Current section / total sections; not a count of successful updates">{{ character.progress!.currentStep }}/{{ character.progress!.totalSteps }} stages</small> }
                @if (character.progress?.error) { <small class="refresh-failure">{{ character.progress!.error }}</small> }
              </td><td>{{ character.characterId }}</td><td>{{ character.lastAuthenticatedAt | date:'medium' }}</td></tr>
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
  styles: `code { overflow-wrap: anywhere; } details p { margin-top: 16px; } summary { cursor: pointer; } .character-name { display:flex; align-items:center; gap:8px; } mat-spinner { flex-shrink:0; } .permission-badge { font-size:12px; white-space:nowrap; } .permission-warning { color:#ffd180; } .refresh-failure { color:#ffb4ab; }`,
})
export class Characters {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
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
  readonly refreshingAll = signal(false);
  readonly refreshMessage = signal('');
  readonly refreshError = signal('');
  readonly progressError = signal('');
  private pollTimer: ReturnType<typeof setTimeout> | undefined;
  private pollRequest: Subscription | undefined;
  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.pollTimer));
    this.load();
  }
  permissionTooltip(character: Character) {
    const permissions = character.permissions;
    return !permissions?.scopesKnown ? 'Granted permissions have not been checked. Refresh EVE data or re-authorise this character.'
      : permissions.hasAllRequiredScopes ? 'All required ESI permissions granted' : `Missing ${permissions.missingScopeCount} required ESI permissions`;
  }
  isRefreshing(character: Character) { return ['queued', 'running'].includes(character.progress?.state ?? ''); }
  progressLabel(character: Character) {
    const progress = character.progress;
    if (!progress || progress.state === 'idle') return 'Idle';
    if (progress.state === 'running') return this.sectionLabel(progress.section);
    return ({ queued: 'Queued', complete: 'Updated', partial: 'Needs attention', failed: 'Failed' } as Record<string, string>)[progress.state] ?? progress.state;
  }
  sectionLabel(section: string | null) {
    return ({ wallet: 'Wallet', skills: 'Skills', skillQueue: 'Skill queue', industryJobs: 'Industry jobs', marketOrders: 'Market orders', assets: 'Assets', blueprints: 'Blueprints' } as Record<string, string>)[section ?? ''] ?? 'Refreshing';
  }
  private schedulePoll() {
    clearTimeout(this.pollTimer);
    this.pollTimer = setTimeout(() => this.pollCharacters(), this.progressError() || this.characters().some(character => this.isRefreshing(character)) ? 1500 : 10000);
  }
  pollCharacters() {
    clearTimeout(this.pollTimer);
    this.pollRequest?.unsubscribe();
    this.pollRequest = this.http.get<Character[]>('/api/eve/characters').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: characters => { this.characters.set(characters); this.progressError.set(''); this.schedulePoll(); },
      error: () => { this.progressError.set('Refresh status could not be updated. Displayed progress may be out of date.'); this.schedulePoll(); },
    });
  }
  refreshAll() {
    if (this.refreshingAll() || !this.characters().length) return;
    this.refreshingAll.set(true); this.refreshMessage.set(''); this.refreshError.set('');
    forkJoin(this.characters().map(character =>
      this.http.post(`/api/eve/characters/${character.characterId}/refresh`, {}, { headers: { 'X-Ghost-Watch': '1' } }).pipe(
        map(() => ({ name: character.characterName, state: 'queued' })),
        catchError(error => of({ name: character.characterName, state: error.status === 409 ? 'already' : 'failed' })),
      ),
    )).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.refreshingAll.set(false))).subscribe(results => {
      const queued = results.filter(result => result.state === 'queued').length;
      const already = results.filter(result => result.state === 'already').length;
      const failed = results.filter(result => result.state === 'failed');
      this.refreshMessage.set(`${queued} queued; ${already} already queued or refreshing. Progress is shown beside each character.`);
      if (failed.length) this.refreshError.set(`Could not queue: ${failed.map(result => result.name).join(', ')}. Try again; characters already refreshing will be skipped.`);
      this.pollCharacters();
    });
  }
  load() {
    clearTimeout(this.pollTimer); this.pollRequest?.unsubscribe();
    this.loading.set(true); this.error.set('');
    forkJoin({ config: this.http.get<EveConfig>('/api/auth/eve/config'), characters: this.http.get<Character[]>('/api/eve/characters') }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => { this.config.set(result.config); this.characters.set(result.characters); this.loading.set(false); this.progressError.set(''); this.schedulePoll(); },
      error: () => { this.error.set('Character connections could not be loaded. Check that the local API is running.'); this.loading.set(false); },
    });
  }
}
