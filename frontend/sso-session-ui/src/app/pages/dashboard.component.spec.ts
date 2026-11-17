import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { SessionStore } from '../services/session.store';
import { SessionResponse } from '../models/auth.models';

function mockSession(overrides: Partial<SessionResponse> = {}): SessionResponse {
  return {
    username: 'mock-user',
    sessionId: 'mock-session-id',
    createdAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 300_000).toISOString(),
    remainingSeconds: 300,
    status: 'active',
    ...overrides
  };
}

describe('DashboardComponent', () => {
  let httpMock: HttpTestingController;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  async function setupWithSession(session: SessionResponse) {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    httpMock = TestBed.inject(HttpTestingController);
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    httpMock.expectOne('/api/auth/session').flush(session);
    await fixture.whenStable();
    return fixture;
  }

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  it('validates the session on load and shows the user', async () => {
    const fixture = await setupWithSession(mockSession());

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('mock-user');
    expect(text).toContain('mock-session-id');
  });

  it('refreshes the session and shows the new expiry', async () => {
    const fixture = await setupWithSession(mockSession());
    const renewedExpiry = new Date(Date.now() + 360_000).toISOString();

    fixture.componentInstance.refresh();
    const request = httpMock.expectOne('/api/auth/refresh-session');
    expect(request.request.method).toBe('POST');
    request.flush(mockSession({ expiresAt: renewedExpiry, remainingSeconds: 360 }));
    await fixture.whenStable();

    expect(TestBed.inject(SessionStore).session()?.expiresAt).toBe(renewedExpiry);
    expect(fixture.componentInstance.message()).toContain('newly signed JWT');
  });

  it('logs out, clears the session, and returns to the login page', async () => {
    const fixture = await setupWithSession(mockSession());

    fixture.componentInstance.logout();
    const request = httpMock.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(TestBed.inject(SessionStore).session()).toBeNull();
    expect(navigateByUrl).toHaveBeenCalledWith('/', { replaceUrl: true });
  });

  it('still clears the session if the logout call fails', async () => {
    const fixture = await setupWithSession(mockSession());

    fixture.componentInstance.logout();
    httpMock.expectOne('/api/auth/logout').flush({}, { status: 500, statusText: 'Server Error' });

    expect(TestBed.inject(SessionStore).session()).toBeNull();
    expect(navigateByUrl).toHaveBeenCalledWith('/', { replaceUrl: true });
  });
});
