import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthApiService } from './auth-api.service';
import { SessionResponse } from '../models/auth.models';

const mockSession: SessionResponse = {
  username: 'mock-user',
  sessionId: 'mock-session-id',
  createdAt: '2026-01-01T00:00:00Z',
  expiresAt: '2026-01-01T00:06:00Z',
  remainingSeconds: 360,
  status: 'active'
};

describe('AuthApiService', () => {
  let service: AuthApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the authorization code and state to create-session', async () => {
    const result = firstValueFrom(service.createSession({ code: 'mock-code', state: 'mock-state' }));

    const request = httpMock.expectOne('/api/auth/create-session');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ code: 'mock-code', state: 'mock-state' });
    request.flush(mockSession);

    expect(await result).toEqual(mockSession);
  });

  it('posts demo credentials and state to the simulated IdP', () => {
    service.authenticateDemoUser({ username: 'mock-user', password: 'mock-password', state: 'mock-state' }).subscribe();

    const request = httpMock.expectOne('/api/auth/sso-authenticate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ username: 'mock-user', password: 'mock-password', state: 'mock-state' });
    request.flush({ callbackUrl: '/auth-callback?code=c&state=s' });
  });

  it.each([
    ['getSession', 'GET', '/api/auth/session'],
    ['refreshSession', 'POST', '/api/auth/refresh-session'],
    ['logout', 'POST', '/api/auth/logout']
  ] as const)('%s calls %s %s', (method, httpMethod, url) => {
    (service[method]() as Observable<unknown>).subscribe();

    const request = httpMock.expectOne(url);
    expect(request.request.method).toBe(httpMethod);
    request.flush(method === 'logout' ? null : mockSession);
  });

  it('rejects a sign-in redirect that does not land on the simulated IdP page', async () => {
    const result = firstValueFrom(service.startLogin());

    // The testing backend reports the request URL as the final URL, i.e. no redirect to /simulated-idp-login.
    httpMock.expectOne('/api/auth/sso-login').flush('<html></html>');

    await expect(result).rejects.toThrow('unexpected sign-in redirect');
  });
});
