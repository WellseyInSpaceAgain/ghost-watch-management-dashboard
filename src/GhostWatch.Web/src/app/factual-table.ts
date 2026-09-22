import { Component, computed, input, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatPaginatorModule } from '@angular/material/paginator';
export interface FactColumn { key: string; label: string; format?: 'number' | 'date' | 'isk'; }
export type FactRow = Record<string, string | number | boolean | null>;
@Component({
  selector: 'app-factual-table', imports: [DatePipe, DecimalPipe, MatPaginatorModule],
  template: `<label class="fact-search">Search {{ title() }}<input [attr.aria-label]="'Search ' + title()" (input)="query.set($any($event.target).value); page.set(0)" placeholder="Search names or values"></label>
    <div class="table-wrap"><table><caption class="visually-hidden">{{ title() }}</caption><thead><tr>@for (column of columns(); track column.key) { <th>{{ column.label }}</th> }</tr></thead><tbody>
      @for (row of visible(); track $index) { <tr>@for (column of columns(); track column.key) { <td>
        @if (row[column.key] === null || row[column.key] === undefined) { Unknown }
        @else if (column.format === 'date') { {{ $any(row[column.key]) | date:'medium' }} }
        @else if (column.format === 'isk') { {{ $any(row[column.key]) | number:'1.2-2' }} ISK }
        @else if (column.format === 'number') { {{ $any(row[column.key]) | number }} }
        @else { {{ row[column.key] }} }
      </td> }</tr> } @empty { <tr><td [attr.colspan]="columns().length">{{ rows().length ? 'No records match this search.' : 'No records returned by EVE.' }}</td></tr> }
    </tbody></table></div><mat-paginator [attr.aria-label]="title() + ' pages'" [length]="filtered().length" [pageSize]="25" [pageIndex]="pageIndex()" (page)="page.set($event.pageIndex)" />`,
  styles: `.fact-search {display:flex; align-items:center; gap:12px; margin:12px 0;} input {background:#17232b; color:#dce6eb; border:1px solid #445b68; padding:8px; min-width:0;}`,
})
export class FactualTable {
  readonly title = input.required<string>(); readonly rows = input<FactRow[]>([]); readonly columns = input.required<FactColumn[]>();
  readonly query = signal(''); readonly page = signal(0);
  readonly filtered = computed(() => this.rows().filter(row => this.columns().some(column => String(row[column.key] ?? '').toLowerCase().includes(this.query().trim().toLowerCase()))));
  readonly pageIndex = computed(() => Math.min(this.page(), Math.max(0, Math.ceil(this.filtered().length / 25) - 1)));
  readonly visible = computed(() => this.filtered().slice(this.pageIndex() * 25, (this.pageIndex() + 1) * 25));
}
