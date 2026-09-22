import { Component } from '@angular/core';

@Component({
  selector: 'app-overview',
  template: `
    <p class="eyebrow">ECONOMICS / OVERVIEW</p>
    <h1>Economic operations</h1>
    <p class="subtitle">Plan the programmes. Track the work. Preserve what you learn.</p>
    <section class="metrics" aria-label="Programme metrics">
      @for (metric of metrics; track metric.name) {
        <article class="metric"><h2>{{ metric.name }}</h2><strong>—</strong><p>{{ metric.reason }}</p></article>
      }
    </section>
    <section class="panel">
      <h2>Start your economic programme</h2>
      <p>This is a new local workspace. No EVE data or economic history has been imported.</p>
      <p class="muted">Economy Tracks will organise your strategy, with Runs recording the work and Capital Pools representing your manual allocations.</p>
    </section>
  `,
})
export class Overview {
  readonly metrics = [
    { name: 'Core Capital', reason: 'No capital pool configured' },
    { name: '30-day realised profit', reason: 'No recorded financial results' },
    { name: 'Ghost Watch Treasury', reason: 'No treasury configured' },
    { name: 'Replacement coverage', reason: 'No replacement package configured' },
  ];
}
