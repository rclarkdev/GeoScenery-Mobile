import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { EditProfilePage } from './edit-profile.page';
import { AuthService } from '../../../auth/auth.service';
import { UserService } from '../../../auth/user.service';
import { User } from '../../../auth/user.model';

describe('EditProfilePage', () => {
  let component: EditProfilePage;
  let fixture: ComponentFixture<EditProfilePage>;
  let userService: jasmine.SpyObj<UserService>;

  beforeEach(waitForAsync(() => {
    userService = jasmine.createSpyObj('UserService', ['getCurrentUser', 'updateUser']);
    userService.getCurrentUser.and.returnValue(of(new User(1, 'Test user', 'test@example.com', 'photo.jpg', 10, 20)));
    userService.updateUser.and.returnValue(of(new User(1, 'Updated user', 'updated@example.com')));

    TestBed.configureTestingModule({
      declarations: [ EditProfilePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [
        { provide: AuthService, useValue: { currentUserId: 1 } },
        { provide: UserService, useValue: userService },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.profileForm.controls.displayName.value).toBe('Test user');
  });

  it('saves the profile, preserving the existing photo and location, and navigates back', () => {
    const navController = TestBed.inject(NavController);
    component.profileForm.patchValue({ displayName: 'Updated user', email: 'updated@example.com', bio: 'Loves hiking.' });

    component.onSave();

    expect(userService.updateUser).toHaveBeenCalledWith(1, jasmine.objectContaining({
      displayName: 'Updated user',
      email: 'updated@example.com',
      bio: 'Loves hiking.',
      profileImageUrl: 'photo.jpg',
      latitude: 10,
      longitude: 20
    }));
    expect(navController.navigateBack).toHaveBeenCalledWith('/scenery/tabs/profile');
  });

  it('does not save an invalid form', () => {
    component.profileForm.patchValue({ displayName: '', email: 'not-an-email' });

    component.onSave();

    expect(userService.updateUser).not.toHaveBeenCalled();
  });

  it('sets saveError when the save request fails', () => {
    userService.updateUser.and.returnValue(throwError(() => new Error('failed')));

    component.onSave();

    expect(component.saveError).toBe(true);
    expect(component.isSaving).toBe(false);
  });
});
