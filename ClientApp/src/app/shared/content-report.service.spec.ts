import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ContentReportService } from './content-report.service';

describe('ContentReportService', () => {
  let service: ContentReportService;
  let http: HttpTestingController;
  const usersUrl = `${environment.apiUrl}/api/users`;
  const scenesUrl = `${environment.apiUrl}/api/scenes`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(ContentReportService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('submits a profile report with its description', () => {
    const requestBody = { description: 'This profile contains abusive content.' };
    service.reportProfile(12, requestBody).subscribe();

    const request = http.expectOne(`${usersUrl}/12/reports`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush({ id: 1, targetType: 'Profile', targetId: 12, createdAt: '2026-10-01T00:00:00Z' });
  });

  it('submits a scene report with its description', () => {
    const requestBody = { description: 'This scene contains inappropriate imagery.' };
    service.reportScene(8, requestBody).subscribe();

    const request = http.expectOne(`${scenesUrl}/8/reports`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush({ id: 2, targetType: 'Scene', targetId: 8, createdAt: '2026-10-01T00:00:00Z' });
  });
});
