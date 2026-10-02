import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { AlertController, NavController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { ProfilePage } from './profile.page';
import { UserService } from '../../auth/user.service';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';
import { ImageUrlPipe } from '../../shared/image-url.pipe';
import { ContentReportService } from '../../shared/content-report.service';

describe('ProfilePage', () => {
  let component: ProfilePage;
  let fixture: ComponentFixture<ProfilePage>;
  let authService: jasmine.SpyObj<AuthService>;
  let alertController: jasmine.SpyObj<AlertController>;
  let navController: jasmine.SpyObj<NavController>;
  let contentReportService: jasmine.SpyObj<ContentReportService>;

  function configure(userIdParam: string | null, currentUserId: number | null, user: User) {
    TestBed.resetTestingModule();
    const paramMap = userIdParam === null ? new Map() : new Map([['userId', userIdParam]]);
    authService = jasmine.createSpyObj('AuthService', ['logout'], { currentUserId });
    alertController = jasmine.createSpyObj('AlertController', ['create']);
    navController = jasmine.createSpyObj('NavController', ['navigateRoot']);
    contentReportService = jasmine.createSpyObj('ContentReportService', ['reportProfile', 'reportScene']);
    contentReportService.reportProfile.and.returnValue(of({ id: 1, targetType: 'Profile', targetId: user.id, createdAt: '' }));
    TestBed.configureTestingModule({
      declarations: [ ProfilePage ],
      imports: [ImageUrlPipe, RouterTestingModule],
      providers: [
        {
          provide: UserService,
          useValue: {
            getCurrentUser: jasmine.createSpy('getCurrentUser').and.returnValue(of(user)),
            getUser: jasmine.createSpy('getUser').and.returnValue(of(user)),
            followUser: jasmine.createSpy('followUser').and.returnValue(of(undefined)),
            unfollowUser: jasmine.createSpy('unfollowUser').and.returnValue(of(undefined)),
            blockUser: jasmine.createSpy('blockUser').and.returnValue(of(undefined)),
            unblockUser: jasmine.createSpy('unblockUser').and.returnValue(of(undefined)),
            updateUser: jasmine.createSpy('updateUser').and.returnValue(of(user))
            ,deleteUser: jasmine.createSpy('deleteUser').and.returnValue(of(undefined))
          }
        },
        { provide: AuthService, useValue: authService },
        { provide: ContentReportService, useValue: contentReportService },
        { provide: AlertController, useValue: alertController },
        { provide: NavController, useValue: navController },
        { provide: ActivatedRoute, useValue: { paramMap: of(paramMap) } }
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

  it('keeps account management and app links off the profile', () => {
    const content = fixture.nativeElement.textContent;

    expect(content).not.toContain('Delete account');
    expect(content).not.toContain('Contact support');
    expect(content).not.toContain('About GeoScenery');
    expect(content).not.toContain('View my scenes');
  });

  it('provides one clear edit action without inline profile controls', () => {
    const content = fixture.nativeElement.textContent;

    expect(content).toContain('Edit profile');
    expect(content).not.toContain('Change photo');
    expect(content).not.toContain('Update location');
  });

  it('shows a logout action on the signed-in user profile', () => {
    expect(fixture.nativeElement.textContent).toContain('Log out');
    expect(fixture.nativeElement.textContent).not.toContain('Report profile');
    expect(fixture.nativeElement.textContent).not.toContain('View public scenes');
  });

  it('prompts for a description and submits a profile report', async () => {
    const otherUser = new User(2, 'Other user', null);
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
    alertController.create.and.resolveTo({
      present: jasmine.createSpy('present').and.resolveTo(undefined),
      onDidDismiss: jasmine.createSpy('onDidDismiss').and.resolveTo({
        role: 'confirm',
        data: { values: { description: 'This profile contains abusive content.' } }
      })
    } as any);

    await component.reportProfile();

    expect(contentReportService.reportProfile).toHaveBeenCalledWith(2, {
      description: 'This profile contains abusive content.'
    });
    expect(component.reportFeedback).toContain('Report submitted');
  });

  it('confirms logout, clears authentication, and navigates to sign in', async () => {
    const present = jasmine.createSpy('present');
    alertController.create.and.callFake(async options => {
      const logoutButton = options.buttons?.[1] as any;
      logoutButton.handler();
      return { present } as any;
    });

    await component.confirmLogout();

    expect(present).toHaveBeenCalled();
    expect(authService.logout).toHaveBeenCalled();
    expect(navController.navigateRoot).toHaveBeenCalledWith('/auth');
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
    expect(fixture.nativeElement.textContent).not.toContain('Log out');
    expect(fixture.nativeElement.textContent).toContain('View public scenes');

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

  it('offers follow and block actions for an unblocked other user', () => {
    const otherUser = new User(2, 'Other user', null);
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    fixture.detectChanges();

    const content = fixture.nativeElement.textContent;
    expect(content).toContain('Follow');
    expect(content).toContain('Block');
    expect(content).not.toContain('Unblock');
  });

  it('blocks a followed user, removes follow state, and offers unblock', async () => {
    const otherUser = new User(2, 'Other user', null, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, undefined, 3, 0, true);
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
    const present = jasmine.createSpy('present');
    alertController.create.and.callFake(async options => {
      (options.buttons?.[1] as any).handler();
      return { present } as any;
    });

    await component.confirmBlock();
    fixture.detectChanges();

    expect(TestBed.inject(UserService).blockUser).toHaveBeenCalledWith(2);
    expect(component.user?.isBlockedByCurrentUser).toBeTrue();
    expect(component.user?.isFollowedByCurrentUser).toBeFalse();
    expect(component.user?.followerCount).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Unblock');
    const buttonLabels = Array.from(fixture.nativeElement.querySelectorAll('ion-button'))
      .map((button: any) => button.textContent.trim());
    expect(buttonLabels).not.toContain('Follow');
  });

  it('unblocks a user and restores the follow option', () => {
    const otherUser = new User(2, 'Other user', null);
    otherUser.isBlockedByCurrentUser = true;
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    component.unblockUser();
    fixture.detectChanges();

    expect(TestBed.inject(UserService).unblockUser).toHaveBeenCalledWith(2);
    expect(component.user?.isBlockedByCurrentUser).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Follow');
  });

  it('hides follow when the other user has blocked the current user', () => {
    const otherUser = new User(2, 'Other user', null);
    otherUser.hasBlockedCurrentUser = true;
    configure('2', 1, otherUser);
    fixture = TestBed.createComponent(ProfilePage);
    fixture.detectChanges();

    const content = fixture.nativeElement.textContent;
    expect(content).toContain("You can't follow this profile.");
    const buttonLabels = Array.from(fixture.nativeElement.querySelectorAll('ion-button'))
      .map((button: any) => button.textContent.trim());
    expect(buttonLabels).not.toContain('Follow');
    expect(content).toContain('Block');
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
      imports: [ImageUrlPipe, RouterTestingModule],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: { currentUserId: 1 } },
        { provide: ContentReportService, useValue: jasmine.createSpyObj('ContentReportService', ['reportProfile', 'reportScene']) },
        { provide: AlertController, useValue: jasmine.createSpyObj('AlertController', ['create']) },
        { provide: NavController, useValue: jasmine.createSpyObj('NavController', ['navigateRoot']) },
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['userId', '2']])) } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    });
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    component.toggleFollow();

    expect(component.isFollowChanging).toBe(false);
  });

});
