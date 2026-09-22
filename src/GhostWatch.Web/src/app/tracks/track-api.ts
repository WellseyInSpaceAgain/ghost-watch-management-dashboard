import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface Track {
  id: string;
  name: string;
  description: string;
  status: string;
  purpose: string;
  notes: string;
  defaultCapitalPoolId?: string | null;
  createdAt: string;
  updatedAt: string;
  archivedAt: string | null;
  revision: number;
}
export interface TrackOptions { statuses: string[]; purposes: string[]; }
export type TrackDraft = Pick<Track, 'name' | 'description' | 'status' | 'purpose' | 'notes' | 'defaultCapitalPoolId'> & { revision?: number };

@Injectable({ providedIn: 'root' })
export class TrackApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/economics/tracks';
  list(includeArchived = false) { return this.http.get<Track[]>(this.base, { params: { includeArchived } }); }
  get(id: string) { return this.http.get<Track>(`${this.base}/${id}`); }
  options() { return this.http.get<TrackOptions>(`${this.base}/options`); }
  create(draft: TrackDraft) { return this.http.post<Track>(this.base, draft); }
  update(id: string, draft: TrackDraft) { return this.http.put<Track>(`${this.base}/${id}`, draft); }
}

export function requestError(error: HttpErrorResponse): string {
  if (error.status === 0) return 'The local API could not be reached. Check that the backend is running, then try again.';
  if (error.status === 404) return 'The requested record could not be found.';
  if (error.status === 409) return 'This record changed in another editor. Copy any unsaved notes, then reload the latest version before saving.';
  if (error.error?.errors) return Object.values(error.error.errors).flat().join(' ');
  if (error.error?.title && error.status < 500) return [error.error.title, error.error.detail].filter(Boolean).join(' ');
  return 'The request failed. Your edits have not been discarded. Please try again.';
}
