import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthGuard } from './auth.guard';

describe('AuthGuard', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  it('allows authenticated users to access protected routes', () => {
    const router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    const guard = new AuthGuard({ isAuthenticated: true } as any, router);

    expect(guard.canActivate(route, state)).toBeTrue();
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects unauthenticated users to the auth page', () => {
    const redirect = {} as UrlTree;
    const router = jasmine.createSpyObj<Router>('Router', { createUrlTree: redirect });
    const guard = new AuthGuard({ isAuthenticated: false } as any, router);

    expect(guard.canActivate(route, state)).toBe(redirect);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/auth']);
  });
});