import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Inject, Injectable, InjectionToken } from '@angular/core';
import { BehaviorSubject, firstValueFrom, timeout } from 'rxjs';
import { environment } from '../../environments/environment';
import { SKIP_AUTH } from '../auth/auth.interceptor';

export const PLATFORM_CONFIG_URL = new InjectionToken<string | undefined>(
  'PLATFORM_CONFIG_URL',
  { providedIn: 'root', factory: () => environment.platformConfigUrl }
);

export type SiteIdentityFailure =
  | 'invalid-config-url'
  | 'http-error'
  | 'network-error'
  | 'invalid-payload'
  | 'site-key-mismatch';

export interface PlatformSiteIdentity {
  key: 'geoscenery';
  name?: string;
}

export interface SiteIdentityState {
  status: 'unconfigured' | 'loading' | 'available' | 'unavailable';
  identity: PlatformSiteIdentity | null;
  failure?: SiteIdentityFailure;
  httpStatus?: number;
}

const LOCAL_FALLBACK_STATE: SiteIdentityState = {
  status: 'unconfigured',
  identity: null
};

@Injectable({ providedIn: 'root' })
export class SiteIdentityService {
  private readonly stateSubject = new BehaviorSubject<SiteIdentityState>(LOCAL_FALLBACK_STATE);
  readonly state$ = this.stateSubject.asObservable();

  get currentState(): SiteIdentityState {
    return this.stateSubject.value;
  }

  constructor(
    private http: HttpClient,
    @Inject(PLATFORM_CONFIG_URL) private platformConfigUrl?: string
  ) { }

  /** Loads optional platform identity without delaying or failing application startup. */
  async initialize(): Promise<void> {
    if (!this.platformConfigUrl?.trim()) {
      this.stateSubject.next(LOCAL_FALLBACK_STATE);
      return;
    }

    let endpoint: string;
    try {
      const origin = new URL(this.platformConfigUrl);
      const localDevelopmentHttp = !environment.production
        && origin.protocol === 'http:'
        && ['localhost', '127.0.0.1'].includes(origin.hostname);
      if ((origin.protocol !== 'https:' && !localDevelopmentHttp)
        || origin.username.length > 0 || origin.password.length > 0
        || origin.pathname !== '/' || origin.search.length > 0 || origin.hash.length > 0) {
        throw new Error('Unsupported platform configuration URL.');
      }
      endpoint = new URL('/api/site/config', origin.origin).toString();
    } catch {
      this.stateSubject.next({ status: 'unavailable', identity: null, failure: 'invalid-config-url' });
      return;
    }

    this.stateSubject.next({ status: 'loading', identity: null });
    try {
      const response = await firstValueFrom(this.http.get<unknown>(endpoint, {
        context: new HttpContext().set(SKIP_AUTH, true),
        withCredentials: false
      }).pipe(timeout({ first: 8000 })));
      this.stateSubject.next({ status: 'available', identity: this.validateIdentity(response) });
    } catch (error) {
      if (error instanceof SiteIdentityValidationError) {
        this.stateSubject.next({ status: 'unavailable', identity: null, failure: error.failure });
        return;
      }

      this.stateSubject.next({
        status: 'unavailable',
        identity: null,
        failure: error instanceof HttpErrorResponse && error.status !== 0 ? 'http-error' : 'network-error',
        ...(error instanceof HttpErrorResponse && error.status !== 0 ? { httpStatus: error.status } : {})
      });
    }
  }

  private validateIdentity(value: unknown): PlatformSiteIdentity {
    if (!isRecord(value) || typeof value.key !== 'string') {
      throw new SiteIdentityValidationError('invalid-payload');
    }

    if (value.key !== 'geoscenery') {
      throw new SiteIdentityValidationError('site-key-mismatch');
    }

    if (value.name !== undefined
      && (typeof value.name !== 'string' || value.name.trim().length === 0 || value.name.trim().length > 200)) {
      throw new SiteIdentityValidationError('invalid-payload');
    }

    return {
      key: 'geoscenery',
      ...(typeof value.name === 'string' ? { name: value.name.trim() } : {})
    };
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

class SiteIdentityValidationError extends Error {
  constructor(public readonly failure: 'invalid-payload' | 'site-key-mismatch') {
    super(failure);
  }
}
