import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  AuthenticateRequest,
  AuthenticationRedirectResponse,
  CreateSessionRequest,
  HealthResponse,
  SessionResponse
} from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);

  health(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>('/api/health');
  }

  authenticateDemoUser(request: AuthenticateRequest): Observable<AuthenticationRedirectResponse> {
    return this.http.post<AuthenticationRedirectResponse>('/api/auth/sso-authenticate', request);
  }

  createSession(request: CreateSessionRequest): Observable<SessionResponse> {
    return this.http.post<SessionResponse>('/api/auth/create-session', request);
  }

  getSession(): Observable<SessionResponse> {
    return this.http.get<SessionResponse>('/api/auth/session');
  }

  refreshSession(): Observable<SessionResponse> {
    return this.http.post<SessionResponse>('/api/auth/refresh-session', {});
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {});
  }

  startLogin(): Observable<string> {
    return this.http.get('/api/auth/sso-login', {
      observe: 'response',
      responseType: 'text'
    }).pipe(
      map((response) => {
        const redirectUrl = new URL(response.url ?? '', window.location.origin);
        if (redirectUrl.origin !== window.location.origin
          || redirectUrl.pathname !== '/simulated-idp-login'
          || !redirectUrl.searchParams.has('state')) {
          throw new Error('The API returned an unexpected sign-in redirect.');
        }

        return `${redirectUrl.pathname}${redirectUrl.search}`;
      })
    );
  }
}