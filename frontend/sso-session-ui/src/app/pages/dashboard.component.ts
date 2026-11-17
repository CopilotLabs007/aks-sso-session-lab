import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthApiService } from '../services/auth-api.service';
import { SessionStore } from '../services/session.store';
import { SessionStatusComponent } from '../components/session-status.component';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink, SessionStatusComponent],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);
  readonly sessionStore = inject(SessionStore);
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly message = signal('');

  ngOnInit(): void {
    this.authApi.getSession().subscribe({
      next: (session) => {
        this.sessionStore.set(session);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  refresh(): void {
    if (this.refreshing()) {
      return;
    }
    this.refreshing.set(true);
    this.message.set('');
    this.authApi.refreshSession().subscribe({
      next: (session) => {
        this.sessionStore.set(session);
        this.message.set('Session renewed. The cookie now contains a newly signed JWT.');
        this.refreshing.set(false);
      },
      error: () => this.refreshing.set(false)
    });
  }

  logout(): void {
    this.authApi.logout().subscribe({
      next: () => this.finishLogout(),
      error: () => this.finishLogout()
    });
  }

  private finishLogout(): void {
    this.sessionStore.clear();
    void this.router.navigateByUrl('/', { replaceUrl: true });
  }
}