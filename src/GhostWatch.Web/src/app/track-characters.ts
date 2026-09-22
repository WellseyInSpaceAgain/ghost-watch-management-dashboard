import { Component, inject, input, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
@Component({selector: 'app-track-characters', imports: [RouterLink], template: `<section class="panel"><h2>Linked characters</h2>@if (error()) { <p class="error">{{ error() }}</p> } @for (character of characters(); track character.characterId) { <p><a [routerLink]="['/characters', character.characterId]">{{ character.characterName }}</a></p> } @empty { <p class="muted">Link characters to this Track from their Economic assignment section.</p> }</section>`})
export class TrackCharacters implements OnInit {
  readonly trackId = input.required<string>(); private readonly http = inject(HttpClient);
  readonly characters = signal<{characterId: number; characterName: string}[]>([]); readonly error = signal('');
  ngOnInit() { this.http.get<{characterId: number; characterName: string}[]>(`/api/management/tracks/${this.trackId()}/characters`).subscribe({next: rows => this.characters.set(rows), error: () => this.error.set('Linked characters could not be loaded. Reload this page.')}); }
}
