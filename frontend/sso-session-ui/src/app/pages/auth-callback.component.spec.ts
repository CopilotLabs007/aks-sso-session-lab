import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { AuthCallbackComponent } from './auth-callback.component';
import { SessionStore } from '../services/session.store';
import { SessionResponse } from '../models/auth.models';

const mockSession: SessionResponse = {
  username: 'mock-user',
  sessionId: 'mock-session-id',
  createdAt: '2026-01-01T00:00:00Z',
  expiresAt: '2026-01-01T00:06:00Z',
  remainingSeconds: 360,
  status: 'active'
};

describe('AuthCallbackComponent', () => {
  let httpMock: HttpTestingController;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  function setup(queryParams: Record<string, string>) {
    TestBed.configureTestingModule({
      imports: [AuthCallbackComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
    httpMock = TestBed.inject(HttpTestingController);
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AuthCallbackComponent);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  it('exchanges code and state for a session, then opens the dashboard', () => {
    setup({ code: 'mock-code', state: 'mock-state' });

    const request = httpMock.expectOne('/api/auth/create-session');
    expect(request.request.body).toEqual({ code: 'mock-code', state: 'mock-state' });
    request.flush(mockSession);

    expect(TestBed.inject(SessionStore).session()).toEqual(mockSession);
    expect(navigateByUrl).toHaveBeenCalledWith('/dashboard', { replaceUrl: true });
  });

  it('does not call the API when state is missing', () => {
    const fixture = setup({ code: 'mock-code' });

    httpMock.expectNone('/api/auth/create-session');
    expect(fixture.componentInstance.stateStatus()).toBe('Missing code or state');
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('callback is incomplete');
  });

  it('shows the API error when the code is rejected', async () => {
    const fixture = setup({ code: 'used-code', state: 'mock-state' });

    httpMock.expectOne('/api/auth/create-session').flush(
      { message: 'The authorization code or state is invalid, expired, or already used.' },
      { status: 400, statusText: 'Bad Request' }
    );
    await fixture.whenStable();

    expect(fixture.componentInstance.requestStatus()).toBe('Failed');
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('already used');
    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(TestBed.inject(SessionStore).session()).toBeNull();
  });
});
