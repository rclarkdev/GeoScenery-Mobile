import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Scene } from './scene.model';
import { environment } from '../../environments/environment';

export interface SceneRequest {
  title: string;
  description: string;
  imageUrl: string;
  rating: number;
  latitude?: number | null;
  longitude?: number | null;
  tags?: string[] | null;
  ownerUserId?: number;
}

export interface SceneSearchParams {
  tags?: string[];
  latitude?: number;
  longitude?: number;
  radiusKm?: number;
}

@Injectable({
  providedIn: 'root'
})
export class SceneryService {
  private readonly scenesUrl = `${environment.apiUrl}/api/scenes`;

  constructor(private http: HttpClient) { }

  getScenery(): Observable<Scene[]> {
    return this.http.get<Scene[]>(this.scenesUrl);
  }

  searchScenery(searchParams: SceneSearchParams): Observable<Scene[]> {
    let params = new HttpParams();
    if (searchParams.tags?.length) {
      params = params.set('tags', searchParams.tags.join(','));
    }
    if (searchParams.latitude != null) {
      params = params.set('latitude', searchParams.latitude);
    }
    if (searchParams.longitude != null) {
      params = params.set('longitude', searchParams.longitude);
    }
    if (searchParams.radiusKm != null) {
      params = params.set('radiusKm', searchParams.radiusKm);
    }
    return this.http.get<Scene[]>(this.scenesUrl, { params });
  }

  getScene(id: number): Observable<Scene> {
    return this.http.get<Scene>(`${this.scenesUrl}/${id}`);
  }

  createScene(scene: SceneRequest): Observable<Scene> {
    return this.http.post<Scene>(this.scenesUrl, scene);
  }

  updateScene(id: number, scene: SceneRequest): Observable<Scene> {
    return this.http.put<Scene>(`${this.scenesUrl}/${id}`, scene);
  }

  deleteScene(id: number): Observable<void> {
    return this.http.delete<void>(`${this.scenesUrl}/${id}`);
  }

  rateScene(id: number, rating: number): Observable<Scene> {
    return this.http.post<Scene>(`${this.scenesUrl}/${id}/rating`, { rating });
  }

  removeRating(id: number): Observable<void> {
    return this.http.delete<void>(`${this.scenesUrl}/${id}/rating`);
  }
}
