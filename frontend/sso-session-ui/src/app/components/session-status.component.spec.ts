import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SessionStatusComponent } from './session-status.component';
import { SessionResponse } from '../models/auth.models';

describe('SessionStatusComponent', () => {
  let httpMock: HttpTestingController;

  function setup(expiresInMs: number) {
    TestBed.configureTestingModule({
      imports: [SessionStatusComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    httpMock = TestBed.inject(HttpTestingController);

    const session: SessionResponse = {
      username: 'mock-user',
      sessionId: 'mock-session-id',
      createdAt: new Date(Date.now() - 60_000).toISOString(),
      expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
      remainingSeconds: Math.max(0, Math.ceil(expiresInMs / 1000)),
      status: 'active'
    };
    const fixture = TestBed.createComponent(SessionStatusComponent);
    fixture.componentRef.setInput('session', session);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => vi.useFakeTimers());

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  it('counts down the remaining session time', () => {
    const fixture = setup(125_000);

    vi.advanceTimersByTime(0);
    expect(fixture.componentInstance.formattedRemaining()).toBe('02:05');
    expect(fixture.componentInstance.expired()).toBe(false);

    vi.advanceTimersByTime(5_000);
    expect(fixture.componentInstance.formattedRemaining()).toBe('02:00');
    httpMock.expectNone('/api/auth/session');
  });

  it('asks the API exactly once when the session expires', () => {
    const fixture = setup(1_000);

    vi.advanceTimersByTime(1_000);
    expect(fixture.componentInstance.expired()).toBe(true);
    expect(fixture.componentInstance.formattedRemaining()).toBe('00:00');

    const request = httpMock.expectOne('/api/auth/session');
    vi.advanceTimersByTime(3_000);
    httpMock.expectNone('/api/auth/session');
    request.flush({ message: 'expired' }, { status: 401, statusText: 'Unauthorized' });
  });
});
