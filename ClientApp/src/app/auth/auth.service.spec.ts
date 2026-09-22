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
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('stores the token after a successful login', () => {
    service.login({ email: 'ava@example.com', password: 'Password123!' }).subscribe();
    const request = http.expectOne(`${authUrl}/login`);
    request.flush({ userId: 1, displayName: 'Ava', email: 'ava@example.com', token: 'token' });

    expect(service.token).toBe('token');
  });

  it('posts registration details to the registration endpoint', () => {
    service.register({ displayName: 'Ava', email: 'ava@example.com', password: 'Password123!' }).subscribe();
    const request = http.expectOne(`${authUrl}/register`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body.email).toBe('ava@example.com');
    request.flush({ userId: 1, displayName: 'Ava', email: 'ava@example.com', token: 'token' });
  });

  it('removes stored authentication data when logging out', () => {
    localStorage.setItem('geoscenery.auth.token', 'token');
    localStorage.setItem('geoscenery.auth.user', '{}');

    service.logout();

    expect(service.token).toBeNull();
    expect(localStorage.getItem('geoscenery.auth.user')).toBeNull();
  });
});
