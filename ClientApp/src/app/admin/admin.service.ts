import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AdminUser {
  id: number;
  displayName: string;
  email: string;
  roles: string[];
}

export type AdminReportStatus = 'Pending' | 'Reviewed' | 'Dismissed' | 'Actioned';
export type AdminReportTarget = 'Profile' | 'Scene';

export interface AdminContentReport {
  id: number;
  targetType: AdminReportTarget;
  targetId: number;
  targetLabel: string;
  reporterDisplayName: string;
  reporterEmail: string;
  description: string;
  status: AdminReportStatus;
  createdAt: string;
  resolvedAt?: string | null;
  resolutionNotes?: string | null;
  actionTaken?: string | null;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly adminUrl = `${environment.apiUrl}/api/admin`;

  constructor(private http: HttpClient) { }

  getUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${this.adminUrl}/users`);
  }

  updateUserRoles(userId: number, roles: string[]): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.adminUrl}/users/${userId}/roles`, { roles });
  }

  deleteUser(userId: number): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/users/${userId}`);
  }

  getReports(status?: AdminReportStatus | 'All'): Observable<AdminContentReport[]> {
    let params = new HttpParams();
    if (status && status !== 'All') {
      params = params.set('status', status);
    }
    return this.http.get<AdminContentReport[]>(`${this.adminUrl}/reports`, { params });
  }

  updateReport(reportId: number, status: AdminReportStatus, resolutionNotes?: string, actionTaken?: string): Observable<AdminContentReport> {
    return this.http.put<AdminContentReport>(`${this.adminUrl}/reports/${reportId}/status`, {
      status,
      resolutionNotes,
      actionTaken
    });
  }

  deleteScene(sceneId: number): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/scenes/${sceneId}`);
  }
}
