import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { NavController } from '@ionic/angular';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { Geolocation } from '@capacitor/geolocation';
import { SceneryService } from '../../scenery.service';

@Component({
  selector: 'app-new-scene',
  templateUrl: './new-scene.page.html',
  styleUrls: ['./new-scene.page.scss'],
})
export class NewScenePage implements OnInit {
  isSaving = false;
  saveError = false;
  isLocating = false;
  locationError = false;

  readonly sceneForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    imageUrl: ['', [Validators.required]],
    rating: [0, [Validators.min(0), Validators.max(10)]],
    tags: [''],
    latitude: this.formBuilder.control<number | null>(null, [Validators.min(-90), Validators.max(90)]),
    longitude: this.formBuilder.control<number | null>(null, [Validators.min(-180), Validators.max(180)])
  });

  constructor(
    private formBuilder: FormBuilder,
    private sceneryService: SceneryService,
    private navCtrl: NavController
  ) { }

  ngOnInit() {
    // Default to the device's current location; the user can still edit it manually
    this.useCurrentLocation();
  }

  async useCurrentLocation() {
    this.isLocating = true;
    this.locationError = false;
    try {
      const position = await Geolocation.getCurrentPosition();
      this.sceneForm.patchValue({
        latitude: position.coords.latitude,
        longitude: position.coords.longitude
      });
    } catch {
      this.locationError = true;
    } finally {
      this.isLocating = false;
    }
  }

  async onPickImage() {
    try {
      // Prompt lets the user choose between the camera and their photo library
      const photo = await Camera.getPhoto({
        quality: 80,
        resultType: CameraResultType.DataUrl,
        source: CameraSource.Prompt
      });
      if (photo.dataUrl) {
        this.sceneForm.patchValue({ imageUrl: photo.dataUrl });
        this.sceneForm.controls.imageUrl.markAsTouched();
      }
    } catch {
      // user cancelled the picker
    }
  }

  onSave() {
    if (this.sceneForm.invalid || this.isSaving) {
      this.sceneForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    const { tags, ...rest } = this.sceneForm.getRawValue();
    this.sceneryService.createScene({
      ...rest,
      tags: tags.split(',').map(tag => tag.trim()).filter(tag => tag.length > 0)
    }).subscribe({
      next: () => this.navCtrl.navigateBack('/scenery/tabs/my-scenes'),
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }
}
