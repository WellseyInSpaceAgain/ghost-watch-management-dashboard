import { Routes } from '@angular/router';
import { Overview } from './overview';
export const routes: Routes = [
  { path: '', component: Overview },
  { path: 'tracks', loadComponent: () => import('./tracks/track-list').then(m => m.TrackList) },
  { path: 'tracks/new', loadComponent: () => import('./tracks/track-detail').then(m => m.TrackDetail) },
  { path: 'tracks/:id', loadComponent: () => import('./tracks/track-detail').then(m => m.TrackDetail) },
  { path: 'playbooks', data: {kind:'playbooks'}, loadComponent: () => import('./knowledge').then(m => m.Knowledge) },
  { path: 'records', data: {kind:'records'}, loadComponent: () => import('./knowledge').then(m => m.Knowledge) },
  { path: 'objectives', loadComponent: () => import('./objectives').then(m => m.Objectives) },
  { path: 'runs', loadComponent: () => import('./runs').then(m => m.Runs) },
  { path: 'runs/new', loadComponent: () => import('./run-detail').then(m => m.RunDetail) },
  { path: 'runs/:id', loadComponent: () => import('./run-detail').then(m => m.RunDetail) },
  { path: 'industry-jobs', loadComponent: () => import('./industry-jobs').then(m => m.IndustryJobs) },
  { path: 'capital', loadComponent: () => import('./capital').then(m => m.Capital) },
  { path: 'accounts', loadComponent: () => import('./accounts').then(m => m.Accounts) },
  { path: 'characters', loadComponent: () => import('./characters').then(m => m.Characters) },
  { path: 'characters/:id', loadComponent: () => import('./character-data').then(m => m.CharacterDataPage) },
  { path: '**', redirectTo: '' },
];
