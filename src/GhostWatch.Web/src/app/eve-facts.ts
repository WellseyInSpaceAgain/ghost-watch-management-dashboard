import { Component, input } from '@angular/core';
import { FactualTable, FactColumn, FactRow } from './factual-table';
import { DatePipe } from '@angular/common';
interface Evidence { skillId: number; name: string; trainedLevel: number; activeLevel: number | null; }
export interface Assessment { accountState: string; piAvailability: string; areas: { key: string; label: string; summary: string; evidence: Evidence[] }[]; dormantSkills: Evidence[]; productionReadiness: { label: string; summary: string }[]; }
export interface FactStatus { name: string; updatedAt: string | null; error: string | null; warning?: string | null; }
@Component({
  selector: 'app-eve-facts', imports: [FactualTable, DatePipe],
  template: `
    <section class="panel"><h2>Economic foundations</h2>
      @if (assessment(); as assessment) {
        <p>{{ assessment.accountState }} · {{ assessment.piAvailability }}</p>
        @for (area of assessment.areas; track area.key) { <details><summary>{{ area.label }} · {{ area.summary }}</summary>
          <div class="table-wrap"><table><thead><tr><th>Skill evidence</th><th>Trained</th><th>Currently active</th></tr></thead><tbody>
            @for (skill of area.evidence; track skill.skillId) { <tr><td>{{ skill.name }}</td><td>{{ skill.trainedLevel }}</td><td>{{ skill.activeLevel ?? 'Unknown' }}</td></tr> }
            @empty { <tr><td colspan="3">No matching trained skills recorded.</td></tr> }</tbody></table></div></details> }
        <p class="muted">These foundations describe skill evidence, not assignments or recipe eligibility. Active levels come from EVE; manually selecting Omega does not activate skills.</p>
        <p>{{ assessment.dormantSkills.length }} recorded skills have active levels below their trained levels.</p>
        @for (readiness of assessment.productionReadiness; track readiness.label) { <p class="muted">{{ readiness.label }}: {{ readiness.summary }}</p> }
      } @else { <p class="muted">Refresh skills to assess economic foundations.</p> }
    </section>
    @for (view of views; track view.key) {
      <section class="panel" [attr.aria-label]="view.title"><h2>{{ view.title }}</h2>
        @if (status(view.key); as section) {
          <p class="muted">Last successful update: {{ section.updatedAt ? (section.updatedAt | date:'medium') : 'Never' }}</p>
          @if (section.error) { <p class="error">{{ section.error }}</p> }
          @if (section.warning) { <p class="notice">{{ section.warning }}</p> }
        }
        @if (facts()?.[view.key] != null) { <app-factual-table [title]="view.title" [rows]="rows(view.key)" [columns]="view.columns" /> }
        @else { <p class="muted">Not collected yet. Refresh this character to collect {{ view.title.toLowerCase() }}.</p> }
        @if (view.key === 'planets') { <p class="muted">Deployed colony summaries from EVE; this view does not infer production, extraction rates or export access.</p> }
        @if (view.key === 'marketOrders') { <p class="muted">Remaining buy value is an order commitment; listed sell value is not realised revenue.</p> }
      </section>
    }
  `,
  styles: `details {border-top:1px solid #29353d;padding:12px 0;} summary {cursor:pointer;} details + p {margin-top:16px;}`,
})
export class EveFacts {
  readonly facts = input<Record<string, unknown> | null>(null); readonly assessment = input<Assessment | null>(null); readonly sections = input<FactStatus[]>([]);
  readonly views: {key: string; title: string; columns: FactColumn[]}[] = [
    { key: 'skills', title: 'Skills', columns: [{key:'skill_name',label:'Skill'},{key:'group_name',label:'Group'},{key:'trained_skill_level',label:'Trained level'},{key:'active_skill_level',label:'Active level'},{key:'skillpoints_in_skill',label:'Skill points',format:'number'}] },
    { key: 'skillQueue', title: 'Skill queue', columns: [{key:'skill_name',label:'Skill'},{key:'finished_level',label:'Target level'},{key:'start_date',label:'Start',format:'date'},{key:'finish_date',label:'Finish',format:'date'}] },
    { key: 'marketOrders', title: 'Market orders', columns: [{key:'type_name',label:'Item'},{key:'side',label:'Side'},{key:'volume_remain',label:'Remaining',format:'number'},{key:'price',label:'Unit price',format:'isk'},{key:'remaining_value',label:'Remaining value',format:'isk'},{key:'location_name',label:'Location'},{key:'issued',label:'Issued',format:'date'}] },
    { key: 'planets', title: 'Planetary Interaction', columns: [{key:'planet_name',label:'Planet'},{key:'solar_system_name',label:'System'},{key:'planet_type',label:'Type'},{key:'upgrade_level',label:'Command center level'},{key:'num_pins',label:'Pins'},{key:'last_update',label:'Last colony update',format:'date'}] },
    { key: 'standings', title: 'Standings', columns: [{key:'from_name',label:'Entity'},{key:'from_type',label:'Kind'},{key:'standing',label:'Standing',format:'number'}] },
    { key: 'loyalty', title: 'Loyalty points', columns: [{key:'corporation_name',label:'Corporation'},{key:'loyalty_points',label:'Loyalty points',format:'number'}] },
  ];
  status(name: string) { return this.sections().find(x => x.name === name); }
  rows(name: string): FactRow[] { const data = this.facts()?.[name]; return (name === 'skills' ? (data as {skills: FactRow[]})?.skills : data) as FactRow[] ?? []; }
}
