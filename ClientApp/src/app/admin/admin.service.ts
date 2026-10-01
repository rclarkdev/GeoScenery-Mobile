import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AdminUser {
  id: number;
  displayName: string;
  email: string;
  roles: string[];
  isSuspended: boolean;
  suspendedAt?: string | null;
  suspensionReason?: string | null;
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
  reviewedByUserId?: number | null;
  reviewedByDisplayName?: string | null;
  reviewedAt?: string | null;
  resolvedAt?: string | null;
  resolutionNotes?: string | null;
  actionTaken?: string | null;
  targetExists: boolean;
  targetIsHidden: boolean;
  targetIsSuspended: boolean;
}

export interface AdminActionAudit {
  id: number;
  actorUserId?: number | null;
  actorDisplayName: string;
  actionType: string;
  targetType: string;
  targetId?: number | null;
  reason?: string | null;
  beforeStateJson?: string | null;
  afterStateJson?: string | null;
  correlationId?: string | null;
  createdAt: string;
}

export interface AdminScene {
  id: number;
  title: string;
  ownerUserId?: number | null;
  isHidden: boolean;
  hiddenAt?: string | null;
  hiddenReason?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly adminUrl = `${environment.apiUrl}/api/admin`;

  constructor(private http: HttpClient) { }

  checkAccess(): Observable<void> {
    return this.http.get<void>(`${this.adminUrl}/access`);
  }

  getUsers(page = 1, pageSize = 25, search = ''): Observable<PagedResult<AdminUser>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResult<AdminUser>>(`${this.adminUrl}/users`, { params });
  }

  updateUserRoles(userId: number, roles: string[]): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.adminUrl}/users/${userId}/roles`, { roles });
  }

  deleteUser(userId: number): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/users/${userId}`);
  }

  getReports(status?: AdminReportStatus | 'All', page = 1, pageSize = 25, targetType?: AdminReportTarget | 'All', search = ''): Observable<PagedResult<AdminContentReport>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status && status !== 'All') {
      params = params.set('status', status);
    }
    if (targetType && targetType !== 'All') {
      params = params.set('targetType', targetType);
    }
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResult<AdminContentReport>>(`${this.adminUrl}/reports`, { params });
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

  updateUserSuspension(userId: number, isSuspended: boolean, reason?: string): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.adminUrl}/users/${userId}/suspension`, { isSuspended, reason });
  }

  setSceneVisibility(sceneId: number, isHidden: boolean, reason?: string): Observable<void> {
    return this.http.put<void>(`${this.adminUrl}/scenes/${sceneId}/visibility`, { isHidden, reason });
  }

  getScenes(page = 1, pageSize = 25, search = '', isHidden?: boolean): Observable<PagedResult<AdminScene>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (isHidden !== undefined) params = params.set('isHidden', isHidden);
    return this.http.get<PagedResult<AdminScene>>(`${this.adminUrl}/scenes`, { params });
  }

  getAudit(page = 1, pageSize = 25): Observable<PagedResult<AdminActionAudit>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<AdminActionAudit>>(`${this.adminUrl}/audit`, { params });
  }
}
