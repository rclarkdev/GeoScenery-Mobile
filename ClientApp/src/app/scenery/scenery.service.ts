import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Scene } from './scene.model';
import { environment } from '../../environments/environment';

export interface SceneRequest {
  title: string;
  description: string;
  imageUrl: string;
  rating: number;
  ownerUserId?: number;
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
}
