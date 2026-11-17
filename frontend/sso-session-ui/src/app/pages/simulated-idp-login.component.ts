import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthApiService } from '../services/auth-api.service';

@Component({
  selector: 'app-simulated-idp-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './simulated-idp-login.component.html'
})
export class SimulatedIdpLoginComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder).nonNullable;
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly state = signal('');
  readonly submitting = signal(false);
  readonly errorMessage = signal('');
  readonly form = this.formBuilder.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  ngOnInit(): void {
    this.state.set(this.route.snapshot.queryParamMap.get('state') ?? '');
    if (!this.state()) {
      this.errorMessage.set('The sign-in state is missing. Return to the application and start again.');
    }
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.state() || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');
    this.authApi.authenticateDemoUser({
      ...this.form.getRawValue(),
      state: this.state()
    }).subscribe({
      next: ({ callbackUrl }) => {
        const callback = new URL(callbackUrl, window.location.origin);
        if (callback.origin !== window.location.origin || callback.pathname !== '/auth-callback') {
          this.errorMessage.set('The identity provider returned an unexpected callback address.');
          this.submitting.set(false);
          return;
        }

        void this.router.navigateByUrl(`${callback.pathname}${callback.search}`, { replaceUrl: true });
      },
      error: (error: unknown) => {
        this.errorMessage.set(this.getErrorMessage(error));
        this.submitting.set(false);
      }
    });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error?.message === 'string') {
        return error.error.message;
      }
      if (error.status === 0) {
        return 'The API could not be reached. Confirm it is running, then start a fresh sign-in.';
      }
      if (error.status === 400) {
        return 'The sign-in state is invalid or expired. Return to the app and start a fresh sign-in.';
      }
      if (error.status === 401) {
        return 'The configured demo credentials were rejected. Verify them locally and try a fresh sign-in.';
      }
      return `The API could not complete sign-in (HTTP ${error.status}). Check its logs and start a fresh sign-in.`;
    }
    return 'The simulated identity provider could not complete sign-in.';
  }
}