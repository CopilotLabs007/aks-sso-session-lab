import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { timer } from 'rxjs';
import { SessionResponse } from '../models/auth.models';
import { AuthApiService } from '../services/auth-api.service';
import { SessionStore } from '../services/session.store';

@Component({
  selector: 'app-session-status',
  imports: [DatePipe],
  templateUrl: './session-status.component.html'
})
export class SessionStatusComponent {
  private readonly authApi = inject(AuthApiService);
  private readonly sessionStore = inject(SessionStore);
  private expiryCheckSent = false;

  readonly session = input.required<SessionResponse>();
  readonly remainingSeconds = signal(0);
  readonly expired = computed(() => this.remainingSeconds() === 0);
  readonly progressPercent = computed(() => Math.min(100, this.remainingSeconds() / 360 * 100));

  constructor() {
    timer(0, 1000).pipe(takeUntilDestroyed()).subscribe(() => {
      const expiresAt = Date.parse(this.session().expiresAt);
      const remainingSeconds = Math.max(0, Math.ceil((expiresAt - Date.now()) / 1000));
      this.remainingSeconds.set(remainingSeconds);

      if (remainingSeconds > 0) {
        this.expiryCheckSent = false;
      } else if (!this.expiryCheckSent) {
        this.expiryCheckSent = true;
        this.authApi.getSession().subscribe({
          next: (session) => this.sessionStore.set(session),
          error: () => undefined
        });
      }
    });
  }

  formattedRemaining(): string {
    const seconds = this.remainingSeconds();
    const minutesPart = Math.floor(seconds / 60).toString().padStart(2, '0');
    const secondsPart = (seconds % 60).toString().padStart(2, '0');
    return `${minutesPart}:${secondsPart}`;
  }
}