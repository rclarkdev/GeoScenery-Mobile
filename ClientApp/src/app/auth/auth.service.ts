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

  get isAuthenticated(): boolean { return this.token !== null; }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem('geoscenery.auth.user');
  }

  private store(response: AuthResponse): void {
    localStorage.setItem(this.tokenKey, response.token);
    localStorage.setItem('geoscenery.auth.user', JSON.stringify(response));
  }
}