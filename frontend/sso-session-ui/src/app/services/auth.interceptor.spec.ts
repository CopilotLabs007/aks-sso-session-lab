import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { SessionStore } from './session.store';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let sessionStore: SessionStore;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    sessionStore = TestBed.inject(SessionStore);
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    vi.spyOn(console, 'info').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    sessionStore.set({
      username: 'mock-user',
      sessionId: 'mock-session-id',
      createdAt: '2026-01-01T00:00:00Z',
      expiresAt: '2026-01-01T00:06:00Z',
      remainingSeconds: 360,
      status: 'active'
    });
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  it('sends every request with credentials so the browser attaches the cookie', () => {
    http.get('/api/auth/session').subscribe();

    const request = httpMock.expectOne('/api/auth/session');
    expect(request.request.withCredentials).toBe(true);
    request.flush({});
  });

  it('clears the session and returns to the login page on 401', () => {
    http.get('/api/auth/session').subscribe({ error: () => undefined });

    httpMock.expectOne('/api/auth/session').flush({ message: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStore.session()).toBeNull();
    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('does not redirect when the simulated IdP rejects credentials', () => {
    http.post('/api/auth/sso-authenticate', {}).subscribe({ error: () => undefined });

    httpMock.expectOne('/api/auth/sso-authenticate').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(sessionStore.session()).not.toBeNull();
  });

  it('does not clear the session on non-401 errors', () => {
    http.get('/api/auth/session').subscribe({ error: () => undefined });

    httpMock.expectOne('/api/auth/session').flush({}, { status: 500, statusText: 'Server Error' });

    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(sessionStore.session()).not.toBeNull();
  });
});
