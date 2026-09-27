import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Camera, CameraDirection, CameraResultType, CameraSource } from '@capacitor/camera';
import { Geolocation } from '@capacitor/geolocation';
import { AlertController, NavController } from '@ionic/angular';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../../auth/auth.service';
import { User } from '../../../auth/user.model';
import { UserService } from '../../../auth/user.service';
import { ImageUploadService } from '../../../shared/image-upload.service';

@Component({
  selector: 'app-edit-profile',
  templateUrl: './edit-profile.page.html',
  styleUrls: ['./edit-profile.page.scss'],
})
export class EditProfilePage implements OnInit {
  user?: User;
  profileImageUrl: string | null = null;
  isSaving = false;
  saveError = false;
  photoError = false;
  isLocating = false;
  locationError = false;
  isChangingEmail = false;
  isEmailFormOpen = false;
  emailMessage: string | null = null;
  emailError: string | null = null;
  isChangingPassword = false;
  isPasswordFormOpen = false;
  passwordMessage: string | null = null;
  passwordError: string | null = null;

  readonly profileForm = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    birthDate: this.formBuilder.control<string | null>(null),
    education: this.formBuilder.control<string | null>(null, [Validators.maxLength(200)]),
    hobbies: this.formBuilder.control<string | null>(null, [Validators.maxLength(500)]),
    employment: this.formBuilder.control<string | null>(null, [Validators.maxLength(200)]),
    bio: this.formBuilder.control<string | null>(null, [Validators.maxLength(2000)])
  });

  readonly emailForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    currentPassword: ['', Validators.required]
  });

  readonly passwordForm = this.formBuilder.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
    confirmPassword: ['', Validators.required]
  });

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private userService: UserService,
    private imageUploadService: ImageUploadService,
    private navCtrl: NavController,
    private alertController: AlertController
  ) { }

  ngOnInit() {
    this.userService.getCurrentUser().subscribe(user => {
      this.user = user;
      this.profileImageUrl = user.profileImageUrl ?? null;
      this.profileForm.setValue({
        displayName: user.displayName,
        birthDate: user.birthDate ?? null,
        education: user.education ?? null,
        hobbies: user.hobbies ?? null,
        employment: user.employment ?? null,
        bio: user.bio ?? null
      });
      this.emailForm.controls.email.setValue(user.email ?? '');
    });
  }

  toggleEmailForm(): void {
    this.isEmailFormOpen = !this.isEmailFormOpen;
    this.isPasswordFormOpen = false;
    this.emailError = null;
    this.emailMessage = null;
    this.emailForm.controls.currentPassword.reset();
    this.emailForm.controls.email.setValue(this.user?.email ?? '');
  }

  togglePasswordForm(): void {
    this.isPasswordFormOpen = !this.isPasswordFormOpen;
    this.isEmailFormOpen = false;
    this.passwordError = null;
    this.passwordMessage = null;
    this.passwordForm.reset();
  }

  async onPickPhoto(): Promise<void> {
    this.photoError = false;
    try {
      const photo = await Camera.getPhoto({
        quality: 80,
        direction: CameraDirection.Front,
        resultType: CameraResultType.Uri,
        source: CameraSource.Prompt
      });
      if (photo.webPath) {
        this.profileImageUrl = photo.webPath;
      }
    } catch (error) {
      if (!(error instanceof Error) || !/cancel/i.test(error.message)) {
        this.photoError = true;
      }
    }
  }

  async useCurrentLocation(): Promise<void> {
    if (!this.user) {
      return;
    }

    this.isLocating = true;
    this.locationError = false;
    try {
      const position = await Geolocation.getCurrentPosition();
      this.user.latitude = position.coords.latitude;
      this.user.longitude = position.coords.longitude;
    } catch {
      this.locationError = true;
    } finally {
      this.isLocating = false;
    }
  }

  onChangeEmail(): void {
    if (this.emailForm.invalid || this.isChangingEmail) {
      this.emailForm.markAllAsTouched();
      return;
    }

    this.isChangingEmail = true;
    this.emailMessage = null;
    this.emailError = null;
    this.userService.changeEmail(this.emailForm.getRawValue()).subscribe({
      next: user => {
        this.user = user;
        this.emailForm.controls.email.setValue(user.email ?? '');
        this.emailForm.controls.currentPassword.reset();
        this.emailForm.markAsPristine();
        this.isChangingEmail = false;
        this.isEmailFormOpen = false;
        this.emailMessage = 'Email address updated.';
      },
      error: (error: HttpErrorResponse) => {
        this.isChangingEmail = false;
        this.emailError = error.status === 401
          ? 'Your current password is incorrect.'
          : error.status === 409
            ? 'That email address is already in use.'
            : error.status === 429
              ? 'Too many attempts. Please try again later.'
            : 'Unable to update your email address.';
      }
    });
  }

  onChangePassword(): void {
    if (this.passwordForm.invalid || this.isChangingPassword) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.getRawValue();
    if (newPassword !== confirmPassword) {
      this.passwordForm.controls.confirmPassword.setErrors({ passwordMismatch: true });
      return;
    }

    this.isChangingPassword = true;
    this.passwordMessage = null;
    this.passwordError = null;
    this.userService.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.passwordForm.reset();
        this.passwordForm.markAsPristine();
        this.isChangingPassword = false;
        this.isPasswordFormOpen = false;
        this.passwordMessage = 'Password updated.';
      },
      error: (error: HttpErrorResponse) => {
        this.isChangingPassword = false;
        this.passwordError = error.status === 401
          ? 'Your current password is incorrect.'
          : error.status === 400
            ? 'Choose a new password that differs from your current password.'
            : error.status === 429
              ? 'Too many attempts. Please try again later.'
            : 'Unable to update your password.';
      }
    });
  }

  async onSave(): Promise<void> {
    if (!this.user || this.profileForm.invalid || this.isSaving) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    try {
      let profileImageUrl = this.profileImageUrl;
      if (profileImageUrl && profileImageUrl !== this.user.profileImageUrl) {
        profileImageUrl = (await firstValueFrom(
          this.imageUploadService.uploadSelectedImage(profileImageUrl, 'profile')
        )).url;
      }

      await firstValueFrom(this.userService.updateUser(this.user.id, {
        ...this.profileForm.getRawValue(),
        profileImageUrl,
        latitude: this.user.latitude,
        longitude: this.user.longitude
      }));
      await this.navCtrl.navigateBack('/scenery/tabs/profile');
    } catch {
      this.isSaving = false;
      this.saveError = true;
    }
  }

  async confirmDeleteAccount(): Promise<void> {
    const alert = await this.alertController.create({
      header: 'Delete account?',
      message: 'This permanently removes your profile, scenes, ratings, and follows.',
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        {
          text: 'Delete',
          role: 'destructive',
          handler: () => this.deleteAccount()
        }
      ]
    });
    await alert.present();
  }

  private deleteAccount(): void {
    if (!this.user) {
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    this.userService.deleteUser(this.user.id).subscribe({
      next: () => {
        this.authService.logout();
        void this.navCtrl.navigateRoot('/auth');
      },
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }
}
