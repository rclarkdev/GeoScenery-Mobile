import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { Geolocation } from '@capacitor/geolocation';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';

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
    private authService: AuthService,
    private userService: UserService
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
        resultType: CameraResultType.DataUrl,
        source: CameraSource.Prompt
      });
      if (photo.dataUrl) {
        this.user.profileImageUrl = photo.dataUrl;
        this.save();
      }
    } catch {
      // user cancelled the picker
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
      longitude: this.user.longitude
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
}
