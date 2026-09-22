import { Component, computed, input, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule } from '@angular/material/paginator';

interface InventoryRow { typeId: number; name: string; category: string; availability: string; quantity: number; itemId?: number; locationId?: number; locationFlag?: string; }
interface BlueprintRow { itemId: number; typeId: number; name: string; kind: string; quantity: number; materialEfficiency: number; timeEfficiency: number; runsRemaining: number | null; locationId: number; locationFlag: string; }
export interface InventoryData { assets: InventoryRow[]; stock: InventoryRow[]; blueprints: BlueprintRow[]; }
interface SectionStatus { name: string; updatedAt: string | null; error: string | null; warning?: string | null; }

@Component({
  selector: 'app-inventory-view',
  imports: [DatePipe, DecimalPipe, MatFormFieldModule, MatInputModule, MatSelectModule, MatPaginatorModule],
  template: `
    <section class="panel" aria-labelledby="assets-title">
      <h2 id="assets-title">Assets</h2>
      @if (status('assets'); as status) {
        <p class="muted">Last successful collection: {{ status.updatedAt ? (status.updatedAt | date:'medium') : 'Never' }}</p>
        @if (status.error) { <p class="error" role="alert">{{ status.error }} Showing the last successful collection, if available.</p> }
        @if (status.warning) { <p class="notice">{{ status.warning }}</p> }
      }
      @if (!status('assets')?.updatedAt) { <p class="muted">Refresh this character to collect assets.</p> }
      @else if (!inventory()?.assets?.length) { <p>No assets returned by EVE.</p> }
      @else {
        <div class="inventory-filters">
          <mat-form-field appearance="outline"><mat-label>Search assets</mat-label><input matInput #assetSearch (input)="search.set(assetSearch.value); assetPage.set(0)" placeholder="Name, type or location ID"></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Availability</mat-label><mat-select [value]="availability()" (selectionChange)="availability.set($event.value); assetPage.set(0)">
            <mat-option value="">All availability</mat-option><mat-option value="Available stock">Available stock</mat-option><mat-option value="Fitted / contained assets">Fitted / contained assets</mat-option><mat-option value="Availability unknown">Availability unknown</mat-option>
          </mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Category</mat-label><mat-select [value]="category()" (selectionChange)="category.set($event.value); assetPage.set(0)">
            <mat-option value="">All categories</mat-option>@for (category of categories(); track category) { <mat-option [value]="category">{{ category }}</mat-option> }
          </mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Asset view</mat-label><mat-select [value]="mode()" (selectionChange)="mode.set($event.value); assetPage.set(0)"><mat-option value="stock">Stock summary</mat-option><mat-option value="assets">Item locations</mat-option></mat-select></mat-form-field>
        </div>
        <div class="table-wrap"><table><caption class="visually-hidden">Character assets</caption><thead><tr><th>Item</th><th>Category</th><th>Quantity</th><th>Availability</th>@if (mode() === 'assets') { <th>Location</th> }</tr></thead><tbody>
        @for (row of visibleAssets(); track $index) {
          <tr><td>{{ row.name }}<small>Type {{ row.typeId }} @if (row.itemId) { · Item {{ row.itemId }} }</small></td><td>{{ row.category }}</td><td>{{ row.quantity | number }}</td><td>{{ row.availability }}</td>
            @if (mode() === 'assets') { <td>{{ row.locationId }}<small>{{ row.locationFlag }}</small></td> }</tr>
        } @empty { <tr><td colspan="5">No assets match these filters.</td></tr> }
        </tbody></table></div>
        <mat-paginator aria-label="Asset pages" [length]="filteredAssets().length" [pageSize]="25" [pageIndex]="assetPageIndex()" (page)="assetPage.set($event.pageIndex)" />
      }
      <p class="muted">Available stock is loose hangar inventory. Fitted or contained items are kept separate; other locations remain unknown. This does not reserve stock or verify access. Switch to Item locations to inspect location IDs.</p>
    </section>
    <section class="panel" aria-labelledby="blueprints-title">
      <h2 id="blueprints-title">Blueprints</h2>
      @if (status('blueprints'); as status) {
        <p class="muted">Last successful collection: {{ status.updatedAt ? (status.updatedAt | date:'medium') : 'Never' }}</p>
        @if (status.error) { <p class="error" role="alert">{{ status.error }} Showing the last successful collection, if available.</p> }
        @if (status.warning) { <p class="notice">{{ status.warning }}</p> }
      }
      @if (!status('blueprints')?.updatedAt) { <p class="muted">Refresh this character to collect blueprints.</p> }
      @else if (!inventory()?.blueprints?.length) { <p>No blueprints returned by EVE.</p> }
      @else {
        <div class="inventory-filters">
          <mat-form-field appearance="outline"><mat-label>Search blueprints</mat-label><input matInput #blueprintSearch (input)="blueprintQuery.set(blueprintSearch.value); blueprintPage.set(0)" placeholder="Name or type ID"></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Blueprint kind</mat-label><mat-select [value]="kind()" (selectionChange)="kind.set($event.value); blueprintPage.set(0)"><mat-option value="">Originals and copies</mat-option><mat-option value="Original">Originals</mat-option><mat-option value="Copy">Copies</mat-option></mat-select></mat-form-field>
        </div>
        <div class="table-wrap"><table><caption class="visually-hidden">Character blueprints</caption><thead><tr><th>Blueprint</th><th>Kind</th><th>Quantity</th><th>ME</th><th>TE</th><th>Runs remaining</th><th>Location</th></tr></thead><tbody>
        @for (row of visibleBlueprints(); track row.itemId) {
          <tr><td>{{ row.name }}<small>Type {{ row.typeId }} · Item {{ row.itemId }}</small></td><td>{{ row.kind }}</td><td>{{ row.quantity | number }}</td><td>{{ row.materialEfficiency }}%</td><td>{{ row.timeEfficiency }}%</td><td>{{ row.kind === 'Original' ? 'Unlimited' : row.runsRemaining }}</td><td>{{ row.locationId }}<small>{{ row.locationFlag }}</small></td></tr>
        } @empty { <tr><td colspan="7">No blueprints match these filters.</td></tr> }
        </tbody></table></div>
        <mat-paginator aria-label="Blueprint pages" [length]="filteredBlueprints().length" [pageSize]="25" [pageIndex]="blueprintPageIndex()" (page)="blueprintPage.set($event.pageIndex)" />
      }
      <p class="muted">ME: material efficiency. TE: time efficiency. Original blueprints are reusable; copies show their remaining licensed runs. Ownership does not establish recipe eligibility.</p>
    </section>
  `,
  styles: `.inventory-filters { display:flex; flex-wrap:wrap; gap:12px; } .inventory-filters mat-form-field { flex:1; min-width:170px; } mat-paginator { margin-bottom:16px; }`,
})
export class InventoryView {
  readonly inventory = input<InventoryData | null>(null);
  readonly sections = input<SectionStatus[]>([]);
  readonly search = signal('');
  readonly availability = signal('');
  readonly category = signal('');
  readonly mode = signal<'stock' | 'assets'>('stock');
  readonly assetPage = signal(0);
  readonly blueprintQuery = signal('');
  readonly kind = signal('');
  readonly blueprintPage = signal(0);
  readonly categories = computed(() => [...new Set(this.inventory()?.assets.map(row => row.category) ?? [])].sort());
  readonly filteredAssets = computed(() => (this.inventory()?.[this.mode()] ?? []).filter(row =>
    (!this.availability() || row.availability === this.availability()) && (!this.category() || row.category === this.category()) &&
    `${row.name} ${row.typeId} ${row.locationId ?? ''}`.toLowerCase().includes(this.search().toLowerCase().trim())));
  readonly assetPageIndex = computed(() => Math.min(this.assetPage(), Math.max(0, Math.ceil(this.filteredAssets().length / 25) - 1)));
  readonly visibleAssets = computed(() => this.filteredAssets().slice(this.assetPageIndex() * 25, (this.assetPageIndex() + 1) * 25));
  readonly filteredBlueprints = computed(() => (this.inventory()?.blueprints ?? []).filter(row =>
    (!this.kind() || row.kind === this.kind()) && `${row.name} ${row.typeId}`.toLowerCase().includes(this.blueprintQuery().toLowerCase().trim())));
  readonly blueprintPageIndex = computed(() => Math.min(this.blueprintPage(), Math.max(0, Math.ceil(this.filteredBlueprints().length / 25) - 1)));
  readonly visibleBlueprints = computed(() => this.filteredBlueprints().slice(this.blueprintPageIndex() * 25, (this.blueprintPageIndex() + 1) * 25));
  status(name: string) { return this.sections().find(section => section.name === name); }
}
