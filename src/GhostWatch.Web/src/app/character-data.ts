import { CharacterPlan } from './character-plan';
import { EvePermissions } from './eve-permissions';
import { InventoryData, InventoryView } from './inventory-view';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, JsonPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface Section { name: string; attemptedAt: string | null; updatedAt: string | null; error: string | null; warning?: string | null; data: unknown; }
interface Capacity { manufacturingJobs: number; researchJobs: number; reactionJobs: number; marketOrders: number; piColonies: number; }
interface Job { productName: string; blueprintName: string; jobId: number; activityId: number; productTypeId: number | null; blueprintTypeId: number; runs: number; status: string; startDate: string; endDate: string; lastSeenAt: string; }
interface CharacterData {
  permissions?: EvePermissions;
  character: { characterId: number; characterName: string };
  progress: { state: string; section: string | null; error: string | null };
  sections: Section[];
  capacity: { trained: Capacity; active: Capacity | null } | null;
  jobs: Job[];
  inventory?: InventoryData;
}

@Component({
  selector: 'app-character-data',
  imports: [DatePipe, DecimalPipe, JsonPipe, RouterLink, MatButtonModule, InventoryView, CharacterPlan],
  template: `
    <a routerLink="/characters" class="back-link">← Characters</a>
    <p class="eyebrow">EVE DATA / CHARACTER</p>
    <div class="page-heading"><div><h1>{{ data()?.character?.characterName || 'Character data' }}</h1><p class="muted">Factual EVE state · local Track planning remains separate</p></div>
      <button mat-flat-button (click)="refresh()" [disabled]="!data() || busy() || starting()">{{ busy() || starting() ? 'Refresh in progress…' : 'Refresh EVE data' }}</button></div>
    @if (authOutcome) { <p [class]="reauthorised ? 'notice' : 'error'" [attr.role]="reauthorised ? 'status' : 'alert'">{{ authOutcome }}</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }} <button mat-button (click)="load()">Retry loading</button></p> }
    @if (data(); as current) {
      <p role="status">Refresh: {{ current.progress.state }} @if (current.progress.section) { · {{ label(current.progress.section) }} }</p>
      @if (current.progress.error) { <p class="error" role="alert">{{ current.progress.error }}</p> }
      @if (current.progress.state === 'partial') { <p class="notice">Some sections could not be refreshed, or names and categories are incomplete. Check errors, warnings and last successful update times below.</p> }
      <app-character-plan [characterId]="current.character.characterId" />
      <section class="panel" aria-labelledby="permissions-title"><h2 id="permissions-title">ESI Permissions</h2>
        @if (current.permissions; as permissions) {
          @if (!permissions.scopesKnown) {
            <p class="notice">Status: Granted permissions have not been checked.</p><p>Refresh EVE data to check this connection, or re-authorise to grant the current permissions. Previously collected data is retained.</p>
          } @else if (permissions.hasAllRequiredScopes) {
            <p>Status: All required permissions granted</p>
          } @else {
            <p class="notice">Status: {{ permissions.missingScopeCount }} required {{ permissions.missingScopeCount === 1 ? 'permission' : 'permissions' }} missing</p>
          }
          @if (!permissions.hasAllRequiredScopes) {
            <p>{{ permissions.scopesKnown ? 'Missing permissions:' : 'Required permissions not yet verified:' }}</p>
            <ul>@for (scope of permissions.missingScopes; track scope) { <li><code>{{ scope }}</code></li> }</ul>
            <p>Ensure these permissions are enabled in your EVE application registration, then select this same character during re-authorisation. Your local data will be retained.</p>
            <a mat-flat-button [href]="reauthoriseUrl">Re-authorise Character</a>
          } @else { <a mat-stroked-button [href]="reauthoriseUrl">Re-authorise Character</a> }
        } @else { <p>Permission status unavailable. Reload this page to try again.</p> }
      </section>
      <section class="panel"><h2>Wallet balance</h2>
        @if (wallet() !== null) { <p class="balance">{{ wallet() | number:'1.2-2' }} ISK</p> }
        @else { <p class="muted">Unknown — refresh this character to collect a balance.</p> }
        <p class="muted">A factual character wallet; conceptual Capital Pools will be managed separately.</p>
      </section>
      <section class="panel"><h2>Skill capacity</h2>
        @if (current.capacity; as capacity) {
          <div class="table-wrap"><table><thead><tr><th>Capacity</th><th>Trained potential</th><th>Active skill potential</th></tr></thead><tbody>
            @for (metric of capacities; track metric.key) { <tr><td>{{ metric.label }}</td><td>{{ capacity.trained[metric.key] }}</td><td>{{ capacity.active ? capacity.active[metric.key] : 'Unknown' }}</td></tr> }
          </tbody></table></div>
        } @else { <p class="muted">Unknown — no successful skill data refresh yet.</p> }
        <p class="muted" style="margin-top:16px">These are skill-based limits, not free slots or recipe eligibility. PI skill potential does not verify subscription, export or facility access. Missing active skill levels remain unknown.</p>
      </section>
      <section class="panel"><h2>Industry jobs</h2>
        @if (current.jobs.length) {
          <div class="table-wrap"><table><thead><tr><th>Job</th><th>Activity</th><th>Product / Blueprint</th><th>Runs</th><th>Status</th><th>End</th><th>Last seen</th></tr></thead><tbody>
          @for (job of current.jobs; track job.jobId) { <tr><td>{{ job.jobId }}</td><td>{{ activity(job.activityId) }}</td><td>{{ job.productName || job.blueprintName || 'Product name unavailable' }}<small>Type {{ job.productTypeId ?? job.blueprintTypeId }}</small></td><td>{{ job.runs }}</td><td>{{ job.status }}</td><td>{{ job.endDate | date:'medium' }}</td><td>{{ job.lastSeenAt | date:'medium' }}</td></tr> }
          </tbody></table></div>
        } @else { <p class="muted">{{ section('industryJobs')?.updatedAt ? 'No industry jobs returned by EVE.' : 'Industry jobs have not been collected yet.' }}</p> }
        <p class="muted" style="margin-top:16px">Jobs remain in local history when they leave EVE's response window. Status is last observed, not inferred. Run associations are not available yet.</p>
      </section>
      <app-inventory-view [inventory]="current.inventory ?? null" [sections]="current.sections" />
      <section class="panel"><h2>Data freshness and collected records</h2><p class="muted">Errors retain the last successful result. Expand a section to inspect raw EVE records, including skill queue and market orders. These diagnostic records retain the original EVE IDs.</p>
        @for (section of current.sections; track section.name) {
          <details class="data-section"><summary>{{ label(section.name) }} · {{ section.updatedAt ? 'Collected' : 'Not collected' }}{{ section.error ? ' · Refresh failed' : '' }}</summary>
            <p class="muted">Last successful update: {{ section.updatedAt ? (section.updatedAt | date:'medium') : 'Never' }} · Last attempt: {{ section.attemptedAt ? (section.attemptedAt | date:'medium') : 'Never' }}</p>
            @if (section.error) { <p class="error">{{ section.error }}</p> }
            @if (section.warning) { <p class="notice">{{ section.warning }}</p> }
            <pre>{{ section.data | json }}</pre>
          </details>
        }
      </section>
    } @else if (!error()) { <p role="status">Loading character data…</p> }
  `,
  styles: `code { overflow-wrap:anywhere; } .balance { font-size: 24px; } .data-section { border-top: 1px solid #29353d; padding: 12px 0; } summary { cursor: pointer; } details p { margin-top: 12px; } pre { overflow: auto; max-height: 360px; font-size: 12px; }`,
})
export class CharacterDataPage {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly id = this.route.snapshot.paramMap.get('id');
  readonly reauthoriseUrl = `/api/auth/eve/start?characterId=${this.id}`;
  private readonly auth = this.route.snapshot.queryParamMap.get('auth');
  readonly reauthorised = this.auth === 'reauthorised';
  readonly authOutcome = this.auth ? ({
    reauthorised: 'Character re-authorised successfully. Permissions have been updated.',
    cancelled: 'Re-authorisation was cancelled. Your existing connection and data were retained.',
    'wrong-character': 'A different EVE character was selected. Select this character when trying again. Your existing connection and data were retained.',
    'ownership-changed': 'EVE reported a different character owner. Your existing connection and data were retained.',
    configuration: 'EVE authentication needs local configuration before you can re-authorise.',
    'invalid-token': 'The EVE identity or permissions could not be verified. Your existing connection and data were retained.',
    'invalid-grant': 'The EVE login code was rejected. Try re-authorising again. Your existing connection and data were retained.',
    'invalid-client': 'EVE rejected the application credentials. Check the local SSO configuration. Your existing connection and data were retained.',
    'network-error': 'EVE could not be reached. Try again shortly. Your existing connection and data were retained.',
    timeout: 'EVE authentication timed out. Try re-authorising again. Your existing connection and data were retained.',
  } as Record<string, string>)[this.auth] ?? 'Re-authorisation failed. Your existing connection and data were retained. Try again.' : '';
  private timer: ReturnType<typeof setTimeout> | undefined;
  private request = 0;
  readonly data = signal<CharacterData | null>(null);
  readonly error = signal('');
  readonly starting = signal(false);
  readonly capacities: { key: keyof Capacity; label: string }[] = [
    { key: 'manufacturingJobs', label: 'Manufacturing jobs' }, { key: 'researchJobs', label: 'Research jobs' },
    { key: 'reactionJobs', label: 'Reaction jobs' }, { key: 'marketOrders', label: 'Market orders' }, { key: 'piColonies', label: 'PI colonies' },
  ];
  constructor() { this.destroyRef.onDestroy(() => clearTimeout(this.timer)); this.load(); }
  busy() { return ['queued', 'running'].includes(this.data()?.progress.state ?? ''); }
  section(name: string) { return this.data()?.sections.find(section => section.name === name); }
  wallet(): number | null { const value = this.section('wallet')?.data; return typeof value === 'number' ? value : null; }
  label(name: string) { return ({ wallet: 'Wallet', skills: 'Skills', skillQueue: 'Skill queue', industryJobs: 'Industry jobs', marketOrders: 'Market orders', assets: 'Assets', blueprints: 'Blueprints' } as Record<string, string>)[name] ?? name; }
  activity(id: number) { return ({ 1: 'Manufacturing', 3: 'Time research', 4: 'Material research', 5: 'Copying', 8: 'Invention', 11: 'Reactions' } as Record<number, string>)[id] ?? `Activity ${id}`; }
  load() {
    clearTimeout(this.timer);
    const request = ++this.request;
    this.http.get<CharacterData>(`/api/eve/characters/${this.id}/data`).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => { if (request !== this.request) return; this.data.set(data); this.error.set(''); if (this.busy()) this.timer = setTimeout(() => this.load(), 1500); },
      error: error => { if (request === this.request) this.error.set(error.status === 404 ? 'This character was not found.' : 'Character data could not be loaded. Try again.'); },
    });
  }
  refresh() {
    if (this.busy() || this.starting()) return;
    this.starting.set(true); this.error.set('');
    this.http.post(`/api/eve/characters/${this.id}/refresh`, {}, { headers: { 'X-Ghost-Watch': '1' } }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.starting.set(false); this.load(); },
      error: error => { this.starting.set(false); if (error.status === 409) this.load(); else this.error.set('Refresh could not be started. Try again.'); },
    });
  }
}
