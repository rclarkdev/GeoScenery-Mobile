import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { throwError } from 'rxjs';
import { SKIP_AUTH } from '../auth/auth.interceptor';
import { PLATFORM_CONFIG_URL, SiteIdentityService } from './site-identity.service';

describe('SiteIdentityService', () => {
  let service: SiteIdentityService;
  let http: HttpTestingController;
  const platformOrigin = 'https://platform.example';
  const endpoint = `${platformOrigin}/api/site/config`;

  function configure(origin: string | undefined): void {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        SiteIdentityService,
        { provide: PLATFORM_CONFIG_URL, useValue: origin }
      ]
    });
    service = TestBed.inject(SiteIdentityService);
    http = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    if (http) {
      http.verify();
    }
    TestBed.resetTestingModule();
  });

  it('accepts the expected site key and optional display name while ignoring branding metadata', async () => {
    configure(platformOrigin);
    const initialization = service.initialize();
    const request = http.expectOne(endpoint);

    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(SKIP_AUTH)).toBe(true);
    expect(request.request.withCredentials).toBe(false);
    request.flush({
      key: 'geoscenery',
      name: 'GeoScenery',
      logoUrl: '/assets/platform-logo.png',
      theme: { themeKey: 'collections', primaryColor: '#FFFFFF' },
      futureFeatures: { marketplace: true }
    });

    await initialization;

    expect(service.currentState).toEqual({
      status: 'available',
      identity: { key: 'geoscenery', name: 'GeoScenery' }
    });
  });

  it('accepts the required key when optional name is absent', async () => {
    configure(platformOrigin);
    const initialization = service.initialize();
    http.expectOne(endpoint).flush({ key: 'geoscenery' });

    await initialization;

    expect(service.currentState.identity).toEqual({ key: 'geoscenery' });
  });

  it('rejects a different site key and leaves identity unset', async () => {
    configure(platformOrigin);
    const keys = ['collectionsofmine', 'GeoScenery', ' geoscenery'];

    for (const key of keys) {
      const initialization = service.initialize();
      http.expectOne(endpoint).flush({ key, name: 'Other site' });

      await initialization;

      expect(service.currentState).toEqual({
        status: 'unavailable', identity: null, failure: 'site-key-mismatch'
      });
    }
  });

  it('rejects a missing or malformed required key', async () => {
    configure(platformOrigin);
    const initialization = service.initialize();
    http.expectOne(endpoint).flush({ name: 'GeoScenery', logoUrl: '/assets/logo.png' });

    await initialization;

    expect(service.currentState).toEqual({
      status: 'unavailable', identity: null, failure: 'invalid-payload'
    });
  });

  it('rejects malformed optional identity metadata', async () => {
    configure(platformOrigin);
    for (const name of ['  ', 'N'.repeat(201), null]) {
      const initialization = service.initialize();
      http.expectOne(endpoint).flush({ key: 'geoscenery', name });

      await initialization;

      expect(service.currentState.failure).toBe('invalid-payload');
      expect(service.currentState.identity).toBeNull();
    }
  });

  it('trims a provided display name and accepts the 200-character limit', async () => {
    configure(platformOrigin);
    const name = 'N'.repeat(200);
    const initialization = service.initialize();
    http.expectOne(endpoint).flush({ key: 'geoscenery', name: ` ${name} ` });

    await initialization;

    expect(service.currentState).toEqual({
      status: 'available', identity: { key: 'geoscenery', name }
    });
  });

  it('classifies an HTTP 200 JSON parse failure as an invalid payload', async () => {
    configure(platformOrigin);
    spyOn<any>(service['http'], 'get').and.returnValue(throwError(() => new HttpErrorResponse({
      status: 200,
      statusText: 'OK',
      error: new SyntaxError('Unexpected end of JSON input')
    })));

    await service.initialize();

    expect(service.currentState).toEqual({
      status: 'unavailable', identity: null, failure: 'invalid-payload'
    });
  });

  it('falls back on HTTP 404 and server errors without rejecting initialization', async () => {
    configure(platformOrigin);
    const notFoundInitialization = service.initialize();
    http.expectOne(endpoint).flush({ message: 'not found' }, { status: 404, statusText: 'Not Found' });
    await expectAsync(notFoundInitialization).toBeResolvedTo(undefined);
    expect(service.currentState).toEqual({
      status: 'unavailable', identity: null, failure: 'http-error', httpStatus: 404
    });

    const serverErrorInitialization = service.initialize();
    http.expectOne(endpoint).flush({ message: 'unavailable' }, { status: 500, statusText: 'Server Error' });
    await expectAsync(serverErrorInitialization).toBeResolvedTo(undefined);
    expect(service.currentState.httpStatus).toBe(500);
    expect(service.currentState.identity).toBeNull();
  });

  it('falls back on offline or CORS-style network failures', async () => {
    configure(platformOrigin);
    const initialization = service.initialize();
    http.expectOne(endpoint).error(new ProgressEvent('error'));

    await expectAsync(initialization).toBeResolvedTo(undefined);

    expect(service.currentState).toEqual({
      status: 'unavailable', identity: null, failure: 'network-error'
    });
  });

  it('times out a stalled platform request and leaves the app on local defaults', fakeAsync(() => {
    configure(platformOrigin);
    let initialized = false;
    void service.initialize().then(() => initialized = true);
    http.expectOne(endpoint);

    tick(8000);
    flushMicrotasks();

    expect(initialized).toBe(true);
    expect(service.currentState).toEqual({
      status: 'unavailable', identity: null, failure: 'network-error'
    });
  }));

  it('classifies a browser CORS-style status-zero failure as a network fallback', async () => {
    configure(platformOrigin);
    const initialization = service.initialize();
    http.expectOne(endpoint).error(new ProgressEvent('error'));

    await expectAsync(initialization).toBeResolvedTo(undefined);

    expect(service.currentState.failure).toBe('network-error');
    expect(service.currentState.identity).toBeNull();
  });

  it('uses local defaults without making a request when platform config is unconfigured', async () => {
    configure(undefined);

    await service.initialize();

    expect(service.currentState).toEqual({ status: 'unconfigured', identity: null });
    http.expectNone(() => true);
  });

  it('falls back when the configured origin is invalid', async () => {
    configure('file:///platform');

    await service.initialize();

    expect(service.currentState).toEqual({
      status: 'unavailable', identity: null, failure: 'invalid-config-url'
    });
    http.expectNone(() => true);
  });
});
