import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthApiService } from '../services/auth-api.service';

type ApiStatus = 'checking' | 'online' | 'offline';

@Component({
  selector: 'app-login',
  imports: [RouterLink],
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit {
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);
  readonly apiStatus = signal<ApiStatus>('checking');
  readonly startingLogin = signal(false);
  readonly loginError = signal('');

  ngOnInit(): void {
    this.authApi.health().subscribe({
      next: () => this.apiStatus.set('online'),
      error: () => this.apiStatus.set('offline')
    });
  }

  beginLogin(): void {
    if (this.startingLogin()) {
      return;
    }

    this.startingLogin.set(true);
    this.loginError.set('');
    this.authApi.startLogin().subscribe({
      next: (redirectPath) => {
        void this.router.navigateByUrl(redirectPath);
        this.startingLogin.set(false);
      },
      error: () => {
        this.startingLogin.set(false);
        this.loginError.set('The API could not start sign-in. Confirm it is running and configured, then try again.');
      }
    });
  }
}