import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  const authUrl = `${environment.apiUrl}/api/auth`;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('keeps the token in the current app session after a successful login', () => {
    service.login({ email: 'ava@example.com', password: 'Password123!' }).subscribe();
    const request = http.expectOne(`${authUrl}/login`);
    request.flush({ userId: 1, displayName: 'Ava', email: 'ava@example.com', token: createToken(Date.now() + 60000) });

    expect(service.isAuthenticated).toBeTrue();
    expect(localStorage.getItem('geoscenery.auth.token')).toBeNull();
    expect(sessionStorage.getItem('geoscenery.auth.token')).not.toBeNull();
  });

  it('posts registration details to the registration endpoint', () => {
    service.register({ displayName: 'Ava', email: 'ava@example.com', password: 'Password123!', confirmPassword: 'Password123!' }).subscribe();
    const request = http.expectOne(`${authUrl}/register`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body.email).toBe('ava@example.com');
    expect(request.request.body.confirmPassword).toBe('Password123!');
    request.flush({ message: 'Check your email.' });
  });

  it('posts a verification token to the verify endpoint', () => {
    service.verifyEmail('verification-token').subscribe();
    const request = http.expectOne(`${authUrl}/verify-email`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'verification-token' });
    request.flush(null);
  });

  it('requests a replacement verification link by email', () => {
    service.resendVerification('ava@example.com').subscribe();
    const request = http.expectOne(`${authUrl}/resend-verification`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'ava@example.com' });
    request.flush({ message: 'If the account exists, a verification email has been sent.' });
  });

  it('rejects an expired token', () => {
    service.login({ email: 'ava@example.com', password: 'Password123!' }).subscribe();
    http.expectOne(`${authUrl}/login`).flush({
      userId: 1,
      displayName: 'Ava',
      email: 'ava@example.com',
      token: createToken(Date.now() - 60000)
    });

    expect(service.isAuthenticated).toBeFalse();
  });

  it('rejects a malformed token', () => {
    service.login({ email: 'ava@example.com', password: 'Password123!' }).subscribe();
    http.expectOne(`${authUrl}/login`).flush({
      userId: 1,
      displayName: 'Ava',
      email: 'ava@example.com',
      token: 'not-a-jwt'
    });

    expect(service.isAuthenticated).toBeFalse();
  });

  it('removes authentication data when logging out', () => {
    localStorage.setItem('geoscenery.auth.token', 'token');
    localStorage.setItem('geoscenery.auth.user', '{}');
    sessionStorage.setItem('geoscenery.auth.token', 'token');
    sessionStorage.setItem('geoscenery.auth.user', '{}');

    service.logout();

    expect(service.token).toBeNull();
    expect(localStorage.getItem('geoscenery.auth.user')).toBeNull();
    expect(sessionStorage.getItem('geoscenery.auth.user')).toBeNull();
  });

  it('restores authentication after a page refresh in the same app session', () => {
    service.login({ email: 'ava@example.com', password: 'Password123!' }).subscribe();
    http.expectOne(`${authUrl}/login`).flush({
      userId: 1,
      displayName: 'Ava',
      email: 'ava@example.com',
      token: createToken(Date.now() + 60000)
    });

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);

    expect(service.isAuthenticated).toBeTrue();
    expect(service.currentUserId).toBe(1);
  });

  it('does not restore authentication from a previous app run', () => {
    localStorage.setItem('geoscenery.auth.token', createToken(Date.now() + 60000));
    localStorage.setItem('geoscenery.auth.user', JSON.stringify({ userId: 1 }));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);

    expect(service.isAuthenticated).toBeFalse();
    expect(localStorage.getItem('geoscenery.auth.token')).toBeNull();
    expect(localStorage.getItem('geoscenery.auth.user')).toBeNull();
  });

  function createToken(expiresAt: number): string {
    const payload = btoa(JSON.stringify({ exp: Math.floor(expiresAt / 1000) }))
      .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    return `header.${payload}.signature`;
  }
});
