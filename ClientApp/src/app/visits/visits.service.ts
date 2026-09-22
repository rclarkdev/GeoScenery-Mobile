import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Visit } from './visit.model';

export interface CreateVisitRequest {
  sceneId: number;
  userId: number;
  visitedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class VisitsService {
  private readonly visitsUrl = `${environment.apiUrl}/api/visits`;

  constructor(private http: HttpClient) { }

  getUserVisits(userId: number = environment.currentUserId): Observable<Visit[]> {
    return this.http.get<Visit[]>(`${this.visitsUrl}/user/${userId}`);
  }

  recordVisit(sceneId: number, userId: number = environment.currentUserId): Observable<Visit> {
    const request: CreateVisitRequest = { sceneId, userId };
    return this.http.post<Visit>(this.visitsUrl, request);
  }
}