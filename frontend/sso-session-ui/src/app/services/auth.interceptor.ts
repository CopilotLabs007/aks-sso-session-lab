import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpResponse
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, tap, throwError } from 'rxjs';
import { SessionStore } from './session.store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router);
  const sessionStore = inject(SessionStore);
  const credentialedRequest = request.clone({ withCredentials: true });

  return next(credentialedRequest).pipe(
    tap((event) => {
      if (event instanceof HttpResponse) {
        console.info(`[API] ${request.method} ${request.urlWithParams} -> ${event.status}`);
      }
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        console.warn(`[API] ${request.method} ${request.urlWithParams} -> ${error.status}`);
        const isCredentialSubmission = request.url.endsWith('/sso-authenticate');
        if (error.status === 401 && !isCredentialSubmission) {
          sessionStore.clear();
          void router.navigateByUrl('/');
        }
      }

      return throwError(() => error);
    })
  );
};