import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { AlertController, IonicModule, NavController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { EditProfilePage } from './edit-profile.page';
import { AuthService } from '../../../auth/auth.service';
import { UserService } from '../../../auth/user.service';
import { User } from '../../../auth/user.model';
import { ImageUploadService } from '../../../shared/image-upload.service';
import { ImageUrlPipe } from '../../../shared/image-url.pipe';

describe('EditProfilePage', () => {
  let component: EditProfilePage;
  let fixture: ComponentFixture<EditProfilePage>;
  let userService: jasmine.SpyObj<UserService>;
  let imageUploadService: jasmine.SpyObj<ImageUploadService>;
  let alertController: jasmine.SpyObj<AlertController>;

  beforeEach(waitForAsync(() => {
    userService = jasmine.createSpyObj('UserService', ['getCurrentUser', 'updateUser', 'changeEmail', 'changePassword', 'deleteUser']);
    userService.getCurrentUser.and.returnValue(of(new User(1, 'Test user', 'test@example.com', 'photo.jpg', 10, 20)));
    userService.updateUser.and.returnValue(of(new User(1, 'Updated user', 'updated@example.com')));
    userService.changeEmail.and.returnValue(of(new User(1, 'Test user', 'updated@example.com')));
    userService.changePassword.and.returnValue(of(undefined));
    userService.deleteUser.and.returnValue(of(undefined));
    imageUploadService = jasmine.createSpyObj('ImageUploadService', ['uploadSelectedImage']);
    imageUploadService.uploadSelectedImage.and.returnValue(of({ url: '/uploads/new-photo.jpg' }));
    alertController = jasmine.createSpyObj('AlertController', ['create']);

    TestBed.configureTestingModule({
      declarations: [ EditProfilePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot(), ImageUrlPipe],
      providers: [
        { provide: AuthService, useValue: { currentUserId: 1, logout: jasmine.createSpy('logout') } },
        { provide: UserService, useValue: userService },
        { provide: ImageUploadService, useValue: imageUploadService },
        { provide: NavController, useValue: {
          navigateBack: jasmine.createSpy('navigateBack'),
          navigateRoot: jasmine.createSpy('navigateRoot')
        } },
        { provide: AlertController, useValue: alertController }
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

  it('saves the profile, preserving the existing photo and location, and navigates back', async () => {
    const navController = TestBed.inject(NavController);
    component.profileForm.patchValue({ displayName: 'Updated user', bio: 'Loves hiking.' });

    await component.onSave();

    expect(userService.updateUser).toHaveBeenCalledWith(1, jasmine.objectContaining({
      displayName: 'Updated user',
      bio: 'Loves hiking.',
      profileImageUrl: 'photo.jpg',
      latitude: 10,
      longitude: 20
    }));
    expect(navController.navigateBack).toHaveBeenCalledWith('/scenery/tabs/profile');
  });

  it('does not save an invalid form', async () => {
    component.profileForm.patchValue({ displayName: '' });

    await component.onSave();

    expect(userService.updateUser).not.toHaveBeenCalled();
  });

  it('changes email using the current password', () => {
    component.emailForm.setValue({ email: 'updated@example.com', currentPassword: 'Password123!' });

    component.onChangeEmail();

    expect(userService.changeEmail).toHaveBeenCalledWith({ email: 'updated@example.com', currentPassword: 'Password123!' });
    expect(component.emailMessage).toBe('Email address updated.');
    expect(component.emailForm.controls.currentPassword.value).toBe('');
  });

  it('shows only one credential editor at a time', () => {
    component.toggleEmailForm();
    expect(component.isEmailFormOpen).toBeTrue();
    expect(component.isPasswordFormOpen).toBeFalse();

    component.togglePasswordForm();
    expect(component.isEmailFormOpen).toBeFalse();
    expect(component.isPasswordFormOpen).toBeTrue();
  });

  it('does not change a password when confirmation does not match', () => {
    component.passwordForm.setValue({
      currentPassword: 'Password123!',
      newPassword: 'NewPassword456!',
      confirmPassword: 'DifferentPassword789!'
    });

    component.onChangePassword();

    expect(userService.changePassword).not.toHaveBeenCalled();
    expect(component.passwordForm.controls.confirmPassword.hasError('passwordMismatch')).toBeTrue();
  });

  it('changes a password and clears the sensitive fields', () => {
    component.passwordForm.setValue({
      currentPassword: 'Password123!',
      newPassword: 'NewPassword456!',
      confirmPassword: 'NewPassword456!'
    });

    component.onChangePassword();

    expect(userService.changePassword).toHaveBeenCalledWith({
      currentPassword: 'Password123!',
      newPassword: 'NewPassword456!'
    });
    expect(component.passwordMessage).toBe('Password updated.');
    expect(component.passwordForm.controls.currentPassword.value).toBe('');
  });

  it('uploads a newly selected photo when saving the profile', async () => {
    component.profileImageUrl = 'blob:http://localhost/photo-id';

    await component.onSave();

    expect(imageUploadService.uploadSelectedImage).toHaveBeenCalledWith('blob:http://localhost/photo-id', 'profile');
    expect(userService.updateUser).toHaveBeenCalledWith(1, jasmine.objectContaining({
      profileImageUrl: '/uploads/new-photo.jpg'
    }));
  });

  it('places account deletion in the advanced section', () => {
    const content = fixture.nativeElement.textContent;

    expect(content).toContain('Advanced');
    expect(content).toContain('Delete account');
  });

  it('asks for confirmation before deleting the account', async () => {
    const present = jasmine.createSpy('present');
    alertController.create.and.resolveTo({ present } as any);

    await component.confirmDeleteAccount();

    expect(alertController.create).toHaveBeenCalledWith(jasmine.objectContaining({
      header: 'Delete account?'
    }));
    expect(present).toHaveBeenCalled();
  });

  it('sets saveError when the save request fails', async () => {
    userService.updateUser.and.returnValue(throwError(() => new Error('failed')));

    await component.onSave();

    expect(component.saveError).toBe(true);
    expect(component.isSaving).toBe(false);
  });
});
