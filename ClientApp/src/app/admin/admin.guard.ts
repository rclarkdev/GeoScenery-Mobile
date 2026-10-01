import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AdminService } from './admin.service';

@Injectable({ providedIn: 'root' })
export class AdminGuard implements CanActivate {
  constructor(private authService: AuthService, private adminService: AdminService, private router: Router) { }

  canActivate(): Observable<boolean | UrlTree> {
    if (!this.authService.isAuthenticated) {
      return of(this.router.parseUrl('/auth'));
    }

    if (!this.authService.isAdmin) {
      return of(this.router.parseUrl('/scenery/tabs/profile'));
    }

    // The JWT only decides whether to try the route. The API's live DB-backed
    // authorization check revokes access immediately after an admin demotion.
    return this.adminService.checkAccess().pipe(
      map(() => true),
      catchError(() => of(this.router.parseUrl('/scenery/tabs/profile')))
    );
  }
}
