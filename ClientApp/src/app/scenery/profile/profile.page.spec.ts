import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { AlertController } from '@ionic/angular';
import { Geolocation } from '@capacitor/geolocation';
import { of, throwError } from 'rxjs';

import { ProfilePage } from './profile.page';
import { UserService } from '../../auth/user.service';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';
import { ImageUploadService } from '../../shared/image-upload.service';

describe('ProfilePage', () => {
  let component: ProfilePage;
  let fixture: ComponentFixture<ProfilePage>;

  function configure(userIdParam: string | null, currentUserId: number | null, user: User) {
    TestBed.resetTestingModule();
    const paramMap = userIdParam === null ? new Map() : new Map([['userId', userIdParam]]);
    TestBed.configureTestingModule({
      declarations: [ ProfilePage ],
      providers: [
        {
          provide: UserService,
          useValue: {
            getCurrentUser: jasmine.createSpy('getCurrentUser').and.returnValue(of(user)),
            getUser: jasmine.createSpy('getUser').and.returnValue(of(user)),
            followUser: jasmine.createSpy('followUser').and.returnValue(of(undefined)),
            unfollowUser: jasmine.createSpy('unfollowUser').and.returnValue(of(undefined)),
            updateUser: jasmine.createSpy('updateUser').and.returnValue(of(user))
            ,deleteUser: jasmine.createSpy('deleteUser').and.returnValue(of(undefined))
          }
        },
        { provide: AuthService, useValue: { currentUserId } },
        { provide: Router, useValue: { navigateByUrl: jasmine.createSpy('navigateByUrl') } },
        { provide: AlertController, useValue: { create: jasmine.createSpy('create') } },
        { provide: ActivatedRoute, useValue: { paramMap: of(paramMap) } }
        ,{ provide: ImageUploadService, useValue: {} }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    });
  }

  beforeEach(waitForAsync(() => {
    configure(null, 1, new User(1, 'Test user', 'test@example.com'));
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.user?.displayName).toBe('Test user');
  });

  it('treats the profile as your own when no userId param is present', () => {
    expect(component.isOwnProfile).toBe(true);
    expect(TestBed.inject(UserService).getCurrentUser).toHaveBeenCalled();
  });

  it('follows a user that is not currently followed', () => {
    const otherUser = new User(2, 'Other user', null, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, 3, 0, false);
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.isOwnProfile).toBe(false);

    component.toggleFollow();

    expect(TestBed.inject(UserService).followUser).toHaveBeenCalledWith(2);
    expect(component.user?.isFollowedByCurrentUser).toBe(true);
    expect(component.user?.followerCount).toBe(4);
  });

  it('unfollows a user that is currently followed', () => {
    const otherUser = new User(2, 'Other user', null, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, 3, 0, true);
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    component.toggleFollow();

    expect(TestBed.inject(UserService).unfollowUser).toHaveBeenCalledWith(2);
    expect(component.user?.isFollowedByCurrentUser).toBe(false);
    expect(component.user?.followerCount).toBe(2);
  });

  it('sets isFollowChanging back to false when the follow request fails', () => {
    const otherUser = new User(2, 'Other user', null);
    const userService = {
      getCurrentUser: () => of(otherUser),
      getUser: () => of(otherUser),
      followUser: () => throwError(() => new Error('failed')),
      unfollowUser: () => of(undefined),
      updateUser: () => of(otherUser)
    };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      declarations: [ ProfilePage ],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: { currentUserId: 1 } },
        { provide: Router, useValue: { navigateByUrl: jasmine.createSpy('navigateByUrl') } },
        { provide: AlertController, useValue: { create: jasmine.createSpy('create') } },
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['userId', '2']])) } }
        ,{ provide: ImageUploadService, useValue: {} }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    });
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    component.toggleFollow();

    expect(component.isFollowChanging).toBe(false);
  });

  it('sets locationError when useCurrentLocation fails', async () => {
    spyOn(Geolocation, 'getCurrentPosition').and.rejectWith(new Error('denied'));

    await component.useCurrentLocation();

    expect(component.locationError).toBe(true);
    expect(component.isLocating).toBe(false);
  });
});
