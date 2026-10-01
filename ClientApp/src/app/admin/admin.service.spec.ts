import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminService } from './admin.service';

describe('AdminService', () => {
  let service: AdminService;
  let http: HttpTestingController;
  const adminUrl = `${environment.apiUrl}/api/admin`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads users', () => {
    service.getUsers().subscribe();
    const request = http.expectOne(`${adminUrl}/users?page=1&pageSize=25`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('filters reports by status', () => {
    service.getReports('Pending').subscribe();
    const request = http.expectOne(`${adminUrl}/reports?page=1&pageSize=25&status=Pending`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('updates a report status with resolution notes', () => {
    service.updateReport(8, 'Dismissed', 'Reviewed; no violation found.').subscribe();
    const request = http.expectOne(`${adminUrl}/reports/8/status`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      status: 'Dismissed',
      resolutionNotes: 'Reviewed; no violation found.',
      actionTaken: undefined
    });
    request.flush({});
  });

  it('deletes a user account', () => {
    service.deleteUser(12).subscribe();
    const request = http.expectOne(`${adminUrl}/users/12`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('deletes reported scene content', () => {
    service.deleteScene(31).subscribe();
    const request = http.expectOne(`${adminUrl}/scenes/31`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('sends server-side user search and pagination parameters', () => {
    service.getUsers(3, 10, 'trail user').subscribe();
    const request = http.expectOne(`${adminUrl}/users?page=3&pageSize=10&search=trail%20user`);
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], page: 3, pageSize: 10, totalCount: 0 });
  });

  it('loads a paged audit history', () => {
    service.getAudit(2, 10).subscribe();
    const request = http.expectOne(`${adminUrl}/audit?page=2&pageSize=10`);
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0 });
  });

  it('checks live Admin permissions with the server', () => {
    service.checkAccess().subscribe();
    const request = http.expectOne(`${adminUrl}/access`);
    expect(request.request.method).toBe('GET');
    request.flush(null);
  });
});
