import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AlertController } from '@ionic/angular';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { Geolocation } from '@capacitor/geolocation';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';
import { ImageUploadService } from '../../shared/image-upload.service';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  styleUrls: ['./profile.page.scss'],
})
export class ProfilePage implements OnInit {
  user?: User;
  isOwnProfile = false;
  isLocating = false;
  isSaving = false;
  saveError = false;
  locationError = false;
  isFollowChanging = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private alertController: AlertController,
    private authService: AuthService,
    private userService: UserService,
    private imageUploadService: ImageUploadService
  ) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      const userId = paramMap.has('userId') ? Number(paramMap.get('userId')) : this.authService.currentUserId;
      this.isOwnProfile = userId === this.authService.currentUserId;
      const request = this.isOwnProfile ? this.userService.getCurrentUser() : this.userService.getUser(userId!);
      request.subscribe(user => this.user = user);
    });
  }

  toggleFollow() {
    if (!this.user || this.isFollowChanging) {
      return;
    }

    this.isFollowChanging = true;
    const request = this.user.isFollowedByCurrentUser
      ? this.userService.unfollowUser(this.user.id)
      : this.userService.followUser(this.user.id);

    request.subscribe({
      next: () => {
        if (this.user) {
          this.user.isFollowedByCurrentUser = !this.user.isFollowedByCurrentUser;
          this.user.followerCount += this.user.isFollowedByCurrentUser ? 1 : -1;
        }
        this.isFollowChanging = false;
      },
      error: () => this.isFollowChanging = false
    });
  }

  async onPickPhoto() {
    if (!this.user) {
      return;
    }

    try {
      // Prompt lets the user choose between the camera and their photo library
      const photo = await Camera.getPhoto({
        quality: 80,
        resultType: CameraResultType.Uri,
        source: CameraSource.Prompt
      });
      if (photo.webPath) {
        this.isSaving = true;
        this.saveError = false;
        const uploaded = await firstValueFrom(this.imageUploadService.uploadUri(photo.webPath, 'profile'));
        this.user.profileImageUrl = uploaded.url;
        this.save();
      }
    } catch {
      // user cancelled the picker
      this.isSaving = false;
      this.saveError = true;
    }
  }

  async useCurrentLocation() {
    if (!this.user) {
      return;
    }

    this.isLocating = true;
    this.locationError = false;
    try {
      const position = await Geolocation.getCurrentPosition();
      this.user.latitude = position.coords.latitude;
      this.user.longitude = position.coords.longitude;
      this.save();
    } catch {
      this.locationError = true;
    } finally {
      this.isLocating = false;
    }
  }

  private save() {
    if (!this.user) {
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    this.userService.updateUser(this.user.id, {
      displayName: this.user.displayName,
      email: this.user.email ?? '',
      profileImageUrl: this.user.profileImageUrl,
      latitude: this.user.latitude,
      longitude: this.user.longitude,
      birthDate: this.user.birthDate,
      education: this.user.education,
      hobbies: this.user.hobbies,
      employment: this.user.employment,
      bio: this.user.bio
    }).subscribe({
      next: user => {
        this.user = user;
        this.isSaving = false;
      },
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
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
    this.userService.deleteUser(this.user.id).subscribe({
      next: () => {
        this.authService.logout();
        void this.router.navigateByUrl('/auth');
      },
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }
}
