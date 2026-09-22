import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Visit } from './visit.model';

export interface CreateVisitRequest {
  sceneId: number;
  visitedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class VisitsService {
  private readonly visitsUrl = `${environment.apiUrl}/api/visits`;

  constructor(private http: HttpClient) { }

  getUserVisits(): Observable<Visit[]> {
    return this.http.get<Visit[]>(`${this.visitsUrl}/me`);
  }

  recordVisit(sceneId: number): Observable<Visit> {
    const request: CreateVisitRequest = { sceneId };
    return this.http.post<Visit>(this.visitsUrl, request);
  }
}