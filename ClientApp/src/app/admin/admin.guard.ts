import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class AdminGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) { }

  canActivate(): boolean | UrlTree {
    return this.authService.isAdmin
      ? true
      : this.router.parseUrl(this.authService.isAuthenticated ? '/scenery/tabs/profile' : '/auth');
  }
}
