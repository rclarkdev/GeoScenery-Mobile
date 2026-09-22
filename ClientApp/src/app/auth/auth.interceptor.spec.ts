import { HttpErrorResponse, HttpHandler, HttpRequest } from '@angular/common/http';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';

import { AuthInterceptor } from './auth.interceptor';

describe('AuthInterceptor', () => {
  it('clears the session and redirects after a 401 response', () => {
    const authService = jasmine.createSpyObj('AuthService', ['logout'], { token: 'token' });
    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const handler: HttpHandler = {
      handle: (_request: HttpRequest<unknown>) => throwError(() => new HttpErrorResponse({ status: 401 }))
    };
    const interceptor = new AuthInterceptor(authService, router);

    interceptor.intercept(new HttpRequest('GET', '/api/scenes'), handler).subscribe({ error: () => undefined });

    expect(authService.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/auth']);
  });

  it('adds the bearer token to authenticated requests', () => {
    const authService = jasmine.createSpyObj('AuthService', ['logout'], { token: 'token' });
    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const handler: HttpHandler = {
      handle: (request: HttpRequest<unknown>) => {
        expect(request.headers.get('Authorization')).toBe('Bearer token');
        return throwError(() => new HttpErrorResponse({ status: 500 }));
      }
    };
    const interceptor = new AuthInterceptor(authService, router);

    interceptor.intercept(new HttpRequest('GET', '/api/scenes'), handler).subscribe({ error: () => undefined });

    expect(authService.logout).not.toHaveBeenCalled();
  });
});