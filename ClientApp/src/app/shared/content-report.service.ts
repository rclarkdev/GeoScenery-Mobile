import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ContentReportReceipt, CreateContentReportRequest } from './content-report.model';

@Injectable({ providedIn: 'root' })
export class ContentReportService {
  private readonly usersUrl = `${environment.apiUrl}/api/users`;
  private readonly scenesUrl = `${environment.apiUrl}/api/scenes`;

  constructor(private http: HttpClient) { }

  reportProfile(userId: number, request: CreateContentReportRequest): Observable<ContentReportReceipt> {
    return this.http.post<ContentReportReceipt>(`${this.usersUrl}/${userId}/reports`, request);
  }

  reportScene(sceneId: number, request: CreateContentReportRequest): Observable<ContentReportReceipt> {
    return this.http.post<ContentReportReceipt>(`${this.scenesUrl}/${sceneId}/reports`, request);
  }
}
