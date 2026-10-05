export interface PasswordResetResponse {
  message: string;
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthResponse {
  userId: number;
  displayName: string;
  email: string;
  token: string;
}

export interface AuthRequest {
  email: string;
  password: string;
  displayName?: string;
  confirmPassword?: string;
}

export interface RegistrationResponse {
  message: string;
  emailSent: boolean;
}

export interface EmailVerificationResponse {
  message: string;
  emailSent: boolean | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authUrl = `${environment.apiUrl}/api/auth`;
  private readonly tokenKey = 'geoscenery.auth.token';
  private readonly userKey = 'geoscenery.auth.user';

  constructor(private http: HttpClient) {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
  }

  login(request: AuthRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.authUrl}/login`, request).pipe(tap(response => this.store(response)));
  }

  register(request: AuthRequest): Observable<RegistrationResponse> {
    return this.http.post<RegistrationResponse>(`${this.authUrl}/register`, request);
  }

  verifyEmail(token: string): Observable<void> {
    return this.http.post<void>(`${this.authUrl}/verify-email`, { token });
  }

  resendVerification(email: string): Observable<EmailVerificationResponse> {
    return this.http.post<EmailVerificationResponse>(`${this.authUrl}/resend-verification`, { email });
  }

  requestPasswordReset(email: string): Observable<PasswordResetResponse> {
    return this.http.post<PasswordResetResponse>(`${this.authUrl}/forgot-password`, { email });
  }

  resetPassword(token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.authUrl}/reset-password`, { token, password });
  }

  get token(): string | null { return sessionStorage.getItem(this.tokenKey); }

  get currentUserId(): number | null {
    const stored = sessionStorage.getItem(this.userKey);
    if (!stored) {
      return null;
    }

    try {
      return (JSON.parse(stored) as AuthResponse).userId;
    } catch {
      return null;
    }
  }

  get roles(): string[] {
    const token = this.token;
    if (!token) {
      return [];
    }

    try {
      const payload = JSON.parse(this.decodeBase64Url(token.split('.')[1])) as Record<string, unknown>;
      const roleClaim = payload.role ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      if (typeof roleClaim === 'string') {
        return [roleClaim];
      }
      return Array.isArray(roleClaim) ? roleClaim.filter((role): role is string => typeof role === 'string') : [];
    } catch {
      return [];
    }
  }

  get isAdmin(): boolean {
    return this.isAuthenticated && this.roles.includes('Admin');
  }

  get isAuthenticated(): boolean {
    const token = this.token;
    if (!token) {
      return false;
    }

    try {
      const payload = JSON.parse(this.decodeBase64Url(token.split('.')[1])) as { exp?: number };
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  logout(): void {
    sessionStorage.removeItem(this.tokenKey);
    sessionStorage.removeItem(this.userKey);
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
  }

  private store(response: AuthResponse): void {
    sessionStorage.setItem(this.tokenKey, response.token);
    sessionStorage.setItem(this.userKey, JSON.stringify(response));
  }

  private decodeBase64Url(value: string): string {
    const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
    return atob(normalized.padEnd(normalized.length + (4 - normalized.length % 4) % 4, '='));
  }
}