import { Routes } from '@angular/router';
import { Overview } from './overview';
export const routes: Routes = [
  { path: '', component: Overview },
  { path: 'tracks', loadComponent: () => import('./tracks/track-list').then(m => m.TrackList) },
  { path: 'tracks/new', loadComponent: () => import('./tracks/track-detail').then(m => m.TrackDetail) },
  { path: 'tracks/:id', loadComponent: () => import('./tracks/track-detail').then(m => m.TrackDetail) },
  { path: 'characters', loadComponent: () => import('./characters').then(m => m.Characters) },
  { path: 'characters/:id', loadComponent: () => import('./character-data').then(m => m.CharacterDataPage) },
  { path: '**', redirectTo: '' },
];
