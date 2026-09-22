import { Routes } from '@angular/router';
import { Overview } from './overview';
export const routes: Routes = [
  { path: '', component: Overview },
  { path: '**', redirectTo: '' },
];
