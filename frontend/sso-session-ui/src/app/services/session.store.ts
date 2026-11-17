import { Injectable, signal } from '@angular/core';
import { SessionResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  readonly session = signal<SessionResponse | null>(null);

  set(session: SessionResponse): void {
    this.session.set(session);
  }

  clear(): void {
    this.session.set(null);
  }
}