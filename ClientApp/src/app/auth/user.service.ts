import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { User } from './user.model';
import { UserSummary } from './user-summary.model';
import { environment } from '../../environments/environment';

export interface UpdateUserRequest {
  displayName: string;
  profileImageUrl?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  birthDate?: string | null;
  education?: string | null;
  hobbies?: string | null;
  employment?: string | null;
  bio?: string | null;
}

export interface ChangeEmailRequest {
  email: string;
  currentPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
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

  changeEmail(request: ChangeEmailRequest): Observable<User> {
    return this.http.put<User>(`${this.usersUrl}/me/email`, request);
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.put<void>(`${this.usersUrl}/me/password`, request);
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.usersUrl}/${id}`);
  }

  followUser(id: number): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/${id}/follow`, {});
  }

  unfollowUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.usersUrl}/${id}/follow`);
  }

  blockUser(id: number): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/${id}/block`, {});
  }

  unblockUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.usersUrl}/${id}/block`);
  }

  getFollowers(id: number): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${this.usersUrl}/${id}/followers`);
  }

  getFollowing(id: number): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${this.usersUrl}/${id}/following`);
  }
}

