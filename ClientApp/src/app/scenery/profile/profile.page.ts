import { Component, OnInit } from '@angular/core';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { Geolocation } from '@capacitor/geolocation';
import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  styleUrls: ['./profile.page.scss'],
})
export class ProfilePage implements OnInit {
  user?: User;
  isLocating = false;
  isSaving = false;
  saveError = false;
  locationError = false;

  constructor(private userService: UserService) { }

  ngOnInit() {
    this.userService.getCurrentUser().subscribe(user => this.user = user);
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
      email: this.user.email,
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
