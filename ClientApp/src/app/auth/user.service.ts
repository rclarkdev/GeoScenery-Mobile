import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { User } from './user.model';
import { UserSummary } from './user-summary.model';
import { environment } from '../../environments/environment';

export interface UpdateUserRequest {
  displayName: string;
  email: string;
  profileImageUrl?: string | null;
  latitude?: number | null;
  longitude?: number | null;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly usersUrl = `${environment.apiUrl}/api/users`;

  constructor(private http: HttpClient) { }

  getCurrentUser(): Observable<User> {
    return this.http.get<User>(`${this.usersUrl}/me`);
  }

  getUser(id: number): Observable<User> {
    return this.http.get<User>(`${this.usersUrl}/${id}`);
  }

  updateUser(id: number, user: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.usersUrl}/${id}`, user);
  }

  followUser(id: number): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/${id}/follow`, {});
  }

  unfollowUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.usersUrl}/${id}/follow`);
  }

  getFollowers(id: number): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${this.usersUrl}/${id}/followers`);
  }

  getFollowing(id: number): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${this.usersUrl}/${id}/following`);
  }
}

