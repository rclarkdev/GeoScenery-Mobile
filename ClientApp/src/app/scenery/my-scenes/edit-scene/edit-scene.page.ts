import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormBuilder, Validators } from '@angular/forms';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { Geolocation } from '@capacitor/geolocation';
import { SceneryService } from '../../scenery.service';
import { NavController } from '@ionic/angular';
import { Scene } from '../../scene.model';

@Component({
  selector: 'app-edit-scene',
  templateUrl: './edit-scene.page.html',
  styleUrls: ['./edit-scene.page.scss'],
})
export class EditScenePage implements OnInit {

  scene?: Scene;
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
    private route: ActivatedRoute,
    private sceneryService: SceneryService,
    private navCtrl: NavController,
    private formBuilder: FormBuilder
  ) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      const sceneId = Number(paramMap.get('sceneId'));
      if (!Number.isInteger(sceneId)) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      this.sceneryService.getScene(sceneId).subscribe(scene => {
        this.scene = scene;
        this.sceneForm.setValue({
          title: scene.title,
          description: scene.description,
          imageUrl: scene.imageUrl,
          rating: scene.rating,
          tags: scene.tags.join(', '),
          latitude: scene.latitude ?? null,
          longitude: scene.longitude ?? null
        });
      });

    });
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
    if (!this.scene || this.sceneForm.invalid || this.isSaving) {
      this.sceneForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    const { tags, ...rest } = this.sceneForm.getRawValue();
    this.sceneryService.updateScene(this.scene.id, {
      ...rest,
      tags: tags.split(',').map(tag => tag.trim()).filter(tag => tag.length > 0)
    }).subscribe({
      next: () => this.navCtrl.navigateBack(`/scenery/tabs/my-scenes/${this.scene?.id}`),
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }

}
