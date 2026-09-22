import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { VisitsService } from './visits.service';

describe('VisitsService', () => {
  let service: VisitsService;
  let http: HttpTestingController;
  const visitsUrl = `${environment.apiUrl}/api/visits`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(VisitsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets visits for the current user', () => {
    service.getUserVisits().subscribe();
    const request = http.expectOne(`${visitsUrl}/me`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('records a visit for the current user', () => {
    service.recordVisit(8).subscribe();
    const request = http.expectOne(visitsUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ sceneId: 8 });
    request.flush({ id: 1, sceneId: 8, userId: 4, visitedAt: '2026-09-21T12:00:00Z' });
  });
});