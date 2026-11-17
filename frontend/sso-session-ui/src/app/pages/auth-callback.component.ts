import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthApiService } from '../services/auth-api.service';
import { SessionStore } from '../services/session.store';

@Component({
  selector: 'app-auth-callback',
  imports: [RouterLink],
  templateUrl: './auth-callback.component.html'
})
export class AuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authApi = inject(AuthApiService);
  private readonly sessionStore = inject(SessionStore);

  readonly code = signal('');
  readonly stateStatus = signal('Waiting for callback values');
  readonly requestStatus = signal('Not started');
  readonly errorMessage = signal('');

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code') ?? '';
    const state = this.route.snapshot.queryParamMap.get('state') ?? '';
    this.code.set(code);

    if (!code || !state) {
      this.stateStatus.set('Missing code or state');
      this.requestStatus.set('Not sent');
      this.errorMessage.set('The callback is incomplete. Start a new sign-in from the application.');
      return;
    }

    this.stateStatus.set('Received; validating with the API');
    this.requestStatus.set('Sending POST /api/auth/create-session');
    this.authApi.createSession({ code, state }).subscribe({
      next: (session) => {
        this.stateStatus.set('Validated by the API');
        this.requestStatus.set('Session created; opening dashboard');
        this.sessionStore.set(session);
        void this.router.navigateByUrl('/dashboard', { replaceUrl: true });
      },
      error: (error: unknown) => {
        this.requestStatus.set('Failed');
        this.errorMessage.set(this.getErrorMessage(error));
      }
    });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && typeof error.error?.message === 'string') {
      return error.error.message;
    }
    return 'The application could not create a session. Start sign-in again.';
  }
}