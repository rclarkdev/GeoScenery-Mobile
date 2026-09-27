import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { UpdateUserRequest, UserService } from './user.service';
import { User } from './user.model';
import { UserSummary } from './user-summary.model';

describe('UserService', () => {
  let service: UserService;
  let http: HttpTestingController;
  const usersUrl = `${environment.apiUrl}/api/users`;
  const user: User = new User(1, 'Ava', 'ava@example.com');
  const request: UpdateUserRequest = {
    displayName: user.displayName
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(UserService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets the current user', () => {
    service.getCurrentUser().subscribe(result => expect(result).toEqual(user));
    const httpRequest = http.expectOne(`${usersUrl}/me`);
    expect(httpRequest.request.method).toBe('GET');
    httpRequest.flush(user);
  });

  it('gets a user by id', () => {
    service.getUser(user.id).subscribe(result => expect(result).toEqual(user));
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}`);
    expect(httpRequest.request.method).toBe('GET');
    httpRequest.flush(user);
  });

  it('updates a user', () => {
    service.updateUser(user.id, request).subscribe(result => expect(result).toEqual(user));
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}`);
    expect(httpRequest.request.method).toBe('PUT');
    expect(httpRequest.request.body).toEqual(request);
    httpRequest.flush(user);
  });

  it('changes the current user email', () => {
    const changeRequest = { email: 'new@example.com', currentPassword: 'Password123!' };
    service.changeEmail(changeRequest).subscribe(result => expect(result).toEqual(user));
    const httpRequest = http.expectOne(`${usersUrl}/me/email`);
    expect(httpRequest.request.method).toBe('PUT');
    expect(httpRequest.request.body).toEqual(changeRequest);
    httpRequest.flush(user);
  });

  it('changes the current user password', () => {
    const changeRequest = { currentPassword: 'Password123!', newPassword: 'NewPassword456!' };
    service.changePassword(changeRequest).subscribe();
    const httpRequest = http.expectOne(`${usersUrl}/me/password`);
    expect(httpRequest.request.method).toBe('PUT');
    expect(httpRequest.request.body).toEqual(changeRequest);
    httpRequest.flush(null);
  });

  it('deletes a user', () => {
    service.deleteUser(user.id).subscribe();
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}`);
    expect(httpRequest.request.method).toBe('DELETE');
    httpRequest.flush(null);
  });

  it('follows a user', () => {
    service.followUser(user.id).subscribe();
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}/follow`);
    expect(httpRequest.request.method).toBe('POST');
    httpRequest.flush(null);
  });

  it('unfollows a user', () => {
    service.unfollowUser(user.id).subscribe();
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}/follow`);
    expect(httpRequest.request.method).toBe('DELETE');
    httpRequest.flush(null);
  });

  it('gets followers', () => {
    const summary = new UserSummary(2, 'Bob');
    service.getFollowers(user.id).subscribe(result => expect(result).toEqual([summary]));
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}/followers`);
    expect(httpRequest.request.method).toBe('GET');
    httpRequest.flush([summary]);
  });

  it('gets following', () => {
    const summary = new UserSummary(2, 'Bob');
    service.getFollowing(user.id).subscribe(result => expect(result).toEqual([summary]));
    const httpRequest = http.expectOne(`${usersUrl}/${user.id}/following`);
    expect(httpRequest.request.method).toBe('GET');
    httpRequest.flush([summary]);
  });
});
