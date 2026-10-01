import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AdminGuard } from './admin.guard';
import { AdminService } from './admin.service';

describe('AdminGuard', () => {
  let router: jasmine.SpyObj<Router>;
  let adminService: jasmine.SpyObj<AdminService>;
  let auth: { isAdmin: boolean; isAuthenticated: boolean };

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['parseUrl']);
    router.parseUrl.and.callFake(url => ({ url } as any));
    adminService = jasmine.createSpyObj<AdminService>('AdminService', ['checkAccess']);
    adminService.checkAccess.and.returnValue(of(undefined));
    auth = { isAdmin: false, isAuthenticated: false };
    TestBed.configureTestingModule({
      providers: [
        AdminGuard,
        { provide: AuthService, useValue: auth },
        { provide: AdminService, useValue: adminService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('allows an administrator only after the server confirms current access', async () => {
    auth.isAdmin = true;
    auth.isAuthenticated = true;

    await expectAsync(firstValueFrom(TestBed.inject(AdminGuard).canActivate())).toBeResolvedTo(true);
    expect(adminService.checkAccess).toHaveBeenCalled();
    expect(router.parseUrl).not.toHaveBeenCalled();
  });

  it('redirects a signed-in non-admin to profile without calling the Admin API', async () => {
    auth.isAuthenticated = true;

    await expectAsync(firstValueFrom(TestBed.inject(AdminGuard).canActivate())).toBeResolvedTo({ url: '/scenery/tabs/profile' } as any);
    expect(adminService.checkAccess).not.toHaveBeenCalled();
  });

  it('redirects an unauthenticated visitor to sign-in', async () => {
    await expectAsync(firstValueFrom(TestBed.inject(AdminGuard).canActivate())).toBeResolvedTo({ url: '/auth' } as any);
    expect(adminService.checkAccess).not.toHaveBeenCalled();
  });

  it('redirects a demoted admin when the server rejects the stale token', async () => {
    auth.isAuthenticated = true;
    auth.isAdmin = true;
    adminService.checkAccess.and.returnValue(throwError(() => ({ status: 403 })));

    await expectAsync(firstValueFrom(TestBed.inject(AdminGuard).canActivate())).toBeResolvedTo({ url: '/scenery/tabs/profile' } as any);
  });
});
