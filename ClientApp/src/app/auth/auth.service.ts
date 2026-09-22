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
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authUrl = `${environment.apiUrl}/api/auth`;
  private readonly tokenKey = 'geoscenery.auth.token';

  constructor(private http: HttpClient) { }

  login(request: AuthRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.authUrl}/login`, request).pipe(tap(response => this.store(response)));
  }

  register(request: AuthRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.authUrl}/register`, request).pipe(tap(response => this.store(response)));
  }

  get token(): string | null { return localStorage.getItem(this.tokenKey); }

  get currentUserId(): number | null {
    const stored = localStorage.getItem('geoscenery.auth.user');
    if (!stored) {
      return null;
    }

    try {
      return (JSON.parse(stored) as AuthResponse).userId;
    } catch {
      return null;
    }
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
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem('geoscenery.auth.user');
  }

  private store(response: AuthResponse): void {
    localStorage.setItem(this.tokenKey, response.token);
    localStorage.setItem('geoscenery.auth.user', JSON.stringify(response));
  }

  private decodeBase64Url(value: string): string {
    const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
    return atob(normalized.padEnd(normalized.length + (4 - normalized.length % 4) % 4, '='));
  }
}