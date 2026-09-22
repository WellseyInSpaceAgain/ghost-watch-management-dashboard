import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { requestError } from './tracks/track-api';
export interface Account { id: string; name: string; subscription: string; notes: string; revision: number; }
@Component({
  selector: 'app-accounts', imports: [FormsModule, MatButtonModule],
  template: `
    <p class="eyebrow">EVE DATA / LOCAL MANAGEMENT</p><h1>Accounts</h1>
    <p class="muted">Manual grouping and subscription status. EVE does not identify your accounts or subscription here. Assign characters from their detail pages.</p>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button mat-button (click)="load()">Reload accounts</button></p> }
    <section class="panel"><div class="section-heading"><h2>Account groups</h2><button mat-stroked-button (click)="reset()">New account</button></div>
      <div class="table-wrap"><table><thead><tr><th>Account</th><th>Subscription</th><th>Notes</th><th></th></tr></thead><tbody>
      @for (account of accounts(); track account.id) { <tr><td>{{ account.name }}</td><td>{{ account.subscription }}</td><td>{{ account.notes }}</td><td><button mat-button (click)="edit(account)">Edit {{ account.name }}</button></td></tr> }
      @empty { <tr><td colspan="4">No account groups yet.</td></tr> }</tbody></table></div>
    </section>
    <form class="panel compact-form" #form="ngForm" (ngSubmit)="save()"><h2>{{ draft.id ? 'Edit account' : 'New account' }}</h2>
      <label>Account name<input name="name" [(ngModel)]="draft.name" required maxlength="120"></label>
      <label>Subscription<select name="subscription" [(ngModel)]="draft.subscription"><option>Unknown</option><option>Alpha</option><option>Omega</option></select></label>
      <label>Account notes<textarea name="notes" [(ngModel)]="draft.notes" maxlength="2000" rows="3"></textarea></label>
      <button mat-flat-button [disabled]="form.invalid || saving() || !draft.name.trim()">Save account</button>
      @if (saved()) { <p role="status">Account saved.</p> }
    </form>`,
})
export class Accounts {
  private readonly http = inject(HttpClient);
  readonly accounts = signal<Account[]>([]); readonly error = signal(''); readonly saving = signal(false); readonly saved = signal(false);
  draft: Account = { id: '', name: '', subscription: 'Unknown', notes: '', revision: 0 };
  constructor() { this.load(); }
  reset() { this.draft = { id: '', name: '', subscription: 'Unknown', notes: '', revision: 0 }; this.saved.set(false); }
  edit(account: Account) { this.draft = { ...account }; this.saved.set(false); }
  load() { this.http.get<Account[]>('/api/management/accounts').subscribe({ next: rows => { this.accounts.set(rows); this.error.set(''); }, error: error => this.error.set(requestError(error)) }); }
  save() {
    this.saving.set(true); this.saved.set(false); this.error.set('');
    const request = this.draft.id ? this.http.put<Account>(`/api/management/accounts/${this.draft.id}`, this.draft) : this.http.post<Account>('/api/management/accounts', this.draft);
    request.subscribe({ next: account => { this.draft = account; this.saving.set(false); this.saved.set(true); this.load(); }, error: error => { this.error.set(requestError(error)); this.saving.set(false); } });
  }
}
