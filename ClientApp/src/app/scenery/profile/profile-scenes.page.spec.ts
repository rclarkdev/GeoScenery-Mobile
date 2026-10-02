import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';
import { ImageUrlPipe } from '../../shared/image-url.pipe';
import { Scene } from '../scene.model';
import { SceneryService } from '../scenery.service';
import { ProfileScenesPage } from './profile-scenes.page';

describe('ProfileScenesPage', () => {
  let component: ProfileScenesPage;
  let fixture: ComponentFixture<ProfileScenesPage>;
  let userService: jasmine.SpyObj<UserService>;
  let sceneryService: jasmine.SpyObj<SceneryService>;

  const visitedUser = new User(42, 'Scenic Explorer', null);
  const publicScene = new Scene(7, 'Canyon overlook', 'A public view.', '/uploads/canyon.jpg', 8);

  function createPage(userResult = of(visitedUser), scenesResult = of([publicScene])) {
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUser']);
    userService.getUser.and.returnValue(userResult);
    sceneryService = jasmine.createSpyObj<SceneryService>('SceneryService', ['getUserPublicScenes']);
    sceneryService.getUserPublicScenes.and.returnValue(scenesResult);

    TestBed.configureTestingModule({
      declarations: [ProfileScenesPage],
      imports: [ImageUrlPipe, RouterTestingModule],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: SceneryService, useValue: sceneryService },
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['userId', '42']])) } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA]
    });

    fixture = TestBed.createComponent(ProfileScenesPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(() => TestBed.resetTestingModule());

  it('loads the visited user and displays their returned public scenes', () => {
    createPage();

    expect(userService.getUser).toHaveBeenCalledWith(42);
    expect(sceneryService.getUserPublicScenes).toHaveBeenCalledWith(42);
    expect(component.scenes).toEqual([publicScene]);
    expect(fixture.nativeElement.textContent).toContain("Scenic Explorer's scenes");
    expect(fixture.nativeElement.textContent).toContain('Canyon overlook');
    expect(fixture.nativeElement.querySelector('ion-item').getAttribute('ng-reflect-router-link')).toContain('7');
  });

  it('shows an empty-state message when the user has no public scenes', () => {
    createPage(of(visitedUser), of([]));

    expect(fixture.nativeElement.textContent).toContain('Scenic Explorer has no public scenes yet.');
    expect(fixture.nativeElement.querySelector('ion-list')).toBeNull();
  });

  it('shows an error state when the public scene request fails', () => {
    createPage(of(visitedUser), throwError(() => new Error('request failed')));

    expect(component.loadError).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain("Unable to load this user's public scenes.");
  });
});
